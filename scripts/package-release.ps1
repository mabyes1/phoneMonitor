[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\PhoneMonitor.Host\PhoneMonitor.Host.csproj"
$solution = Join-Path $repoRoot "PhoneMonitor.sln"
$artifactRoot = Join-Path $repoRoot "artifacts\release"
$localDotNet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotNet) { $localDotNet } else { "dotnet" }

[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "PhoneMonitor.Host.csproj 必須設定 Version，才能建立可追蹤的發佈包。"
}

$releaseName = "VibeDeck-$version-$Runtime"
$publishRoot = Join-Path $artifactRoot $releaseName
$zipPath = Join-Path $artifactRoot "$releaseName.zip"
$checksumPath = "$zipPath.sha256"

function Remove-ReleasePathSafely([string]$path) {
    $resolved = [System.IO.Path]::GetFullPath($path)
    $allowedRoot = [System.IO.Path]::GetFullPath($artifactRoot).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒絕清除 artifacts/release 以外的路徑：$resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

if (-not $SkipTests) {
    Write-Host "[1/3] 執行 Release 測試"
    & $dotnet test $solution -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "測試失敗，停止建立發佈包。" }
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
Remove-ReleasePathSafely $publishRoot
Remove-ReleasePathSafely $zipPath
Remove-ReleasePathSafely $checksumPath

Write-Host "[2/3] 建立免安裝 .NET 的 $Runtime 版本"
& $dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失敗。" }

Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") -Destination $publishRoot
@"
VibeDeck $version

1. 雙擊 PhoneMonitor.Host.exe。
2. PC 開啟 http://127.0.0.1:5000。
3. 用頁面上的 QR Code 在手機開啟並完成配對。
4. 要使用延伸桌面時，在 PC 頁面按「建立虛擬螢幕」並接受 Windows 管理員確認。

這個版本已包含 .NET Runtime，目標電腦不需要安裝 .NET SDK。
建立虛擬螢幕時需要網路；第三方驅動資訊見 THIRD_PARTY_NOTICES.md。
Windows 通知轉送需要具封裝身分的 MSIX 版本；一般副螢幕、資訊板、額度與自訂卡片可直接使用本包。
"@ | Set-Content -LiteralPath (Join-Path $publishRoot "開始使用.txt") -Encoding UTF8

Write-Host "[3/3] 壓縮並產生 SHA-256"
Compress-Archive -LiteralPath $publishRoot -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $releaseName.zip" -Encoding ASCII

Write-Host "發佈包：$zipPath"
Write-Host "雜湊值：$checksumPath"
