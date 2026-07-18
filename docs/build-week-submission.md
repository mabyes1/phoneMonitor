# VibeDeck — Build Week Submission Kit

This file contains the final English copy and asset checklist for the OpenAI Build Week Devpost submission. It is written for the **Work & Productivity** category.

## Submission Fields

| Field | Copy |
|---|---|
| Project name | `VibeDeck` |
| Category | `Work & Productivity` |
| Tagline | `Turn idle phones and e-paper readers into trusted, persistent Windows work surfaces.` |
| Short description | `VibeDeck gives a spare phone or BOOX a real role in a Windows workflow: an optional wireless display, a live information sideboard, and an AI quota view through one secure browser/PWA path.` |

## Long Description

### Bring idle screens back to work

Most desks already have a second screen: an older phone, a small tablet, or an e-paper reader that is no longer central to daily work. Existing virtual-monitor tools can mirror a desktop, but a phone-sized screen is often a poor miniature desktop. It is better at a focused role: a glanceable system sideboard, an AI quota view, or a small display surface when one is actually needed.

VibeDeck turns those devices into persistent companions for a Windows workstation. One local Windows Host serves iPhone, Android, and BOOX through Safari, Chrome, or an installable PWA. There is no native mobile app and no cloud account required to start.

### What VibeDeck does

- **Display mode:** creates an optional, real Windows virtual monitor. Move a normal desktop window to it and stream it locally with WebRTC H.264, with a JPEG compatibility fallback.
- **Sideboard mode:** shows live system telemetry, activity, and focused work context in a phone-first layout instead of shrinking the whole desktop.
- **Quota mode:** keeps Codex and AGY limits, reset windows, accounts, and remaining ChatGPT Credits visible without opening another dashboard.
- **Trusted pairing:** the PC presents a secure URL and QR code; the phone request is matched with a six-digit code and explicitly approved on the PC. Pairings persist with the browser identity.
- **Product behavior:** Windows Setup supports in-place updates, product data survives replaceable application files, and a diagnostic trail makes the next issue easier to locate.

### Why it is different

VibeDeck is not trying to replace a large second monitor or compete as a generic screen-mirroring utility. It gives a spare device a durable, role-specific place in a Windows workflow. The same paired browser can become an optional display, a glanceable information surface, or an AI usage companion. The product is designed around the awkward realities that prototypes usually skip: browser trust, explicit pairing, signed-in Windows desktop sessions, persistent state, e-paper constraints, multilingual UI, installation, updates, and real-device verification.

### Built with Codex + GPT-5.6

Codex was the engineering partner throughout the Build Week: planning, implementation, debugging, review, test design, packaging, and final delivery preparation. GPT-5.6 helped reason about the cross-system decisions that made the prototype a usable product: the Windows interactive-session boundary, display enumeration, browser-media fallbacks, secure pairing, persistent data ownership, e-paper layout constraints, and installer/update behavior.

The work was intentionally routed by task. Faster model tiers accelerated repetitive layout and workflow loops. Deeper reasoning was used for product planning, larger refactors, and review. Human judgment owned the actual product trade-offs, real-device validation, and the final quality bar. AI amplified a solo builder's throughput; it did not replace responsibility for the finished result.

### How to run and verify

VibeDeck runs on Windows 10 or 11 x64. Run the supplied `VibeDeck-Setup-<version>.exe` for the product path, or install the .NET 8 SDK and run `start.bat` from this repository for source development. Visit `http://127.0.0.1:5000` to open the PC console.

The repository includes `scripts\test-product-flow.ps1 -Source` and `scripts\test-product-flow.ps1 -Installed` for product-path checks. The local Device Lab can load the real client at BOOX Go Color 7, Galaxy S23, and iPhone XS viewports, so reviewers can inspect responsive and e-paper behavior without owning all three devices.

### Notes for reviewers

The Windows Host must be running in a signed-in desktop session; a Windows Service cannot capture or enumerate that user's display. Display mode requires the optional virtual display, while Sideboard and Quota do not. Phone clients are intentionally browser/PWA-only, so the mobile path remains identical across iPhone, Android, and BOOX.

## Attach Before Submitting

- [ ] Public YouTube URL for the V9 demo video (under three minutes, with audio that explains both Codex and GPT-5.6 use).
- [ ] Public repository URL, or a private repository shared with `testing@devpost.com` and `build-week-event@openai.com`.
- [ ] `/feedback` Codex Session ID: `019f6890-877f-71e0-9ffa-7cf4d4457f2a`.
- [ ] Optional companion PDF upload: `VibeDeck-Build-Week-Companion-Book-v8-bilingual.pdf`.
- [ ] Confirm that the uploaded video, PDF, screenshots, and repository contain no personal account details, local IP addresses, unsafe QR code, or third-party material without permission.

## Prepared Local Assets

These files are prepared locally and must be uploaded separately because `artifacts/` is intentionally ignored by Git:

- `artifacts\hackathon-trailer\v8\out\VibeDeck-Build-Week-Demo-v9.mp4`
- `artifacts\hackathon-trailer\v8\companion-book\out\VibeDeck-Build-Week-Companion-Book-v8-bilingual.pdf`
