param(
    [string]$KeystorePath = "",
    [string]$Alias = "vibedeck",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$androidRoot = Join-Path $root "apps\android"
$propertiesPath = Join-Path $androidRoot "release-signing.properties"

& (Join-Path $PSScriptRoot "check-android-toolchain.ps1")

if ((Test-Path $propertiesPath) -and -not $Force) {
    Write-Host "Release signing is already configured at $propertiesPath"
    return
}

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $signingRoot = Join-Path $env:LOCALAPPDATA "VibeDeck\signing"
    $KeystorePath = Join-Path $signingRoot "vibedeck-release.jks"
}

if ((Test-Path $KeystorePath) -and -not $Force) {
    throw "Keystore already exists at $KeystorePath, but $propertiesPath is missing. Back it up and pass -Force only if you want to create a new key."
}

function New-SigningPassword {
    $bytes = New-Object byte[] 24
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    } finally {
        $rng.Dispose()
    }

    return [Convert]::ToBase64String($bytes).TrimEnd("=")
}

function Resolve-KeyTool {
    if ($env:JAVA_HOME) {
        $candidate = Join-Path $env:JAVA_HOME "bin\keytool.exe"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $command = Get-Command keytool -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "keytool was not found. Run scripts\install-android-toolchain.ps1 or install JDK 17+."
}

$keystoreDirectory = Split-Path -Parent $KeystorePath
New-Item -ItemType Directory -Path $keystoreDirectory -Force | Out-Null

$storePassword = New-SigningPassword
$keyPassword = $storePassword
$keytool = Resolve-KeyTool

if (Test-Path $KeystorePath) {
    Remove-Item -LiteralPath $KeystorePath -Force
}

& $keytool `
    -genkeypair `
    -v `
    -keystore $KeystorePath `
    -storepass $storePassword `
    -alias $Alias `
    -keyalg RSA `
    -keysize 4096 `
    -validity 10000 `
    -dname "CN=VibeDeck Local Release,O=VibeDeck,C=TW"

$gradleKeystorePath = $KeystorePath.Replace("\", "/")
$properties = @(
    "# Local VibeDeck Android release signing. Do not commit this file.",
    "storeFile=$gradleKeystorePath",
    "storePassword=$storePassword",
    "keyAlias=$Alias",
    "keyPassword=$keyPassword"
)
Set-Content -Path $propertiesPath -Value $properties -Encoding ASCII

Write-Host "Created VibeDeck Android release key:"
Write-Host "  $KeystorePath"
Write-Host "Created ignored Gradle signing config:"
Write-Host "  $propertiesPath"
Write-Host "Back up the .jks and password file. Losing them means users cannot update over the old APK."
