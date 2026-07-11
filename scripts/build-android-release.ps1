param(
    [switch]$InitSigningKey,
    [switch]$SkipHostCopy
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$androidRoot = Join-Path $root "apps\android"
$appBuildGradle = Join-Path $androidRoot "app\build.gradle"
$signingProperties = Join-Path $androidRoot "release-signing.properties"

& (Join-Path $PSScriptRoot "check-android-toolchain.ps1")

if (-not (Test-Path $signingProperties)) {
    if ($InitSigningKey) {
        & (Join-Path $PSScriptRoot "init-android-release-signing.ps1")
    } else {
        throw "Release signing is not configured. Run scripts\init-android-release-signing.ps1 once, then rerun this script."
    }
}

function Invoke-AndroidGradle {
    param([string[]]$Tasks)

    $gradlew = Join-Path $androidRoot "gradlew.bat"
    if (Test-Path $gradlew) {
        & $gradlew -p $androidRoot @Tasks
    } elseif ($env:GRADLE_HOME -and (Test-Path (Join-Path $env:GRADLE_HOME "bin\gradle.bat"))) {
        & (Join-Path $env:GRADLE_HOME "bin\gradle.bat") -p $androidRoot @Tasks
    } else {
        gradle -p $androidRoot @Tasks
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Gradle failed with exit code $LASTEXITCODE."
    }
}

function Read-GradleString {
    param(
        [string]$Text,
        [string]$Name,
        [string]$Fallback
    )

    $match = [regex]::Match($Text, "$Name\s+`"([^`"]+)`"")
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $Fallback
}

function Read-GradleInt {
    param(
        [string]$Text,
        [string]$Name,
        [int]$Fallback
    )

    $match = [regex]::Match($Text, "$Name\s+([0-9]+)")
    if ($match.Success) {
        return [int]$match.Groups[1].Value
    }

    return $Fallback
}

Invoke-AndroidGradle @("assembleRelease")

$releaseApk = Join-Path $androidRoot "app\build\outputs\apk\release\app-release.apk"
if (-not (Test-Path $releaseApk)) {
    throw "Signed release APK not found at $releaseApk. Check release signing configuration."
}

$gradleText = Get-Content -Path $appBuildGradle -Raw
$versionName = Read-GradleString -Text $gradleText -Name "versionName" -Fallback "0.0.0"
$versionCode = Read-GradleInt -Text $gradleText -Name "versionCode" -Fallback 1
$outputDirectory = Join-Path $root "outputs\android-release"
$versionedName = "vibedeck-android-$versionName-$versionCode.apk"
$versionedOutput = Join-Path $outputDirectory $versionedName

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Copy-Item -LiteralPath $releaseApk -Destination $versionedOutput -Force

$publicApk = $versionedOutput
if (-not $SkipHostCopy) {
    $downloadDirectory = Join-Path $root "src\PhoneMonitor.Host\wwwroot\downloads"
    New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
    $publicApk = Join-Path $downloadDirectory "vibedeck-android.apk"
    Copy-Item -LiteralPath $releaseApk -Destination $publicApk -Force
}

$sha256 = (Get-FileHash -Path $publicApk -Algorithm SHA256).Hash.ToLowerInvariant()
$sizeBytes = (Get-Item -LiteralPath $publicApk).Length
$builtAt = (Get-Date).ToUniversalTime().ToString("o")
$metadata = [ordered]@{
    fileName = "vibedeck-android.apk"
    versionName = $versionName
    versionCode = $versionCode
    sizeBytes = $sizeBytes
    sha256 = $sha256
    builtAt = $builtAt
}

if (-not $SkipHostCopy) {
    $metadataPath = Join-Path (Split-Path -Parent $publicApk) "vibedeck-android.json"
    $shaPath = Join-Path (Split-Path -Parent $publicApk) "vibedeck-android.apk.sha256"
    $metadata | ConvertTo-Json | Set-Content -Path $metadataPath -Encoding UTF8
    Set-Content -Path $shaPath -Value "$sha256  vibedeck-android.apk" -Encoding ASCII
}

$outputShaPath = Join-Path $outputDirectory "$versionedName.sha256"
Set-Content -Path $outputShaPath -Value "$sha256  $versionedName" -Encoding ASCII

Write-Host "Built signed VibeDeck Android APK:"
Write-Host "  $versionedOutput"
if (-not $SkipHostCopy) {
    Write-Host "Host download APK:"
    Write-Host "  $publicApk"
}
Write-Host "SHA256:"
Write-Host "  $sha256"
