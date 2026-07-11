$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$androidRoot = Join-Path $root "apps\android"

& (Join-Path $PSScriptRoot "check-android-toolchain.ps1")

$gradlew = Join-Path $androidRoot "gradlew.bat"
if (Test-Path $gradlew) {
    & $gradlew -p $androidRoot assembleDebug
} elseif ($env:GRADLE_HOME -and (Test-Path (Join-Path $env:GRADLE_HOME "bin\gradle.bat"))) {
    & (Join-Path $env:GRADLE_HOME "bin\gradle.bat") -p $androidRoot assembleDebug
} else {
    gradle -p $androidRoot assembleDebug
}
