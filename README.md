# VibeDeck

VibeDeck turns a spare phone or e-paper device into a private wireless second screen, system dashboard, and AI-usage sideboard for Windows.

The display, dashboard data, input, and pairing approval stay on the Windows Host. iPhone, Android, and BOOX devices connect through Safari, Chrome, or an installable PWA. VibeDeck's hosted control plane only assigns a browser-trusted HTTPS hostname and never receives a VibeDeck account or bypasses PC approval; users do not need a Cloudflare account.

## Product Architecture

| Component | Supported product path |
|---|---|
| Windows Host | Install with `VibeDeck-Setup-<version>.exe`; it starts in the signed-in desktop session |
| Phone and e-paper clients | Safari, Chrome, or Add to Home Screen/PWA |
| Virtual display | Optional; required only for second-screen mode |
| Windows notifications | Optional packaged companion; it forwards notifications to the Host |
| Product updates | Run a newer Setup with the same AppId |
| Trusted HTTPS | Setup includes a hidden connector; VibeDeck automatically assigns and maintains the PC URL |
| Source development | Use `start.bat` or `scripts\dev-run.ps1` |

The Host must not run as a Windows Service. A service runs in Session 0 and cannot enumerate or capture displays belonging to the signed-in user. VibeDeck instead launches as a hidden background process in the interactive desktop session.

## Judge Quick Start

VibeDeck is a Windows x64 product. A modern browser is the only requirement on iPhone, Android, and BOOX; no native mobile client is installed.

1. **Run the product.** For a release build, run `VibeDeck-Setup-<version>.exe`. To reproduce from source, install the .NET 8 SDK, stop any installed Host that owns ports 5000/5443, then run `start.bat`.
2. **Open the PC console.** Visit `http://127.0.0.1:5000` and use **Device setup** to view the pairing route or create the optional virtual display.
3. **Verify without physical devices.** Run `scripts\open-device-lab.ps1`, then switch among BOOX Go Color 7, Galaxy S23, and iPhone XS profiles. The lab loads the real VibeDeck client at its exact target viewport.
4. **Run the checks.** Use `scripts\test-product-flow.ps1 -Source` for source validation, or `scripts\test-product-flow.ps1 -Installed` after Setup. Add `-RequireVirtualDisplay` only when testing Display mode.

The most complete product path is Windows Setup → browser-trusted QR pairing → explicit six-digit approval on the PC → Display, Sideboard, or Quota on the paired device. Sideboard and Quota do not need the optional virtual display.

## Features

- A real Windows virtual monitor that accepts normal desktop windows.
- Low-latency WebRTC H.264 streaming with a JPEG compatibility fallback.
- Responsive phone, tablet, and e-paper layouts from one browser/PWA client.
- Live CPU, GPU, memory, storage, network, weather, and process information.
- AI-tool usage and quota cards.
- Configurable dashboard layouts and activity updates.
- Optional Windows notification integration.
- Local device approval remains authoritative; the cloud control plane cannot approve a phone.
- Automatic browser-trusted HTTPS without certificate installation, command windows, or a user Cloudflare account.
- Windows Setup, in-place updates, autostart, and persistent product data.

## Showcase

### Wireless second-screen mode

VibeDeck streams a live Windows virtual display to the browser client over the local network.

![VibeDeck wireless second-screen mode](.codex-media/vibedeck-monitor.png)

### Information Board

The same device can become a focused desktop sideboard with live system telemetry, process insights, and AI-usage information.

![VibeDeck Information Board](.codex-media/vibedeck-board.png)

## Requirements

For an installed build:

- Windows 10 or Windows 11 on x64.
- A phone or e-paper device on the same Wi-Fi network, connected through the same Tailscale network, or using the configured VibeDeck secure URL.
- The optional virtual display only when using second-screen mode.

Building from source additionally requires the .NET 8 SDK. Creating the Windows installer requires Inno Setup 6.

## Install VibeDeck

