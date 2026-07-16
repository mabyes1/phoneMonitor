#Requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$InstallDir)

$ErrorActionPreference = "Stop"
$resolvedInstall = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\') + '\'
foreach ($name in @("VibeDeck.Host.exe", "PhoneMonitor.Host.exe")) {
    Get-CimInstance Win32_Process -Filter "Name='$name'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ExecutablePath -and
            [IO.Path]::GetFullPath($_.ExecutablePath).StartsWith($resolvedInstall, [StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

$deadline = [DateTime]::UtcNow.AddSeconds(8)
do {
    $listener = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $listener) { exit 0 }
    Start-Sleep -Milliseconds 250
} while ([DateTime]::UtcNow -lt $deadline)

throw "VibeDeck Host did not release port 5000 before the update."
