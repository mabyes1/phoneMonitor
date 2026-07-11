# Phone App Layer

Last updated: 2026-07-11

## Platform decision

| Platform | Client | Notes |
|----------|--------|--------|
| **iPhone / iOS** | Safari or Add to Home Screen PWA only | WebRTC H.264 + JPEG fallback. **No native iOS app**, no TestFlight, no Xcode project. |
| **Android** | PWA and/or native shell under `apps/android` | Native app adds keep-awake, deep links, MediaCodec H.264, Deck launch. |
| **BOOX / e-ink** | Browser or Android path | Same Host protocol. |

## Current App: PWA (all phones, required path for iPhone)

The Host-served phone UI is installable as a Progressive Web App:

- `manifest.json` exposes the app name, theme, icons, and shortcuts.
- `service-worker.js` caches the app shell, sideboard skins, manifest, and icons.
- `offline.html` is shown when the app shell is installed but the PC host is unreachable.
- iPhone: Safari → Share → Add to Home Screen (no App Store binary).

This gives a home-screen entry without an iOS build pipeline.

The Host can also serve local HTTPS on port `5443` after it automatically creates the PhoneMonitor certificate files. The HTTP page remains the bootstrap entry because phones must download and trust `phone-monitor-root.cer` before the HTTPS URL can open cleanly.

### iPhone setup (canonical)

1. Open Host HTTP URL → download/trust `phone-monitor-root.cer`.
2. Open Host HTTPS URL → pair with the PC.
3. Share → Add to Home Screen.
4. Use Display with WebRTC H.264 (JPEG if WebRTC/FFmpeg unavailable).
5. Turn on keep-awake when needed (HTTPS + user gesture preferred).

## Native App Track (Android only)

Native clients wrap the Host protocol rather than forking the product UI.

The Android client lives at `apps/android`. There is no `apps/ios`.

Current Android scope:

- Native Host URL entry with persistence.
- LAN Host discovery through the `Find` button, using `/health`.
- Host-generated deep links that open the Android app with the current Host URL (`phonemonitor://`).
- Native Display, Sideboard, and Quotas buttons.
- Android launcher shortcuts for Display, Sideboard, and Quotas.
- WebView with JavaScript and DOM storage enabled for the existing Host phone UI.
- `FLAG_KEEP_SCREEN_ON`, so the Android app is not blocked by browser Wake Lock limitations.
- Cleartext HTTP permitted for LAN development Host URLs.
- User-installed CA certificates trusted by the app WebView, so local HTTPS works after `phone-monitor-root.cer` is installed on Android.
- `Trust` action and `mode=cert` deep link to download the Host root certificate and open Android security settings without ADB.
- Native Annex-B H.264 viewer for Display / Deck Window modes.

Zero-install iPhone note:

- Safari should use the HTTPS URL after the local root certificate is trusted.
- If the certificate is not trusted yet, HTTP still works as a bootstrap and fallback path, but the user may need to set Auto-Lock to Never while testing.
- Keep-awake on the web path uses Screen Wake Lock and/or a silent media loop after a user gesture.
