param(
    [string]$InstallRoot = "D:\DevTools\PhoneMonitorAndroid",
    [string]$GradleVersion = "8.7"
)

$ErrorActionPreference = "Stop"

$jdkUrl = "https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jdk/hotspot/normal/eclipse"
$androidToolsUrl = "https://dl.google.com/android/repository/commandlinetools-win-14742923_latest.zip"
$gradleUrl = "https://services.gradle.org/distributions/gradle-$GradleVersion-bin.zip"

$downloads = Join-Path $InstallRoot "downloads"
$sdkRoot = Join-Path $InstallRoot "android-sdk"
$jdkHome = Join-Path $InstallRoot "jdk-17"
$gradleHome = Join-Path $InstallRoot "gradle-$GradleVersion"
$resolvedInstallRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')

New-Item -ItemType Directory -Force -Path $downloads, $sdkRoot | Out-Null

function Assert-InInstallRoot {
    param([string]$Path)

    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($resolved -ne $resolvedInstallRoot -and -not $resolved.StartsWith($resolvedInstallRoot + "\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside install root: $Path"
    }
}

function Download-File {
    param(
        [string]$Url,
        [string]$OutFile
    )

    if (Test-Path $OutFile) {
        Write-Host "Already downloaded: $OutFile"
        return
    }

    Write-Host "Downloading $Url"
    Invoke-WebRequest -Uri $Url -OutFile $OutFile
}

function Expand-ZipFresh {
    param(
        [string]$ZipPath,
        [string]$Destination
    )

    if (Test-Path $Destination) {
        Assert-InInstallRoot $Destination
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $Destination -Force
}

$jdkZip = Join-Path $downloads "temurin-jdk17-windows-x64.zip"
$cmdlineZip = Join-Path $downloads "commandlinetools-win-14742923_latest.zip"
$gradleZip = Join-Path $downloads "gradle-$GradleVersion-bin.zip"

Download-File $jdkUrl $jdkZip
Download-File $androidToolsUrl $cmdlineZip
Download-File $gradleUrl $gradleZip

if (-not (Test-Path (Join-Path $jdkHome "bin\java.exe"))) {
    $jdkTemp = Join-Path $InstallRoot "tmp-jdk"
    Expand-ZipFresh $jdkZip $jdkTemp
    $jdkExtracted = Get-ChildItem -Path $jdkTemp -Directory | Select-Object -First 1
    if (-not $jdkExtracted) {
        throw "Could not find extracted JDK directory."
    }

    if (Test-Path $jdkHome) {
        Assert-InInstallRoot $jdkHome
        Remove-Item -LiteralPath $jdkHome -Recurse -Force
    }

    Assert-InInstallRoot $jdkHome
    Move-Item -LiteralPath $jdkExtracted.FullName -Destination $jdkHome
    Assert-InInstallRoot $jdkTemp
    Remove-Item -LiteralPath $jdkTemp -Recurse -Force
}

if (-not (Test-Path (Join-Path $sdkRoot "cmdline-tools\latest\bin\sdkmanager.bat"))) {
    $cmdTemp = Join-Path $InstallRoot "tmp-cmdline"
    Expand-ZipFresh $cmdlineZip $cmdTemp
    $cmdlineSource = Join-Path $cmdTemp "cmdline-tools"
    if (-not (Test-Path $cmdlineSource)) {
        throw "Could not find cmdline-tools in Android command-line tools zip."
    }

    $cmdlineLatest = Join-Path $sdkRoot "cmdline-tools\latest"
    if (Test-Path $cmdlineLatest) {
        Assert-InInstallRoot $cmdlineLatest
        Remove-Item -LiteralPath $cmdlineLatest -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $cmdlineLatest) | Out-Null
    Assert-InInstallRoot $cmdlineLatest
    Move-Item -LiteralPath $cmdlineSource -Destination $cmdlineLatest
    Assert-InInstallRoot $cmdTemp
    Remove-Item -LiteralPath $cmdTemp -Recurse -Force
}

if (-not (Test-Path (Join-Path $gradleHome "bin\gradle.bat"))) {
    $gradleTemp = Join-Path $InstallRoot "tmp-gradle"
    Expand-ZipFresh $gradleZip $gradleTemp
    $gradleExtracted = Get-ChildItem -Path $gradleTemp -Directory | Select-Object -First 1
    if (-not $gradleExtracted) {
        throw "Could not find extracted Gradle directory."
    }

    if (Test-Path $gradleHome) {
        Assert-InInstallRoot $gradleHome
        Remove-Item -LiteralPath $gradleHome -Recurse -Force
    }

    Assert-InInstallRoot $gradleHome
    Move-Item -LiteralPath $gradleExtracted.FullName -Destination $gradleHome
    Assert-InInstallRoot $gradleTemp
    Remove-Item -LiteralPath $gradleTemp -Recurse -Force
}

$env:JAVA_HOME = $jdkHome
$env:ANDROID_SDK_ROOT = $sdkRoot
$env:ANDROID_HOME = $sdkRoot
$env:GRADLE_HOME = $gradleHome
$env:Path = (Join-Path $jdkHome "bin") + ";" +
    (Join-Path $sdkRoot "cmdline-tools\latest\bin") + ";" +
    (Join-Path $sdkRoot "platform-tools") + ";" +
    (Join-Path $gradleHome "bin") + ";" +
    $env:Path

$sdkManager = Join-Path $sdkRoot "cmdline-tools\latest\bin\sdkmanager.bat"
Write-Host "Accepting Android SDK licenses..."
cmd /c "for /l %i in (1,1,80) do @echo y" | & $sdkManager --sdk_root=$sdkRoot --licenses

Write-Host "Installing Android SDK packages..."
cmd /c "for /l %i in (1,1,80) do @echo y" | & $sdkManager --sdk_root=$sdkRoot "platform-tools" "platforms;android-35" "build-tools;34.0.0" "build-tools;35.0.0"

Write-Host "Android toolchain installed under $InstallRoot"
Write-Host "For this PowerShell session:"
Write-Host "  `$env:PHONE_MONITOR_ANDROID_TOOLS = `"$InstallRoot`""
