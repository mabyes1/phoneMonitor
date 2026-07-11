# VibeDeck Android App

This is the native Android shell for VibeDeck. It wraps the Host-served phone UI in a WebView and adds Android-native affordances that the browser path cannot reliably provide.

The package name and deep link scheme still use `com.phonemonitor.app` and `phonemonitor://` for compatibility.

## Features

- Enter or reuse a Host URL such as `http://192.168.1.20:5000/index.html`.
- Tap `找 Host` to scan the current LAN for a VibeDeck Host at `/health`.
- Open from the Host phone page through `phonemonitor://open?host=...&mode=...` deep links.
- Open `顯示器`, `資訊板`, and `額度` with native buttons and launcher shortcuts.
- Use `顯示器` to open the native hardware H.264 decoder screen after the phone is paired.
- Use `資訊板` or `額度` to ask the PC Host to open a Deck Window on the virtual display, then switch into the same native H.264 viewer.
- Use `Web 顯示` as the JPEG/Web fallback.
- Use `憑證` to download the Host root certificate and jump to Android security settings.
- Keep the phone screen awake with Android window flags.
- Show an `更新 App` prompt when the connected Host serves a signed APK with a newer `versionCode`.
- Trust user-installed local CA certificates so the WebView can open the Host's local HTTPS URL after `phone-monitor-root.cer` is installed.

## Native H.264 Display

The native viewer is the app-side decoder path. It connects to:

```text
ws://<host>:5000/ws/h264-annexb?fps=60&quality=58
```

It parses SPS/PPS, configures Android `MediaCodec` for `video/avc`, renders to a `SurfaceView`, and sends basic touch input to:

```text
ws://<host>:5000/ws/input
```

Single tap maps to left click, drag maps to mouse drag, and long press maps to right click.

The Host reports this route through `/api/stream/capabilities`. When FFmpeg is available on the PC, the route streams Annex-B H.264 encoded with `ffmpeg/libx264`.

Pair once from the Host page so the WebView can sync the `deviceToken` into the Android shell. The native H.264 viewer sends that token on both video and input sockets. Deck launch commands also use this token, so an unpaired phone cannot open PC windows.

The viewer status line reports live H.264 FPS, Mbps, and dropped frames. Tap the top edge to show the controls/status overlay again after it auto-hides.

The H.264 button in the native viewer cycles through:

- `省電`: 30fps / Q46
- `平衡`: 45fps / Q54
- `順暢`: 60fps / Q58

Changing the preset reconnects the native video socket and stores the choice for the next launch.

## Discovery

`找 Host` scans the current Host URL subnet and the Wi-Fi gateway subnet for `http://<candidate>:5000/health`. A match must return a body containing `PhoneMonitor.Host`; that string is an internal Host compatibility identifier.

Manual Host URL entry and Host-generated deep links remain the reliable fallback.

## Local HTTPS

For normal Android app testing, HTTP is usable because the app keeps the screen awake natively. HTTPS is still useful for testing the browser/PWA path or secure pairing over LAN.

The Host automatically creates and refreshes local HTTPS certificates under:

```text
%LOCALAPPDATA%\PhoneMonitor\certs
```

No ADB is required for the product path. Open the Host over HTTP, launch the Android app from `開啟手機 App`, then tap `憑證`. The app downloads `phone-monitor-root.cer` into Android Downloads and opens security settings. Android still requires the user to install the downloaded file as a CA certificate from system UI.

After Android trusts the root certificate, open:

```text
https://<pc-lan-ip>:5443/index.html
```

## Build And Install

Install Android Studio, or use the repo's portable Windows toolchain installer:

```powershell
scripts\install-android-toolchain.ps1
```

The installer defaults to:

```text
D:\DevTools\PhoneMonitorAndroid
```

Manual toolchain requirements:

- JDK 17 or newer.
- Android SDK with platform 35.
- Gradle, or Android Studio project sync.

Build from the repo root:

```powershell
scripts\check-android-toolchain.ps1
scripts\build-android-app.ps1
```

Install with a USB-connected Android phone:

```powershell
scripts\install-android-app-dev.ps1
```

The script uses `adb install -r`, so it keeps existing app data.

## Self-Distributed Release APK

VibeDeck can be distributed without Google Play by publishing a signed APK from GitHub Releases or from the Host download page.

Create a local release signing key once:

```powershell
scripts\init-android-release-signing.ps1
```

Build a signed release APK and copy it into the Host download area:

```powershell
scripts\build-android-release.ps1
```

The script writes:

```text
outputs\android-release\vibedeck-android-<version>-<code>.apk
src\PhoneMonitor.Host\wwwroot\downloads\vibedeck-android.apk
src\PhoneMonitor.Host\wwwroot\downloads\vibedeck-android.apk.sha256
```

The Host then exposes:

```text
http://<pc-lan-ip>:5000/download/vibedeck-android.apk
http://<pc-lan-ip>:5000/install/android
http://<pc-lan-ip>:5000/qr/apk.svg
```

When the app connects to a Host, it checks `/api/android/release`. If the Host has a newer signed APK, the app shows `更新 App` and opens the Host install page.

Back up the generated `.jks` file and `apps\android\release-signing.properties`. Android requires updates to be signed with the same key.
