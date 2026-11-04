<#
.SYNOPSIS
    Publishes SandMarkDown for the current user and associates Markdown files with it.

.DESCRIPTION
    Builds a Release copy into
    %LOCALAPPDATA%\Sandmark Technologies\SandMarkDown\Current
    and points the SandMarkDown.md file type at that program.
    Double-clicking a .md or .markdown file then opens this reader.
    The script changes only the current user's registry. It does not need admin rights.
    It does not use secrets.

.PARAMETER Uninstall
    Removes the published copy and the SandMarkDown.md file type when that type still points at this install.

.PARAMETER Help
    Prints this help and exits.

.EXAMPLE
    .\deploy\Install-SandMarkDown.ps1

.EXAMPLE
    .\deploy\Install-SandMarkDown.ps1 -Uninstall
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Help) {
    Get-Help -Name $PSCommandPath -Full
    exit 0
}

$ProgId = "SandMarkDown.md"
$Extensions = @(".md", ".markdown")
$InstallDir = Join-Path $env:LOCALAPPDATA "Sandmark Technologies\SandMarkDown\Current"
$ExePath = Join-Path $InstallDir "SandMarkDown.exe"

function Set-DefaultValue {
    param(
        [Parameter(Mandatory = $true)][string]$SubKey,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($SubKey)
    try {
        $key.SetValue("", $Value)
    }
    finally {
        $key.Dispose()
    }
}

function Get-DefaultValue {
    param([Parameter(Mandatory = $true)][string]$SubKey)

    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($SubKey)
    if ($null -eq $key) {
        return $null
    }

    try {
        return [string]$key.GetValue("")
    }
    finally {
        $key.Dispose()
    }
}

function Test-PointsAtInstall {
    param([string]$Command)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $false
    }

    return $Command.StartsWith("`"$InstallDir", [StringComparison]::OrdinalIgnoreCase) `
        -or $Command.StartsWith($InstallDir, [StringComparison]::OrdinalIgnoreCase)
}

function Update-Shell {
    # Explorer caches file-type icons and commands. This asks it to reload them.
    Add-Type -Namespace SandMarkDown.Deploy -Name Native -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("shell32.dll")]
public static extern void SHChangeNotify(int eventId, uint flags, System.IntPtr item1, System.IntPtr item2);
"@
    [SandMarkDown.Deploy.Native]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
}

function Register-FileType {
    $command = "`"$ExePath`" `"%1`""
    $icon = "`"$ExePath`",0"

    Set-DefaultValue -SubKey "Software\Classes\$ProgId" -Value "Markdown Document"
    Set-DefaultValue -SubKey "Software\Classes\$ProgId\DefaultIcon" -Value $icon
    Set-DefaultValue -SubKey "Software\Classes\$ProgId\shell\open\command" -Value $command

    Set-DefaultValue -SubKey "Software\Classes\Applications\SandMarkDown.exe\shell\open\command" -Value $command
    Set-DefaultValue -SubKey "Software\Microsoft\Windows\CurrentVersion\App Paths\SandMarkDown.exe" -Value $ExePath

    $classes = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\Applications\SandMarkDown.exe\SupportedTypes")
    try {
        foreach ($extension in $Extensions) {
            if ($null -eq $classes.GetValue($extension)) {
                $classes.SetValue($extension, "", [Microsoft.Win32.RegistryValueKind]::String)
            }
        }
    }
    finally {
        $classes.Dispose()
    }

    foreach ($extension in $Extensions) {
        $extensionKey = "Software\Classes\$extension"
        Set-DefaultValue -SubKey $extensionKey -Value $ProgId

        $openWith = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("$extensionKey\OpenWithProgids")
        try {
            if ($null -eq $openWith.GetValue($ProgId)) {
                $openWith.SetValue($ProgId, [byte[]]::new(0), [Microsoft.Win32.RegistryValueKind]::None)
            }
        }
        finally {
            $openWith.Dispose()
        }
    }
}

function Unregister-FileType {
    $commandKey = "Software\Classes\$ProgId\shell\open\command"
    $command = Get-DefaultValue -SubKey $commandKey
    if (-not (Test-PointsAtInstall -Command $command)) {
        Write-Host "The SandMarkDown.md file type does not point at this install. Leaving it in place."
        return
    }

    foreach ($extension in $Extensions) {
        $extensionKey = "Software\Classes\$extension"
        $current = Get-DefaultValue -SubKey $extensionKey
        if ($current -eq $ProgId) {
            Set-DefaultValue -SubKey $extensionKey -Value ""
        }

        $openWith = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("$extensionKey\OpenWithProgids", $true)
        if ($null -ne $openWith) {
            try {
                $openWith.DeleteValue($ProgId, $false)
            }
            finally {
                $openWith.Dispose()
            }
        }
    }

    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree("Software\Classes\$ProgId", $false)

    $appCommand = Get-DefaultValue -SubKey "Software\Classes\Applications\SandMarkDown.exe\shell\open\command"
    if (Test-PointsAtInstall -Command $appCommand) {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree("Software\Classes\Applications\SandMarkDown.exe", $false)
    }

    $appPath = Get-DefaultValue -SubKey "Software\Microsoft\Windows\CurrentVersion\App Paths\SandMarkDown.exe"
    if (Test-PointsAtInstall -Command $appPath) {
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree("Software\Microsoft\Windows\CurrentVersion\App Paths\SandMarkDown.exe", $false)
    }
}

function Stop-IfRunningFromInstall {
    $running = Get-Process -Name "SandMarkDown" -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and ($_.Path -like "$InstallDir*") }
    if ($running) {
        throw "SandMarkDown is open from the install folder. Close it, then run this script again."
    }
}

if ($Uninstall) {
    Stop-IfRunningFromInstall
    Unregister-FileType
    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
    }

    Update-Shell
    Write-Host "Removed $InstallDir"
    exit 0
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\SandMarkDown\SandMarkDown.csproj"
if (-not (Test-Path -LiteralPath $project)) {
    throw "Cannot find $project. Run this script from the SandMarkDown repo."
}

Stop-IfRunningFromInstall
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$rid = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "win-arm64" } else { "win-x64" }
Write-Host "Publishing SandMarkDown ($rid) to $InstallDir"
& dotnet publish $project -c Release -r $rid --self-contained false -o $InstallDir --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Publish did not produce $ExePath."
}

Register-FileType
Update-Shell

Write-Host "Installed $ExePath"
Write-Host "Double-click a .md or .markdown file to open it in SandMarkDown."
