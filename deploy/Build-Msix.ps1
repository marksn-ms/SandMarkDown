<#
.SYNOPSIS
    Builds the Microsoft Store MSIX package for SandMarkDown.

.DESCRIPTION
    Publishes a self-contained Release build, so Store users do not need .NET installed,
    fills packaging\AppxManifest.xml, adds the icon assets, and packs an .msix with makeappx.

    The Store signs the package after certification, so this script does not sign it.
    It uses no secrets. The identity values are public and come from Partner Center,
    Product management, Product identity.

    Until the identity exists, run with -LocalCheck. That packs with placeholder identity
    values so the manifest and layout can be validated. A package built that way cannot be
    uploaded.

.PARAMETER IdentityName
    Package/Identity/Name from Partner Center.

.PARAMETER Publisher
    Package/Identity/Publisher from Partner Center, such as CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX.

.PARAMETER PublisherDisplayName
    Package/Properties/PublisherDisplayName from Partner Center.

.PARAMETER Architecture
    x64 or arm64. Default x64.

.PARAMETER LocalCheck
    Use placeholder identity values. The package is for validation only.

.PARAMETER Help
    Prints this help and exits.

.EXAMPLE
    .\deploy\Build-Msix.ps1 -LocalCheck

.EXAMPLE
    .\deploy\Build-Msix.ps1 -IdentityName "12345SandmarkTechnologies.SandMarkDown" -Publisher "CN=..." -PublisherDisplayName "Sandmark Technologies"
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [ValidateSet("x64", "arm64")][string]$Architecture = "x64",
    [switch]$LocalCheck,
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Help -or (-not $LocalCheck -and -not $IdentityName -and -not $Publisher -and -not $PublisherDisplayName)) {
    Get-Help -Name $PSCommandPath -Full
    exit 0
}

if ($LocalCheck) {
    $IdentityName = "SandmarkTechnologies.SandMarkDown.LocalCheck"
    $Publisher = "CN=SandMarkDown Local Check"
    $PublisherDisplayName = "Sandmark Technologies"
}
elseif (-not ($IdentityName -and $Publisher -and $PublisherDisplayName)) {
    throw "Pass -IdentityName, -Publisher, and -PublisherDisplayName from Partner Center, or use -LocalCheck."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\SandMarkDown\SandMarkDown.csproj"
$out = Join-Path $repoRoot "artifacts\msix"
$layout = Join-Path $out "layout-$Architecture"

$makeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\x64\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install the Windows SDK."
}

# The package version must have four parts. The Store requires the last one to be 0.
[xml]$csproj = Get-Content -LiteralPath $project
$version = ($csproj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <Version> in $project." }
$parts = @($version.Split(".") + @("0", "0", "0"))[0..2]
$packageVersion = ($parts -join ".") + ".0"

if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layout | Out-Null

Write-Host "Publishing self-contained win-$Architecture build"
& dotnet publish $project -c Release -r "win-$Architecture" --self-contained true -p:PublishReadyToRun=true -p:SatelliteResourceLanguages=en -o $layout --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

Get-ChildItem -LiteralPath $layout -Filter *.pdb -Recurse | Remove-Item -Force
Copy-Item -Recurse -Force (Join-Path $repoRoot "packaging\Assets") (Join-Path $layout "Assets")

$manifest = (Get-Content -LiteralPath (Join-Path $repoRoot "packaging\AppxManifest.xml") -Raw).
    Replace("@@IDENTITY_NAME@@", [Security.SecurityElement]::Escape($IdentityName)).
    Replace("@@PUBLISHER@@", [Security.SecurityElement]::Escape($Publisher)).
    Replace("@@PUBLISHER_DISPLAY_NAME@@", [Security.SecurityElement]::Escape($PublisherDisplayName)).
    Replace("@@VERSION@@", $packageVersion).
    Replace("@@ARCH@@", $Architecture)
[IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, [Text.UTF8Encoding]::new($false))

$suffix = if ($LocalCheck) { "-localcheck" } else { "" }
$package = Join-Path $out "SandMarkDown_${packageVersion}_$Architecture$suffix.msix"
& $makeAppx.FullName pack /o /d $layout /p $package
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

Write-Host "Built $package"
if ($LocalCheck) {
    Write-Host "Placeholder identity. Validation only. Do not upload this package."
}
