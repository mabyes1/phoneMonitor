#Requires -Version 5.1
<#
.SYNOPSIS
  Install VibeDeck from a published payload without Inno Setup (dev / fallback path).

.DESCRIPTION
  Copies artifacts/windows-setup/payload to Program Files\VibeDeck, registers the
  VibeDeckHost Windows Service (auto-start), creates Start Menu + Desktop shortcuts
  that open the web UI, and starts the service.

.PARAMETER PayloadPath
  Path to published payload (default artifacts/windows-setup/payload).

.PARAMETER InstallDir
  Install directory (default Program Files\VibeDeck).

.PARAMETER SkipDesktopIcon
  Do not create a desktop shortcut.

.PARAMETER SkipService
  Copy files only; do not register the Windows Service.
#>
[CmdletBinding()]
param(
    [string]$PayloadPath,
    [string]$InstallDir,
    [switch]$SkipDesktopIcon,
    [switch]$SkipService
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw "Run this script in an elevated PowerShell (Administrator)."
}

if (-not $PayloadPath) {
    $PayloadPath = Join-Path $repoRoot "artifacts\windows-setup\payload"
}
if (-not $InstallDir) {
    $InstallDir = Join-Path ${env:ProgramFiles} "VibeDeck"
}

$hostExeName = "PhoneMonitor.Host.exe"
$serviceName = "VibeDeckHost"
$serviceDisplay = "VibeDeck Host"
$webUrl = "http://127.0.0.1:5000"

if (-not (Test-Path -LiteralPath (Join-Path $PayloadPath $hostExeName))) {
    throw "Payload missing $hostExeName. Run scripts\package-windows-setup.ps1 -SkipInno first. Path: $PayloadPath"
}

Write-Host "Installing VibeDeck → $InstallDir"

# Stop existing service if present
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -eq "Running") {
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }
    & sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $PayloadPath "*") -Destination $InstallDir -Recurse -Force

# Preserve existing browser/PWA pairings + HTTPS root from the old LocalAppData layout
# before the service mints a brand-new ProgramData tree.
$legacyData = Join-Path $env:LOCALAPPDATA "PhoneMonitor"
$productData = Join-Path $env:ProgramData "VibeDeck"
if (Test-Path -LiteralPath $legacyData) {
    Write-Host "Migrating legacy Host data: $legacyData → $productData"
    New-Item -ItemType Directory -Path $productData -Force | Out-Null
    foreach ($rel in @("devices", "certs", "quotas", "custom-sources", "windows-notifications")) {
        $src = Join-Path $legacyData $rel
        $dst = Join-Path $productData $rel
        if (-not (Test-Path -LiteralPath $src)) { continue }
        if (-not (Test-Path -LiteralPath $dst)) {
            Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
            continue
        }
        # Only fill missing leaf files so a re-install does not wipe live product data.
        Get-ChildItem -LiteralPath $src -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($src.Length).TrimStart('\')
            $target = Join-Path $dst $relative
            if (-not (Test-Path -LiteralPath $target)) {
                New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $target -Force
            }
        }
    }
}

$openCmd = Join-Path $InstallDir "Open-VibeDeck.cmd"
if (-not (Test-Path -LiteralPath $openCmd)) {
    $openCmdContent = @"
@echo off
sc query $serviceName | findstr /I "RUNNING" >nul || net start $serviceName >nul 2>&1
timeout /t 2 /nobreak >nul
start "" $webUrl
"@
    Set-Content -LiteralPath $openCmd -Value $openCmdContent -Encoding ASCII
}

$iconPath = Join-Path $InstallDir "vibedeck.ico"
$hostExe = Join-Path $InstallDir $hostExeName

# Shortcuts
$shell = New-Object -ComObject WScript.Shell
$startMenu = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\VibeDeck"
New-Item -ItemType Directory -Path $startMenu -Force | Out-Null

$openVbs = Join-Path $InstallDir "Open-VibeDeck.vbs"
$launchTarget = if (Test-Path -LiteralPath $openVbs) { $openVbs } else { $openCmd }

$startShortcut = $shell.CreateShortcut((Join-Path $startMenu "VibeDeck.lnk"))
$startShortcut.TargetPath = $launchTarget
$startShortcut.WorkingDirectory = $InstallDir
$startShortcut.WindowStyle = 7
$startShortcut.Description = "Open VibeDeck web UI on this PC"
if (Test-Path -LiteralPath $iconPath) {
    $startShortcut.IconLocation = "$iconPath,0"
}
$startShortcut.Save()

if (-not $SkipDesktopIcon) {
    $desktop = [Environment]::GetFolderPath("CommonDesktopDirectory")
    $deskShortcut = $shell.CreateShortcut((Join-Path $desktop "VibeDeck.lnk"))
    $deskShortcut.TargetPath = $launchTarget
    $deskShortcut.WorkingDirectory = $InstallDir
    $deskShortcut.WindowStyle = 7
    $deskShortcut.Description = "Open VibeDeck web UI on this PC"
    if (Test-Path -LiteralPath $iconPath) {
        $deskShortcut.IconLocation = "$iconPath,0"
    }
    $deskShortcut.Save()
}

if (-not $SkipService) {
    $binPath = "`"$hostExe`""
    & sc.exe create $serviceName binPath= $binPath DisplayName= $serviceDisplay start= auto obj= LocalSystem | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sc create failed with exit code $LASTEXITCODE"
    }
    & sc.exe description $serviceName "VibeDeck phone sideboard, AI quotas, and virtual display host. Opens on $webUrl." | Out-Null
    & sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    & netsh.exe advfirewall firewall delete rule name="VibeDeck Host" 2>$null | Out-Null
    & netsh.exe advfirewall firewall add rule name="VibeDeck Host" dir=in action=allow program="$hostExe" enable=yes profile=any | Out-Null
    Start-Service -Name $serviceName
    Write-Host "Service $serviceName started (Automatic)."
}

Write-Host ""
Write-Host "VibeDeck installed." -ForegroundColor Green
Write-Host "  Web UI:  $webUrl"
Write-Host "  Files:   $InstallDir"
Write-Host "  Data:    $env:ProgramData\VibeDeck"
Write-Host "  Service: $serviceName (Automatic)"
Write-Host ""
Start-Process $webUrl
