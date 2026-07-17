#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Source,
    [switch]$Payload,
    [switch]$Installed,
    [string]$PayloadPath,
    [switch]$RequireVirtualDisplay
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "PhoneMonitor.sln"
$project = Join-Path $repoRoot "src\PhoneMonitor.Host\PhoneMonitor.Host.csproj"

if (-not ($Source -or $Payload -or $Installed)) {
    $Source = $true
}

function Assert-Product([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Write-Check([string]$message) {
    Write-Host "[product-check] $message" -ForegroundColor Cyan
}

if ($Source) {
    Write-Check "Release tests"
    & dotnet test $solution -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed." }

    Write-Check "Browser JavaScript syntax"
    $node = Get-Command node -ErrorAction Stop
    Get-ChildItem (Join-Path $repoRoot "src\PhoneMonitor.Host\wwwroot") -Recurse -Filter "*.js" -File |
        ForEach-Object {
            & $node.Source --check $_.FullName
            if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax check failed: $($_.FullName)" }
        }

    Write-Check "Product PowerShell syntax"
    foreach ($relative in @(
        "scripts\install-windows-product.ps1",
        "scripts\build-and-install-windows.ps1",
        "scripts\uninstall-windows-product.ps1",
        "scripts\package-windows-setup.ps1",
        "scripts\package-windows-notifications.ps1",
        "scripts\test-product-flow.ps1"
    )) {
        $path = Join-Path $repoRoot $relative
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
        if ($errors) { throw "PowerShell syntax check failed: $relative`n$($errors -join "`n")" }
    }

    Write-Check "Canonical product path"
    Assert-Product (-not (Test-Path (Join-Path $repoRoot "apps\android"))) "Deprecated apps/android still exists. Phone clients must remain browser/PWA-only."
    Assert-Product (-not (Test-Path (Join-Path $repoRoot "scripts\package-release.ps1"))) "Deprecated portable ZIP release script still exists. Setup is the only product release path."
    Assert-Product (Test-Path (Join-Path $repoRoot "install.bat")) "One-click install entry is missing."
    Assert-Product (Test-Path (Join-Path $repoRoot "update.bat")) "One-click update entry is missing."
    Assert-Product (-not (Get-ChildItem (Join-Path $repoRoot "src\PhoneMonitor.Host\wwwroot") -Recurse -File -Include "*.apk","*.ipa" -ErrorAction SilentlyContinue)) "Native mobile package found under wwwroot."
    $webSource = Get-ChildItem (Join-Path $repoRoot "src\PhoneMonitor.Host\wwwroot") -Recurse -File -Include "*.js","*.css","*.html" |
        ForEach-Object { Get-Content $_.FullName -Raw }
    Assert-Product (($webSource -join "`n") -notmatch "PhoneMonitorShell|native-shell") "Deprecated native-shell branch still exists in the browser/PWA client."
    $projectText = Get-Content $project -Raw
    Assert-Product ($projectText -notmatch "WindowsServices|Logging\.EventLog") "Host still references Windows Service packages."
    $readme = Get-Content (Join-Path $repoRoot "README.md") -Raw
    Assert-Product ($readme -notmatch "apps/android|package-release\.ps1") "README still points to a deprecated product path."
}

if ($Payload) {
    if (-not $PayloadPath) { $PayloadPath = Join-Path $repoRoot "artifacts\windows-setup\payload" }
    $PayloadPath = [IO.Path]::GetFullPath($PayloadPath)
    Write-Check "Published payload at $PayloadPath"
    foreach ($relative in @(
        "VibeDeck.Host.exe",
        "product-install.json",
        "vibedeck.ico",
        "wwwroot\index.html",
        "wwwroot\index.js",
        "Installers\install-virtual-display.ps1"
    )) {
        Assert-Product (Test-Path -LiteralPath (Join-Path $PayloadPath $relative)) "Payload is missing $relative."
    }
    foreach ($legacyLauncher in @("Open-VibeDeck.cmd", "Open-VibeDeck.vbs", "Start-VibeDeck-Host.vbs")) {
        Assert-Product (-not (Test-Path -LiteralPath (Join-Path $PayloadPath $legacyLauncher))) "Payload still contains legacy launcher: $legacyLauncher"
    }
    $projectText = Get-Content $project -Raw
    Assert-Product ($projectText -match "<OutputType>WinExe</OutputType>") "Host must be a native Windows background application."
    Assert-Product (-not (Get-ChildItem (Join-Path $PayloadPath "wwwroot") -Recurse -File -Include "*.apk","*.ipa" -ErrorAction SilentlyContinue)) "Payload contains a native mobile package."
}

if ($Installed) {
    Write-Check "Live installed product"
    Assert-Product (-not (Get-Service "VibeDeckHost" -ErrorAction SilentlyContinue)) "Legacy VibeDeckHost Windows Service is still registered."
    $run = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "VibeDeckHost" -ErrorAction SilentlyContinue).VibeDeckHost
    if ([string]::IsNullOrWhiteSpace($run)) {
        $run = (Get-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "VibeDeckHost" -ErrorAction SilentlyContinue).VibeDeckHost
    }
    Assert-Product (-not [string]::IsNullOrWhiteSpace($run)) "VibeDeckHost sign-in auto-start is missing."

    $listener = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    Assert-Product ($null -ne $listener) "Installed Host is not listening on port 5000."
    $hostProcess = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
    Assert-Product ($hostProcess.SessionId -gt 0) "Installed Host is running in Session 0; virtual display capture will fail."

    $displays = Invoke-RestMethod "http://127.0.0.1:5000/api/displays" -TimeoutSec 8
    Assert-Product (-not ($displays | Where-Object DeviceName -eq "WinDisc")) "Host is enumerating the Session 0 WinDisc display."
    if ($RequireVirtualDisplay) {
        Assert-Product ($null -ne ($displays | Where-Object IsPhoneMonitor | Select-Object -First 1)) "PhoneMonitor virtual display was not found."
    }
}

Write-Host "Product flow checks passed." -ForegroundColor Green
