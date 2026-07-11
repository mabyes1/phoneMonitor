# Android APK Distribution

VibeDeck can ship Android builds without Google Play by publishing a signed APK through GitHub Releases and by serving the latest APK from the local Host.

## One-Time Signing Setup

Run this once on the release machine:

```powershell
scripts\init-android-release-signing.ps1
```

This creates:

```text
%LOCALAPPDATA%\VibeDeck\signing\vibedeck-release.jks
apps\android\release-signing.properties
```

Both files are intentionally ignored by git. Back them up somewhere private. Android will only accept app updates signed by the same key.

## Build A Release APK

```powershell
scripts\build-android-release.ps1
```

The script builds and signs:

```text
outputs\android-release\vibedeck-android-<version>-<code>.apk
outputs\android-release\vibedeck-android-<version>-<code>.apk.sha256
```

It also copies the latest build into the Host download area:

```text
src\PhoneMonitor.Host\wwwroot\downloads\vibedeck-android.apk
src\PhoneMonitor.Host\wwwroot\downloads\vibedeck-android.apk.sha256
src\PhoneMonitor.Host\wwwroot\downloads\vibedeck-android.json
```

## Host Download URLs

After restarting the Host, phones can download the latest APK from:

```text
http://<pc-lan-ip>:5000/download/vibedeck-android.apk
```

The QR endpoint is:

```text
http://<pc-lan-ip>:5000/qr/apk.svg
```

The install page is:

```text
http://<pc-lan-ip>:5000/install/android
```

The Android release metadata endpoint is:

```text
http://<pc-lan-ip>:5000/api/android/release
```

The main VibeDeck page shows the Android APK button only when `vibedeck-android.apk` exists. The Android app checks the metadata endpoint after connecting to a Host and shows `更新 App` only when the Host's `versionCode` is newer than the installed app.

## GitHub Release Upload

For a manual GitHub Release:

1. Build the signed APK.
2. Create a GitHub Release tag such as `android-v0.1.0`.
3. Upload these files:
   - `outputs\android-release\vibedeck-android-<version>-<code>.apk`
   - `outputs\android-release\vibedeck-android-<version>-<code>.apk.sha256`
4. Put the SHA256 in the release notes.

Do not upload the `.jks` keystore or `release-signing.properties`.