For a complete build + install from this repository, double-click `install.bat`. It builds the canonical Setup, requests administrator permission once, installs silently, starts the Host hidden, and verifies the running product.

Run the packaged installer:

```text
VibeDeck-Setup-<version>.exe
```

Setup performs the complete product installation:

- installs the application under `C:\Program Files\VibeDeck`;
- stores persistent product data under `%ProgramData%\VibeDeck`;
- creates Start menu and desktop shortcuts;
- configures hidden autostart in the signed-in desktop session;
- creates the firewall rules required for LAN access;
- removes obsolete VibeDeck Windows Service registrations.
- installs the verified background HTTPS connector managed by the Host.

After installation, open:

```text
http://127.0.0.1:5000
```

### Build the installer from source

```powershell
scripts\package-windows-setup.ps1
```

The default version comes from `src/PhoneMonitor.Host/PhoneMonitor.Host.csproj`. To specify it explicitly:

```powershell
scripts\package-windows-setup.ps1 -Version 0.1.1
```

Output:

```text
artifacts\windows-setup\VibeDeck-Setup-<version>.exe
```

To build only the installation payload without compiling the Setup executable:

```powershell
scripts\package-windows-setup.ps1 -SkipInno
```

The packaging script normally runs tests, JavaScript syntax checks, product-path checks, and payload validation before producing the installer.

## Update an Existing Installation

Installed users update from the local PC UI: press **Check for updates**, then **Install vX.Y.Z**. VibeDeck verifies the published Setup file before handing it to the normal Windows installer; paired devices and layouts remain in `%ProgramData%\VibeDeck`. See `docs/product-updates.md` for the user and publisher flow.

For repository development only, double-click `update.bat`. It builds a local Setup and performs the same in-place replacement and installed-product checks. Do not distribute `update.bat` to users.

1. Build or download a newer Setup version.
2. Run it without uninstalling the existing version.
3. Setup stops the old Host, replaces application files, removes obsolete service registrations, and starts the new Host in the desktop session.
4. `%ProgramData%\VibeDeck` is preserved, including paired devices, certificates, quota accounts, custom cards, and notification settings.

Verify the updated installation:

```powershell
scripts\test-product-flow.ps1 -Installed
```

If the optional virtual display is installed:

```powershell
scripts\test-product-flow.ps1 -Installed -RequireVirtualDisplay
```

These checks verify that:

- no obsolete Host service remains;
- the Host runs outside Session 0;
- port 5000 belongs to the correct Host process;
- Windows display enumeration is coming from the interactive session;
- the PhoneMonitor virtual display is available when required.

## Connect a Phone or E-Paper Device

The preferred path is to open the QR code shown by the PC Host. VibeDeck automatically assigns a secure URL shaped like:

```text
https://<installation-id>.vibedeck.pp.ua/
```

This browser-trusted route does not require accepting a dangerous-page warning, installing a phone certificate, opening a command window, or configuring Cloudflare. The Host still requires the normal phone request, six-digit verification code, and PC **Allow** action. If the Internet control plane is temporarily unavailable, the UI falls back to the existing local-network HTTPS route instead of advertising a dead public URL.

The local-network fallback opens the HTTPS address shown by the PC Host, for example:

```text
https://192.168.1.20:5443
```

On first connection:

1. Request pairing from the phone.
2. Confirm the device name and six-digit code on the PC.
3. Select **Allow** on the PC.
4. Switch between Display, Information Board, and Quota modes on the client.

The phone and PC show the same explicit pairing progress:

| Progress | Meaning |
|---:|---|
| 0–20% | Host, HTTPS address, and QR code are being prepared |
| 25% | Phone reached the Host and is waiting for **Start pairing** |
| 40% | Browser identity and the exact available device model are being read |
| 70–75% | Request and six-digit code reached the PC; waiting for **Allow** |
| 90% | Approval succeeded and the persistent credential is being saved |
| 100% | Pairing is saved, or an existing pairing was restored; the page reloads automatically |

