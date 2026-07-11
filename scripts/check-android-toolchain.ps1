$ErrorActionPreference = "Stop"

$portableRoot = $env:PHONE_MONITOR_ANDROID_TOOLS
if ([string]::IsNullOrWhiteSpace($portableRoot)) {
    $portableRoot = "D:\DevTools\PhoneMonitorAndroid"
}

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    $portableJava = Get-ChildItem -Path $portableRoot -Recurse -Filter java.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\bin\\java\.exe$" } |
        Select-Object -First 1
    if ($portableJava) {
        $env:JAVA_HOME = Split-Path -Parent (Split-Path -Parent $portableJava.FullName)
        $env:Path = (Join-Path $env:JAVA_HOME "bin") + ";" + $env:Path
    }
}

if ([string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT) -and [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
    $portableSdk = Join-Path $portableRoot "android-sdk"
    if (Test-Path $portableSdk) {
        $env:ANDROID_SDK_ROOT = $portableSdk
        $env:ANDROID_HOME = $portableSdk
        $env:Path = (Join-Path $portableSdk "platform-tools") + ";" + $env:Path
    }
}

if (-not (Get-Command gradle -ErrorAction SilentlyContinue)) {
    $portableGradle = Get-ChildItem -Path $portableRoot -Recurse -Filter gradle.bat -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\bin\\gradle\.bat$" } |
        Select-Object -First 1
    if ($portableGradle) {
        $env:GRADLE_HOME = Split-Path -Parent (Split-Path -Parent $portableGradle.FullName)
        $env:Path = (Join-Path $env:GRADLE_HOME "bin") + ";" + $env:Path
    }
}

$missing = New-Object System.Collections.Generic.List[string]

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    $missing.Add("JDK 17+ (java was not found on PATH)")
}

$androidSdk = $env:ANDROID_SDK_ROOT
if ([string]::IsNullOrWhiteSpace($androidSdk)) {
    $androidSdk = $env:ANDROID_HOME
}

if ([string]::IsNullOrWhiteSpace($androidSdk) -or -not (Test-Path $androidSdk)) {
    $missing.Add("Android SDK (set ANDROID_SDK_ROOT or ANDROID_HOME)")
}

if (-not (Get-Command gradle -ErrorAction SilentlyContinue) -and -not (Test-Path (Join-Path $PSScriptRoot "..\apps\android\gradlew.bat"))) {
    $missing.Add("Gradle (or open apps\android in Android Studio)")
}

if ($missing.Count -gt 0) {
    Write-Host "Android toolchain is incomplete:" -ForegroundColor Yellow
    foreach ($item in $missing) {
        Write-Host " - $item" -ForegroundColor Yellow
    }
    throw "Android toolchain is incomplete. Run scripts\install-android-toolchain.ps1 first, or install Android Studio."
}

Write-Host "Android toolchain detected."
