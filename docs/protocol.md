# VibeDeck Protocol Notes

## Approval pairing (default)

Android native, BOOX, and browser/PWA clients (including iPhone Safari) use the approval flow on private LAN; QR pairing remains a fallback. There is no native iOS app.

- `POST /api/devices/pairing/request` — private-LAN client creates a 10-minute request and receives a private poll secret plus six-digit verification code.
- `POST /api/devices/pairing/pending` — PC-local console lists requests (action token required).
- `POST /api/devices/pairing/approve` or `/deny` — PC-local user decides (action token required).
- `POST /api/devices/pairing/poll` — requesting client exchanges its private secret for a persistent device token after approval.

The Host never sends an approved device token to the PC console; only the client holding the request secret can retrieve it. Requests are limited to private LAN addresses and expire automatically.

Last updated: 2026-07-04

This document records the current prototype protocol. Older experiments such as `/ws/video`, window capture, WebRTC DataChannel JPEG, and fragmented MP4 H.264 were useful for learning, but they are not the active product path.

The public product name is VibeDeck. Protocol examples still use `PhoneMonitor`, `phone-monitor`, and `phonemonitor://` where those strings are compatibility identifiers.

## Active Web Prototype

The web prototype is intentionally simple:

- The phone opens the Host web page.
- The page automatically selects the `PhoneMonitor` virtual display.
- iOS Safari prefers H.264 over WebRTC when FFmpeg is available.
- JPEG frames over WebSocket remain the browser fallback.
- Stream FPS and JPEG quality are user-adjustable.
- Virtual display resolution and refresh rate are user-adjustable.
- HTTPS is preferred so Screen Wake Lock can keep the phone awake.

## Display Stream

Endpoint:

```text
GET /ws/display?deviceName=<display>&fps=<fps>&quality=<jpeg-quality>
```

Current payload:

- WebSocket binary message.
- One complete JPEG frame per message.
- Intended for prototype display, debugging, and fallback use.

## WebRTC H.264 Display

Endpoint:

```text
POST /api/stream/webrtc/offer
```

The browser sends its gathered SDP offer together with `deviceName`, `fps`,
and `quality`. The Host answers with SDP and sends Annex-B H.264 access units
as RTP/SRTP. This lets Safari use its hardware H.264 decoder without an
installed native app. The route requires a paired device token (or a loopback
request) and an FFmpeg executable on the Host.

The stream should target the `PhoneMonitor` virtual display. Product UI should not ask normal users to choose arbitrary windows or debug sources.

## Input

Endpoint:

```text
GET /ws/input
```

Payload: UTF-8 JSON.

```json
{
  "type": "pointermove",
  "x": 0.5,
  "y": 0.5,
  "buttons": 1
}
```

Coordinates are normalized from `0.0` to `1.0` so the PC host can map them to the active virtual display resolution.

## Connect Info

Endpoint:

```text
GET /api/connect
```

Returns URLs and LAN addresses that the phone page can use for pairing/opening:

- HTTP URL.
- HTTPS URL.
- preferred URL.
- whether local HTTPS is configured.
- root and host certificate download URLs.
- whether Wake Lock needs HTTPS.

The QR endpoint points to the preferred phone URL. When the local HTTPS certificate is configured, this prefers port `5443`; otherwise it falls back to HTTP on port `5000`:

```text
GET /qr.svg
```

The phone can bootstrap local certificate trust from HTTP:

```text
GET /cert/phone-monitor-root.cer
GET /cert/phone-monitor-host.cer
```

The Host owns local certificate maintenance. On startup it creates missing certificate files and refreshes the Host certificate when the LAN IP list changes.

The native app QR endpoint points to the shared native deep link:

```text
GET /qr/native.svg?mode=display
GET /qr/native.svg?mode=sideboard
GET /qr/native.svg?mode=quota
```

The connect response includes native Android deep links (`phonemonitor://`). `IosApp*` fields remain as compatibility aliases of the same URLs but are unused: **iPhone is web/PWA only**.

```json
{
  "NativeAppUrl": "phonemonitor://open?host=http%3A%2F%2F192.168.1.20%3A5000%2Findex.html&mode=display",
  "NativeAppDisplayUrl": "phonemonitor://open?host=...&mode=display",
  "NativeAppSideboardUrl": "phonemonitor://open?host=...&mode=sideboard",
  "NativeAppQuotaUrl": "phonemonitor://open?host=...&mode=quota",
  "NativeAppCertificateUrl": "phonemonitor://open?host=...&mode=cert",
  "AndroidAppUrl": "phonemonitor://open?host=http%3A%2F%2F192.168.1.20%3A5000%2Findex.html&mode=display",
  "AndroidAppCertificateUrl": "phonemonitor://open?host=...&mode=cert"
}
```

The Android app stores the decoded `host` URL and opens the requested mode.
On Android, `mode=cert` starts the no-ADB local HTTPS trust flow.

## Installable App Shell

The Host also serves the phone app shell:

```text
GET /manifest.json
GET /service-worker.js
GET /offline.html
GET /icons/icon-192.png
GET /icons/icon-512.png
GET /icons/maskable-512.png
GET /icons/apple-touch-icon.png
```

Manifest shortcuts open the same app with mode query parameters:

```text
/index.html?mode=display
/index.html?mode=sideboard
/index.html?mode=quota
```

The JavaScript client treats these as initial mode requests and then continues using the normal Host API and WebSocket endpoints.

The Android native shell uses the same query parameters when native controls open a mode. iPhone uses `?mode=` on the Host web URL only.

## Future Native Stream Path

Android-only native display upgrades are documented in [product-vision.md](product-vision.md). iPhone stays on Safari WebRTC H.264 / JPEG.