Pairing is attached to a persistent browser-instance ID and stored under `%ProgramData%\VibeDeck\devices`. Re-pairing the same browser continues the existing device record and rotates its credential instead of adding a duplicate. Android Chromium reports its model when available, so known devices appear as names such as `BOOX Go Color 7` and `Samsung SM-S9110`; browsers that intentionally hide the model fall back to a platform name. The secure URL assignment is persisted separately in `%ProgramData%\VibeDeck\connect` and only the local PC can change it.

Information Board and Quota modes do not require the virtual display. iPhone, Android, and BOOX all use the same Host-served web application; responsive and e-paper styles handle platform differences.

For access across different networks, use Tailscale. See `docs/remote-access.md`. HTTPS and iPhone certificate setup are documented in `docs/https-onboarding.md`.

## Virtual Display

Only Display mode requires the PhoneMonitor virtual display.

When the PC UI reports that no virtual display is available, select **Create virtual display** and approve the Windows elevation prompt. Normal users do not need the Windows Driver Kit, test-signing mode, or driver-development scripts.

The Host must run in the local signed-in desktop session. If `/api/displays` reports only `WinDisc 1024x768`, the Host is running in the wrong session; this is not evidence that the virtual display driver is missing.

Driver-development tools remain under `driver/` and `scripts/*driver*.ps1`, but they are not part of the normal product installation path.

## Windows Notification Companion

Windows `userNotificationListener` access requires packaged application identity, so notification capture is implemented as an optional MSIX companion. It forwards notification data to the Host and must not listen on ports 5000 or 5443.

Build and install the development package:

```powershell
scripts\package-windows-notifications.ps1 -Install
```

If Windows requires machine-level trust for the development certificate, run from an elevated PowerShell window:

```powershell
scripts\package-windows-notifications.ps1 -RegisterOnly -InstallCertificateMachine
```

The companion process is `VibeDeck.Notifications.exe`. See `docs/windows-notifications.md` for details.

## Run from Source

Start the project with:

```text
start.bat
```

or:

```powershell
scripts\dev-run.ps1
```

Source-development data is stored under `%LocalAppData%\PhoneMonitor`. Installed-product data is stored under `%ProgramData%\VibeDeck`.

The source and installed Hosts cannot use port 5000 at the same time. Stop the installed Host before starting source development. The notification companion may remain running.

Run the source product-flow checks with:

```powershell
scripts\test-product-flow.ps1 -Source
```

## Uninstall

Prefer Windows **Installed apps**, or run from an elevated PowerShell window:

```powershell
scripts\uninstall-windows-product.ps1
```

To preserve product data:

```powershell
scripts\uninstall-windows-product.ps1 -KeepData
```

The notification companion is a separate MSIX package. Remove it with:

```powershell
scripts\package-windows-notifications.ps1 -Uninstall
```

## Troubleshooting

| Symptom | First check |
|---|---|
| PC page does not open | Start VibeDeck, then run `scripts\test-product-flow.ps1 -Installed` |
| Virtual display is installed but missing | Check the Host session; do not reinstall the driver solely because `WinDisc` appears |
| Phone cannot connect | Confirm Wi-Fi/Tailscale connectivity, firewall access, and the HTTPS URL |
| Phone UI looks like an old app | Close obsolete clients and use Safari, Chrome, or the PWA |
| A paired phone asks to pair again | Run `scripts\test-product-flow.ps1 -Installed`, then verify `%ProgramData%\VibeDeck\devices\trusted-devices.json`; Setup grants signed-in users write access and the Host keeps a `.bak` recovery copy |
| Refresh briefly opens PowerShell | Update to the latest Setup; the local information collector is launched non-interactively with a hidden window |
| Windows notifications do not appear | Confirm that the companion is connected and allowed |
| Data appears missing after an update | Verify `%ProgramData%\VibeDeck`; do not confuse it with the development data directory |
| Quota cards have no data | Use the corresponding local CLI/account on the Host PC, then refresh the card |

