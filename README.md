# VibeDeck

**Turn any phone into a trusted extension of your Windows PC—an extra display, a system dashboard, or an AI‑quota companion.**

VibeDeck gives a spare phone, tablet, or e-paper reader a real job: stream a dedicated Windows display for a long-running terminal or background task, keep system health and AI usage in view without switching windows, or remote‑control your PC from outside the house. One browser/PWA works across iPhone, Android, and BOOX. Pairing authority, display capture, input, and private data stay on the Windows PC.

[Download the latest Windows release](https://github.com/mabyes1/phoneMonitor/releases/latest) · [Build Week submission notes](docs/build-week-submission.md) · [Changelog](CHANGELOG.md) · [MIT License](LICENSE)

| Wireless display and remote control | Phone-first information board |
|---|---|
| ![VibeDeck wireless display](.codex-media/vibedeck-monitor.png) | ![VibeDeck information board](.codex-media/vibedeck-board.png) |

## Why VibeDeck

A spare phone is already a high-resolution, always-on screen sitting on your desk. VibeDeck gives it three distinct roles so it earns that space:

| Mode | What it does | Extra hardware or setup |
|---|---|---|
| **Display** | Streams a Windows virtual display or an existing physical monitor to your phone—perfect for a dedicated terminal, a background build, a game running AFK, or quick remote control from the couch; supports touch/mouse and mobile keyboard input | Existing-monitor control works immediately; the extended display is optional |
| **Sideboard** | Shows CPU, GPU, memory, storage, network, weather, processes, activity, and custom cards | None |
| **Quota** | Keeps Codex and AGY usage, reset windows, accounts, and remaining credits visible | Local CLI/account data on the Host PC |

The same approved browser can move among all three roles. It can reconnect on the LAN or through its installation-specific HTTPS address without a VibeDeck account, VPN, or router port forwarding. Every new browser still requires an explicit six-digit approval on the PC.

## Judge Quick Start

### Option A — product path

1. Open the [latest release](https://github.com/mabyes1/phoneMonitor/releases/latest) and download `VibeDeck-Setup-<version>.exe` plus its `.sha256` file.
2. Run Setup on Windows 10 or 11 x64. VibeDeck starts in the signed-in desktop session.
3. Open `http://127.0.0.1:5000` on the PC.
4. Select **Device setup** to pair a real browser, or run `scripts\open-device-lab.ps1` to inspect the actual client at BOOX Go Color 7, Galaxy S23, and iPhone XS viewports without owning those devices.
5. Try **Sideboard** and **Quota** first; neither requires the optional virtual display.

Setup may show a Windows SmartScreen prompt on first run (standard for open-source installers without an EV certificate). It does not enable test-signing mode or modify Secure Boot.

### Option B — reproduce from source

Prerequisites: Windows 10/11 x64 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Node.js is only required for the repository validation command.

```powershell
git clone https://github.com/mabyes1/phoneMonitor.git
cd phoneMonitor
.\start.bat
```

Then open `http://127.0.0.1:5000`.

Run the complete source gate:

```powershell
.\scripts\test-product-flow.ps1 -Source
```

No sample dataset or external account is required for the core experience. Device Lab supplies deterministic viewport and trust-state previews; live Quota cards are optional and read only the corresponding tools already used on the Host PC.

## What Makes It Different

- **Three roles, one device.** The same paired phone can be a dedicated extra display (for a CLI, a build log, or a game running AFK), a glanceable system board, or an AI quota view—switch anytime without re-pairing.
- **One client everywhere.** Safari, Chrome, and an installable PWA share one code path across iPhone, Android, and BOOX; there is no native mobile package to install.
- **PC-controlled trust.** A public route can carry encrypted traffic, but it cannot approve a device. Pairing and revocation remain local PC decisions.
- **Ships as a real product, not a demo.** VibeDeck handles Windows interactive-session boundaries, WebRTC fallback on constrained networks, e-paper-specific layouts, persistent state across reboots, silent in-place updates, auditable diagnostics, and multilingual UI—because these are where prototypes normally stop.
- **Useful without the driver.** Existing-monitor control, Sideboard, Quota, pairing, and Device Lab work before the optional virtual display is installed.

## OpenAI Build Week

VibeDeck entered OpenAI Build Week as an existing Windows virtual-display and browser-streaming prototype. Only work completed after the official submission-period cutoff is presented as Build Week work.

### Verifiable development window

| Evidence | Value |
|---|---|
| Official cutoff | `2026-07-13 09:00 PDT` / `2026-07-14 00:00 UTC+8` |
| Pre-event baseline | [`872a985`](https://github.com/mabyes1/phoneMonitor/commit/872a985c27dbb8c486aef50b7e76a2b1c67d5f8d), committed `2026-07-13 23:52 UTC+8` |
| First eligible commit | [`21c27e3`](https://github.com/mabyes1/phoneMonitor/commit/21c27e3), committed `2026-07-14 00:02 UTC+8` |
| Core submission range | [`21c27e3..fc81cce`](https://github.com/mabyes1/phoneMonitor/compare/872a985...fc81cce) — 21 commits after the baseline |
| Primary Codex `/feedback` session | `019f6890-877f-71e0-9ffa-7cf4d4457f2a` |

### What existed before vs. what was built during Build Week

| Before the event | Built or meaningfully extended during the event |
|---|---|
| Windows virtual-display and web-streaming prototype | Canonical Windows Setup, in-place updates, autostart, persistent product data, and release checks |
| Early browser client and dashboard experiments | One responsive/PWA product path for iPhone, Android, and BOOX, including multilingual and e-paper-specific layouts |
| LAN-oriented pairing and display flow | Explicit six-digit PC approval, persistent browser identity, revocation, managed HTTPS routing, and reconnect across networks |
| Basic display viewing | Existing-monitor selector, WebRTC H.264/JPEG fallback, touch/mouse control, and mobile Unicode keyboard bridge |
| Prototype telemetry and quota ideas | Configurable Sideboard layouts, activity, custom cards, Codex/AGY quota workflows, Device Lab, diagnostics, and regression gates |

### How Codex and GPT-5.6 contributed

Codex was used as an engineering partner for planning, implementation, debugging, review, testing, packaging, and delivery preparation. GPT-5.6 was most valuable where several systems interacted and a locally correct change could still break the product as a whole.

| Challenge | Human decision | Codex + GPT-5.6 contribution | Evidence |
|---|---|---|---|
| A Windows Service cannot capture the signed-in user's displays | Keep the Host in the interactive desktop session and make Setup the only product path | Traced the Session 0 failure, redesigned install/update/autostart behavior, and added product-flow checks | [`2824352`](https://github.com/mabyes1/phoneMonitor/commit/2824352), [`dcae485`](https://github.com/mabyes1/phoneMonitor/commit/dcae485), [`30e233e`](https://github.com/mabyes1/phoneMonitor/commit/30e233e) |
| One client had to work on phones and slow e-paper devices | Keep a single browser/PWA client instead of restoring native shells | Iterated responsive layouts, safe areas, e-paper constraints, localization, and deterministic Device Lab checks | [`cba7816`](https://github.com/mabyes1/phoneMonitor/commit/cba7816), [`ffece3c`](https://github.com/mabyes1/phoneMonitor/commit/ffece3c), [`db4c62c`](https://github.com/mabyes1/phoneMonitor/commit/db4c62c) |
| Remote access must not weaken local pairing authority | Let the cloud route traffic, never approve devices | Reviewed trust boundaries, implemented one-time connection codes and per-installation routing, then hardened pairing and reconnect behavior | [`5b65ace`](https://github.com/mabyes1/phoneMonitor/commit/5b65ace), [`012ad15`](https://github.com/mabyes1/phoneMonitor/commit/012ad15), [`c4458a8`](https://github.com/mabyes1/phoneMonitor/commit/c4458a8) |
| A small screen needed to be useful beyond mirroring | Treat Display, Sideboard, and Quota as distinct jobs | Implemented the existing-monitor selector, remote input, mobile keyboard bridge, and mobile energy/performance decisions | [`fc81cce`](https://github.com/mabyes1/phoneMonitor/commit/fc81cce) |
| A hackathon prototype still had to be testable | Prefer reproducible gates and real-device judgment over screenshots alone | Added source/payload/installed checks, worker tests, release packaging, diagnostics, and submission evidence | [`2c766f7`](https://github.com/mabyes1/phoneMonitor/commit/2c766f7), [`5b65ace`](https://github.com/mabyes1/phoneMonitor/commit/5b65ace), [`cd203d2`](https://github.com/mabyes1/phoneMonitor/commit/cd203d2) |

Human judgment remained responsible for product scope, security trade-offs, real-device acceptance, and the final quality bar. The commit history is intentionally retained as evidence rather than squashed into a single submission commit.

## Architecture

```text
Approved browser / PWA
  ├─ Display: WebRTC H.264 → JPEG fallback
  ├─ Input: pointer + Unicode keyboard bridge
  ├─ Sideboard / Quota / custom cards
  └─ Pairing request + device credential
                 │
          HTTPS / WebSocket
                 │
Windows Host (signed-in desktop session)
  ├─ local PC approval and trusted-device store
  ├─ DXGI display capture and Windows input
  ├─ telemetry, quota readers, layouts, diagnostics
  ├─ optional virtual-display installer
  └─ managed connector → installation-specific HTTPS route
                 │
Cloud control plane
  └─ routes encrypted traffic and one-time connection codes;
     it cannot approve or revoke a device
```

More detail: [product architecture](docs/product-architecture.md), [protocol](docs/protocol.md), [remote access](docs/remote-access.md), and [HTTPS onboarding](docs/https-onboarding.md).

## Trust, Privacy, and Security Boundaries

- The PC must explicitly approve each browser with a matching six-digit code.
- Device credentials are random, stored as hashes on the Host, and can be revoked from the PC.
- Public requests are accepted only through the installation's exact managed hostname and loopback connector path.
- The connector/control plane carries traffic but has no PC action token and cannot approve a device.
- VibeDeck has no cloud user account and does not upload display frames, quota data, or dashboard state to an application database.
- Quota integrations read local metadata/cache state; users are not asked to paste Codex tokens into the UI.
- Administrative actions such as virtual-display installation and product updates are local-PC-only.

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for bundled/downloaded components. Production code signing is planned for a future release.

## Install, Pair, and Use

### Windows Setup

Setup installs under `C:\Program Files\VibeDeck`, keeps mutable product data under `%ProgramData%\VibeDeck`, creates shortcuts and firewall rules, and starts the Host in the signed-in desktop session. Build it from source with:

```powershell
.\scripts\package-windows-setup.ps1
```

The version defaults to `src/PhoneMonitor.Host/PhoneMonitor.Host.csproj`. Output:

```text
artifacts\windows-setup\VibeDeck-Setup-<version>.exe
```

### Pair a browser

1. Open the QR code shown on the PC console, or open the Host URL manually.
2. Select **Start pairing** on the phone/browser.
3. Match the six-digit code and device name on the PC.
4. Select **Allow** on the PC.
5. Switch among Display, Sideboard, and Quota. Pairings persist until revoked.

The preferred managed URL looks like `https://<installation-id>.vibedeck.pp.ua/`. Local HTTPS and private-network routes remain available as fallbacks.

### Optional virtual display

Display mode can control an existing physical monitor without a driver. To create a separate Windows extended display, select **Create virtual display** on the local PC and accept the elevation prompt. VibeDeck verifies the pinned download hash and driver signature; normal users do not need the WDK or Windows test-signing mode.

## Validation

Verified on Windows x64 on 2026-07-20: **59/59 .NET tests passed**, **7/7 managed-connector Worker tests passed**, all browser JavaScript and shipped PowerShell parsed successfully, and `VibeDeck-Setup-0.1.31.exe` was produced with matching `0.1.31` file/product metadata. The release workflow publishes a SHA-256 sidecar for the tagged build.

The source gate restores dependencies and checks:

- all .NET Release tests;
- Cloudflare Worker tests;
- every browser JavaScript file with `node --check`;
- shipped PowerShell syntax;
- canonical product paths and removed legacy clients.

```powershell
.\scripts\test-product-flow.ps1 -Source
```

Build and validate the release payload:

```powershell
.\scripts\package-windows-setup.ps1
.\scripts\test-product-flow.ps1 -Payload -PayloadPath .\artifacts\windows-setup\payload
```

After installing:

```powershell
.\scripts\test-product-flow.ps1 -Installed
# Add -RequireVirtualDisplay only when testing the extended-display path.
```

The repository also contains unit/contract tests for device trust, pairing, managed connectors, public endpoints, updates, Windows input, dashboard layouts, custom sources, localization, quotas, and audit trails.

## Supported Platforms and Constraints

- **Host:** Windows 10 or Windows 11 x64.
- **Clients:** current Safari or Chromium-based browsers; installable PWA supported.
- **Tested layouts:** iPhone XS, Galaxy S23, and BOOX Go Color 7 representative viewports; real-device checks remain the final authority.
- **Display capture:** the Host must run in the signed-in desktop session, not as a Windows Service or through an RDP display session.
- **Secure desktop:** UAC and Ctrl+Alt+Delete are intentionally outside normal remote input.
- **Internet route:** cross-network access requires the Windows PC, Host, and connector to remain online.
- **Notifications:** Windows notification capture is an optional packaged companion because the API requires package identity.

## Repository Map

| Path | Purpose |
|---|---|
| `src/PhoneMonitor.Host` | Windows Host, APIs, capture/input, security, telemetry, and browser/PWA client |
| `workers/vibedeck-connect-code` | Managed endpoint and one-time connection-code control plane |
| `packaging/windows-setup` | Canonical Windows installer |
| `packaging/windows-notifications` | Optional notification companion |
| `tests/PhoneMonitor.Host.Tests` | .NET unit and contract tests |
| `scripts/test-product-flow.ps1` | Source, payload, and installed-product validation |
| `driver` | Virtual-display development project; not the normal installation path |
| `docs` | Architecture, protocol, onboarding, release, and submission documentation |

Useful documents: [release checklist](docs/release-checklist.md), [product updates](docs/product-updates.md), and [custom data sources](docs/custom-data-sources-spec.md).

## License

VibeDeck is released under the [MIT License](LICENSE). Third-party components remain under their respective licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
