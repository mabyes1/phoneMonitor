# Product Architecture

Last updated: 2026-07-04

VibeDeck is a Windows virtual display foundation plus a phone-optimized smart sideboard. `PhoneMonitor` remains the internal compatibility name where the current driver, Host namespace, package IDs, and protocol fields already depend on it.

The product direction is documented in [product-vision.md](product-vision.md). This file describes the current architecture shape.

## Current Web Prototype Loop

```text
Windows Indirect Display Driver
        |
        v
PhoneMonitor virtual display appears in Windows
        |
        v
Host captures that virtual display
        |
        v
Host streams JPEG frames over WebSocket
        |
        v
Phone browser renders the display page
```

This path is good for proving the virtual monitor, phone layout, resolution controls, wake-lock flow, and the smart sideboard concept without requiring users to install a phone app.

## Product Modes

### Native Display Mode

Windows sees `PhoneMonitor Display` as a real extended monitor. PC mouse and keyboard remain native.

The current web prototype can show this display with JPEG streaming. A future native-client track may replace this with hardware H.264 over low-latency UDP if the display mode becomes a major product feature.

### Smart Sideboard Mode

The phone shows phone-first panels instead of a tiny desktop.

PhoneMonitor Host owns the base telemetry collector under `src/PhoneMonitor.Host/Sideboard`, so the phone UI does not depend on a separate `glance-board` server. The original `glance-board` can remain an optional rich PC dashboard and work-pulse source.

Target panel ideas:

- AI usage, quota, and cooldown status.
- Codex or AI task progress.
- Quick controls and local shortcuts.
- PC or project dashboards.
- Ambient pet/companion experience.

Smart Sideboard Mode is the main product wedge. It is where PhoneMonitor can become more useful than a generic virtual monitor on a phone-sized screen.

### Phone App Layer

The current phone app layer has two tracks:

```text
Host static web app
        |
        v
manifest.json + service-worker.js
        |
        v
Phone home-screen app entry
```

The PWA keeps the zero-install URL flow while adding a real phone app entry, cached shell assets, app icons, and shortcuts for Display, Sideboard, and Quotas.

```text
iPhone / Android / BOOX
        |
        v
Browser or Home Screen PWA
        |
        v
Same Host phone UI (WebRTC H.264 + JPEG fallback)
```

All phone platforms use the web/PWA path. There are no native phone binaries in the supported product flow.

## Deliberately Deprioritized

These were useful prototype experiments, but should not drive the product UI right now:

- Existing-window capture.
- Test-pattern streaming.
- WebRTC DataChannel JPEG.
- H.264 fragmented MP4 in the browser.
- ADB as a normal user setup path.
- Generic fit/fill/crop/stretch controls.

## Next Architecture Step

Harden the Host-to-phone panel API so built-in telemetry, AI quotas, and optional workflow panels can live beside the virtual display stream.
Keep polishing the cross-platform browser/PWA path and its WebRTC H.264 fallback behavior.
