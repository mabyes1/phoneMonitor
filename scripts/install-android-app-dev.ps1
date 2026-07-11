param(
    [string]$ApkPath = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $root "apps\android\app\build\outputs\apk\debug\app-debug.apk"
}

if (-not (Test-Path $ApkPath)) {
    throw "APK not found at $ApkPath. Run scripts\build-android-app.ps1 first."
}

& (Join-Path $PSScriptRoot "check-android-toolchain.ps1")

$adb = Join-Path $env:ANDROID_SDK_ROOT "platform-tools\adb.exe"
if (-not (Test-Path $adb)) {
    $adb = "adb"
}

$devices = & $adb devices
$onlineDevices = $devices | Select-String -Pattern "`tdevice$"
if (-not $onlineDevices) {
    Write-Host $devices
    throw "No Android device is ready. Enable USB debugging, connect the S23, and accept the phone trust prompt."
}

& $adb install -r $ApkPath