## Repository Structure

- `src/PhoneMonitor.Host`: Windows Host, APIs, streaming, and browser/PWA client.
- `packaging/windows-setup`: canonical Windows Setup packaging.
- `packaging/windows-notifications`: optional notification companion package.
- `scripts/test-product-flow.ps1`: shared source, payload, and installed-product checks.
- `driver`: virtual display development project.
- `docs`: protocol, remote access, HTTPS, notifications, product, and release documentation.
- `AGENTS.md`: engineering constraints for future development and debugging.

## Release Checklist

```powershell
scripts\test-product-flow.ps1 -Source
scripts\package-windows-setup.ps1
# Run the new Setup for a clean installation or in-place update.
scripts\test-product-flow.ps1 -Installed
```

See `docs/release-checklist.md` for the complete manual checklist.

## OpenAI Build Week

VibeDeck entered OpenAI Build Week as an existing Windows virtual-display prototype. During the submission period, it was meaningfully extended into a complete product workflow with Codex and GPT-5.6.

Build Week work includes:

- redesigned responsive phone and e-paper interfaces, including rotation and overlay fixes;
- a Windows Setup and upgrade path that preserves product data and starts the Host in the signed-in desktop session;
- improved virtual-display discovery and setup guidance;
- a single browser/PWA product path for iPhone, Android, and BOOX devices;
- configurable dashboard layouts, activity updates, quota cards, and optional Windows notification integration;
- installed-product and source-product flow checks for packaging and release verification.

### Codex + GPT-5.6 evidence

| Product decision | How Codex and GPT-5.6 accelerated the work | Verifiable result |
|---|---|---|
| Make the prototype installable | Reasoned through the signed-in desktop-session requirement, installer lifecycle, updates, and data ownership. | Windows Setup replaces app files while `%ProgramData%\VibeDeck` preserves pairings, layouts, diagnostics, and quota data. |
| Keep one phone client across devices | Iterated responsive layouts, e-paper constraints, browser-media fallbacks, and first-run pairing flows. | One browser/PWA client serves iPhone, Android, and BOOX; Device Lab validates the exact S23, iPhone XS, and BOOX viewports. |
| Turn debugging into product behavior | Planned and reviewed diagnostics, tests, product-path guardrails, and release checks. | Auditable diagnostic trail plus source, payload, and installed-product flow checks. |

Codex was used as an engineering partner for planning, implementation, debugging, review, testing, packaging, and delivery assets. GPT-5.6 helped reason across Windows session behavior, display enumeration, browser-media constraints, mobile and e-paper layouts, and installer lifecycle. Faster model tiers handled repetitive layout and workflow passes; deeper reasoning was reserved for system design, larger refactors, and review. Human judgment remained responsible for trade-offs, real-device acceptance, and the final quality bar.

The project is not a generic screen-mirroring clone: VibeDeck gives the same spare device a durable role as an optional Windows display, a glanceable sideboard, or an AI quota surface, while retaining browser-trusted pairing and persistent product state.

Dated commits and Codex session logs document work completed during the event. The primary Build Week Codex session ID is:

```text
019f6890-877f-71e0-9ffa-7cf4d4457f2a
```

See [`docs/build-week-submission.md`](docs/build-week-submission.md) for the ready-to-paste Devpost copy, required submission assets, and final submission checklist.

## Roadmap

- Current refactoring priorities and first-run UX acceptance criteria are documented in [`docs/technical-debt-roadmap.md`](docs/technical-debt-roadmap.md) and [`docs/ui-ux-pairing-review.md`](docs/ui-ux-pairing-review.md).
- Continue translation coverage and multilingual regression checks across newly added product flows.
- Improve adaptive stream quality and latency handling.
- Add more dashboard modules and integrations.
- Make e-paper refresh behavior configurable.
- Simplify signed distribution for non-technical users.

## License

[MIT](LICENSE)
