$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$setupHttps = Join-Path $PSScriptRoot "setup-https.ps1"

& $setupHttps -Quiet

Write-Host "PhoneMonitor Host"
Write-Host "HTTP : http://0.0.0.0:5000"
Write-Host "HTTPS: https://0.0.0.0:5443"
Write-Host ""
Write-Host "Open http://127.0.0.1:5000 on this PC, then scan the QR code with the phone."

dotnet run `
    --no-launch-profile `
    --project (Join-Path $root "src\PhoneMonitor.Host")
