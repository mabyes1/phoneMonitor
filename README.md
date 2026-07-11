# VibeDeck

VibeDeck turns an idle phone into a PC side display and command dashboard.

The product name is **VibeDeck**. Some internal names still say `PhoneMonitor` because the Windows driver, local certificate paths, HTTP headers, deep links, and package IDs already depend on those identifiers. Treat `PhoneMonitor` as the compatibility layer and VibeDeck as the user-facing product.

## What Works Now

- Windows Host serving the phone UI from `src/PhoneMonitor.Host`.
- Real Windows virtual display through the PhoneMonitor Indirect Display Driver work under `driver/PhoneMonitor.Idd`.
- Browser/PWA phone client with QR connect, pairing, HTTPS certificate download, wake-lock support, and installable app shell.
- JPEG WebSocket display stream as the current reliable fallback.
- **iPhone / iOS: Safari (or Add to Home Screen PWA) only** — WebRTC H.264 when FFmpeg is available, JPEG fallback otherwise. No native iOS app.
- FFmpeg/libx264 Annex-B H.264 Host stream at `/ws/h264-annexb` for the Android native decoder, with idle-frame throttling and Host-side metrics for fps/Mbps/skipped frames.
- Phone-first Sideboard mode with Host-owned PC telemetry, Chinese weather/location, AI quota panel, and glance-board-inspired skins.
- PC Deck Window launcher that opens the Sideboard or Quota page on the `PhoneMonitor Display`, giving the installed Android app a native H.264 view of a real PC-controlled page.
- Native Android WebView shell under `apps/android`, with VibeDeck branding, launcher icon, LAN discovery, deep links, native keep-awake, paired-device token handoff, APK update prompts, native Deck launch commands, and a native H.264 decoder screen.
- Independent AI quota page for Codex, Claude Code discovery, and AGY account/OAuth flow.

Intentionally out of the current product UI:

- Existing-window capture.
- Test-pattern/debug stream sources.
- WebRTC DataChannel JPEG.
- Fragmented-MP4 H.264 browser experiment.
- ADB as a user-facing setup path.
- Fit/fill/crop/stretch source controls.

## License

MIT. See [LICENSE](LICENSE).

## Security notes (LAN product)

- The Host listens on the LAN (`0.0.0.0:5000` / HTTPS `:5443`) by design so phones can connect.
- Pairing, device tokens, and action tokens are the access control for non-loopback clients.
- Do not expose the Host port on the public internet.
- AGY Google OAuth credentials must be supplied by you (env or local secrets file). They are never committed.

## Run The Host

From the repo root:

```powershell
scripts\dev-run.ps1
```

Or build and run the Host executable directly:

```powershell
dotnet build PhoneMonitor.sln
src\PhoneMonitor.Host\bin\Debug\netcoreapp3.1\PhoneMonitor.Host.exe --urls http://0.0.0.0:5000
```

Useful URLs:

- PC local: `http://127.0.0.1:5000`
- Phone LAN: `http://<PC-LAN-IP>:5000`
- HTTPS LAN: `https://<PC-LAN-IP>:5443` after the local certificate is trusted.

Useful checks:

```powershell
Invoke-RestMethod http://127.0.0.1:5000/health
Invoke-RestMethod http://127.0.0.1:5000/api/connect | ConvertTo-Json -Depth 4
Invoke-RestMethod http://127.0.0.1:5000/api/stream/capabilities | ConvertTo-Json -Depth 4
Invoke-RestMethod http://127.0.0.1:5000/api/displays | ConvertTo-Json -Depth 3
Invoke-RestMethod http://127.0.0.1:5000/api/sideboard/stats | ConvertTo-Json -Depth 6
Invoke-RestMethod http://127.0.0.1:5000/api/quotas | ConvertTo-Json -Depth 6
```

## Phone Setup

### Browser / PWA (including all iPhones)

Open the Host URL on the phone and use the `手機連線` panel.

- **iPhone**: Safari is the supported client. Prefer HTTPS after trusting the local root certificate, pair once, then Share → Add to Home Screen. WebRTC H.264 is the primary display path; JPEG is the fallback. There is **no** VibeDeck iOS App Store / TestFlight / Xcode project.
- Android Chrome/Edge can install the PWA when the browser allows it.
- HTTPS is preferred for browser wake lock. Phones still require installing/trusting the local root certificate from system UI.

### Android Native App

Build and install:

```powershell
scripts\check-android-toolchain.ps1
scripts\build-android-app.ps1
scripts\install-android-app-dev.ps1
```

The debug APK is here:

```text
apps\android\app\build\outputs\apk\debug\app-debug.apk
```

The installed app keeps the screen awake natively, can scan the LAN for the Host, opens `phonemonitor://` deep links from the Host page, and includes a native H.264 decoder screen. Pair from the Host page once so the app shell can receive the device token; after that `顯示器` opens the native H.264 viewer, while `資訊板` and `額度` ask the PC Host to open a Deck Window on the virtual display and then switch into native H.264.

## Driver Development

The Windows driver needs Visual Studio C++ and WDK.

```powershell
scripts\check-driver-toolchain.ps1
scripts\install-driver-toolchain.ps1
scripts\fetch-idd-sample.ps1
scripts\build-driver.ps1
scripts\install-driver-dev.ps1
```

After install, Windows should expose `PhoneMonitor Display` in Settings. That display name is still internal/driver-facing.

## Product Direction

VibeDeck is not trying to be only a spacedesk clone. The virtual display is the base layer; the sharper wedge is a phone-sized command deck:

1. **Display**: show the real Windows virtual display when needed.
2. **Deck Window**: open a PC-rendered Sideboard or Quota page directly on the virtual display.
3. **Sideboard**: readable PC telemetry, weather, work pulse, quick glance panels.
4. **AI Quotas**: account-aware quota page for coding/AI tools.
5. **Android native app** (optional): keep-awake, deep links, native H.264; **iPhone stays web/PWA**.

Near-term focus:

- Keep JPEG as the reliable display fallback.
- Keep iPhone on Safari/PWA WebRTC H.264; use the Android app only where native H.264 / Deck Window is worth it.
- Replace or tune the current FFmpeg/libx264 process with a Windows GPU encoder once latency/CPU profiling says it is worth it.
- Polish pairing/onboarding so normal users do not need ADB.
- Continue migrating user-facing language from PhoneMonitor to VibeDeck while preserving internal compatibility.
