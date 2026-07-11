$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$inf = Join-Path $root "driver\x64\Debug\PhoneMonitor.Idd\PhoneMonitor.Idd.inf"
$cer = Join-Path $root "driver\x64\Debug\PhoneMonitor.Idd.cer"
$devcon = "C:\Program Files (x86)\Windows Kits\10\Tools\10.0.26100.0\x64\devcon.exe"
$log = Join-Path $root "driver\install-driver-dev.log"
Start-Transcript -Path $log -Force | Out-Null

if (-not (Test-Path $inf)) {
    throw "Built INF not found at $inf. Run scripts\build-driver.ps1 first."
}

if (-not (Test-Path $cer)) {
    throw "Test certificate not found at $cer. Run scripts\build-driver.ps1 first."
}

if (-not (Test-Path $devcon)) {
    throw "devcon.exe not found at $devcon. Install WDK 10.0.26100 first."
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "This script must be run from an elevated Administrator PowerShell."
}

Write-Host "Trusting WDK test certificate."
certutil -addstore Root $cer
certutil -addstore TrustedPublisher $cer

Write-Host "Enabling test signing. A reboot is required before Windows can load test-signed drivers."
bcdedit /set testsigning on

Write-Host "Staging PhoneMonitor indirect display driver package."
pnputil /add-driver $inf /install

$devices = pnputil /enum-devices /class Display
if ($devices -match "PhoneMonitor Display") {
    Write-Host "Updating existing PhoneMonitor display device."
    & $devcon update $inf Root\PhoneMonitorIdd
    & $devcon restart Root\PhoneMonitorIdd
} else {
    Write-Host "Creating Root\\PhoneMonitorIdd device with devcon."
    & $devcon install $inf Root\PhoneMonitorIdd
}

Write-Host "Reboot, then check Windows Settings > System > Display for PhoneMonitor Display."
Stop-Transcript | Out-Null
