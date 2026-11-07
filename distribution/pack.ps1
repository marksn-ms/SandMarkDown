<#
.SYNOPSIS
    Builds the SandMarkDown Windows x64 installer with Velopack.

.DESCRIPTION
    Publishes a self-contained Release build, then packages it with the pinned vpk tool.
    The product version comes from src\SandMarkDown\SandMarkDown.csproj.
    Identity and file names come from distribution\catalog.json.
    No secrets are required. The script does not sign binaries and does not publish a GitHub Release.

.PARAMETER Version
    Overrides the project version for a local experiment. GitHub Actions does not use this.

.PARAMETER BasePackages
    Directory containing an older full .nupkg. Velopack uses it to build a delta package.

.PARAMETER Configuration
    Build configuration. Default Release.

.PARAMETER Help
    Prints this help and exits.

.EXAMPLE
    .\distribution\pack.ps1

.EXAMPLE
    .\distribution\pack.ps1 -BasePackages .\previous -Version 1.0.1
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version,
    [string]$BasePackages,
    [string]$Configuration = "Release",
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$Root = Split-Path -Parent $PSScriptRoot
$CatalogPath = Join-Path $PSScriptRoot "catalog.json"

function Show-Help {
    Get-Help -Name $PSCommandPath -Full
}

if ($Help) {
    Show-Help
    exit 0
}

$catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json
if ($catalog.runtime -ne "win-x64") {
    Write-Error "The catalog runtime '$($catalog.runtime)' is not win-x64."
}
if ($catalog.channel -ne "win") {
    Write-Error "The catalog channel '$($catalog.channel)' is not the default win channel."
}

$project = Join-Path $Root ($catalog.project -replace '/', '\')
if (-not (Test-Path -LiteralPath $project)) {
    Write-Error "Cannot find $project."
}

[xml]$projectXml = Get-Content -LiteralPath $project
$projectVersion = @($projectXml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -Unique)
if ($projectVersion.Count -ne 1) {
    Write-Error "Expected one <Version> in $project."
}

$releaseVersion = if ([string]::IsNullOrWhiteSpace($Version)) { [string]$projectVersion[0] } else { $Version }
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version '$releaseVersion' must be MAJOR.MINOR.PATCH."
}

$publishDir = Join-Path $Root "artifacts\publish"
$distDir = Join-Path $Root "artifacts\dist"
$uploadDir = Join-Path $Root "artifacts\upload"
foreach ($dir in @($publishDir, $distDir, $uploadDir)) {
    if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir | Out-Null
}

if ($BasePackages) {
    $fullPackages = Get-ChildItem -LiteralPath $BasePackages -Filter ($catalog.packId + "-*-full.nupkg") -File
    if (-not $fullPackages) {
        Write-Error "No $($catalog.packId) full packages were found in '$BasePackages'."
    }
    Copy-Item -LiteralPath $fullPackages.FullName -Destination $distDir
}

$planProject = Join-Path $PSScriptRoot "ReleasePlan\ReleasePlan.csproj"
$icon = Join-Path $Root "src\SandMarkDown\Assets\favicon.ico"

Push-Location $Root
try {
    dotnet tool restore --tool-manifest (Join-Path $Root "dotnet-tools.json")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build $planProject -c $Configuration --verbosity quiet --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "Publishing self-contained win-x64 $releaseVersion"
    dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publishDir --nologo `
        -p:Version=$releaseVersion `
        -p:PublishSingleFile=false `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:SatelliteResourceLanguages=en
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $mainExe = Join-Path $publishDir "SandMarkDown.exe"
    if (-not (Test-Path -LiteralPath $mainExe)) {
        Write-Error "Publish did not produce SandMarkDown.exe."
    }

    # Channel stays at the default win. vpk then names Setup and Portable with -win- and writes releases.win.json.
    dotnet tool run vpk -- pack `
        --yes `
        --packId $catalog.packId `
        --packVersion $releaseVersion `
        --packDir $publishDir `
        --mainExe SandMarkDown.exe `
        --packTitle $catalog.name `
        --packAuthors "Sandmark Technologies" `
        --icon $icon `
        --runtime $catalog.runtime `
        --outputDir $distDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

$fullPackage = $catalog.fullPackagePattern.Replace("{version}", $releaseVersion)
$deltaPackage = $catalog.deltaPackagePattern.Replace("{version}", $releaseVersion)
$wanted = @(
    [string]$catalog.installerAsset,
    [string]$catalog.portableAsset,
    $fullPackage,
    $deltaPackage,
    [string]$catalog.feedAsset
)

foreach ($name in $wanted) {
    $source = Join-Path $distDir $name
    if (-not (Test-Path -LiteralPath $source)) {
        if ($name -eq $deltaPackage) { continue }
        Write-Error "Velopack did not produce $name."
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $uploadDir $name)
}

$feed = Join-Path $uploadDir ([string]$catalog.feedAsset)
dotnet run --project $planProject -c $Configuration --no-build --verbosity quiet -- filter-feed --feed $feed --dir $uploadDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$lines = @()
foreach ($name in $wanted) {
    $uploaded = Join-Path $uploadDir $name
    if (-not (Test-Path -LiteralPath $uploaded)) { continue }
    $hash = (Get-FileHash -LiteralPath $uploaded -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines += "$hash  $name"
}

$checksumPath = Join-Path $uploadDir ([string]$catalog.checksumAsset)
Set-Content -LiteralPath $checksumPath -Value ($lines -join "`n") -Encoding ascii -NoNewline
Add-Content -LiteralPath $checksumPath -Value "" -Encoding ascii

Write-Host "Packaged SandMarkDown $releaseVersion"
Write-Host "Upload files: $uploadDir"
