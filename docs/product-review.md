# VibeDeck Product Review

Date: 2026-07-04

## Scope

This review covers the current Host web app, quota actions, display control endpoints, and the phone-facing UI. The goal is to move VibeDeck toward a shippable product while keeping changes reversible through commits.

## Findings

### P0: Unsafe LAN POST actions

Several POST endpoints can mutate local machine state:

- `/api/display/mode`
- `/api/display/enable`
- `/api/display/disable`
- `/api/sideboard/refresh`
- `/api/quotas/refresh`
- `/api/quotas/agy/import`
- `/api/quotas/agy/oauth/start`
- `/api/quotas/agy/cli/open`
- `/api/quotas/agy/account/delete`

These endpoints are intentionally reachable from the phone UI, but they should not accept unauthenticated cross-site POSTs. A hostile web page could otherwise try to trigger actions against `http://127.0.0.1:5000` or the LAN host.

Status: fixed in this pass.

Mitigation:

- Added `/api/session`.
- Added `X-PhoneMonitor-Action-Token`.
- All state-changing POST endpoints now require the token.
- The web UI automatically attaches the token for non-GET requests.

This is a productization guard, not a full account security model. The current pairing layer issues per-device tokens, allows revocation, and now has local HTTPS onboarding for LAN testing.

### P1: AGY CLI account switching is not yet authoritative

`agy.exe --help` exposes no public account-selection flag. PhoneMonitor can open AGY CLI with the selected account context, but cannot yet prove that AGY itself will switch active credentials from that context.

Status: partially implemented.

Current behavior:

- `▶` writes a launcher under `%LOCALAPPDATA%\PhoneMonitor\quotas\agy\launch`.
- The launcher opens AGY CLI and exports selected account id/email as environment variables.
- The terminal tells the user which account was selected.

Recommended next step:

- Locate AGY's official active-account storage or supported env/flag.
- Replace the context launcher with authoritative account switching.

### P1: Single-file web UI is too large

`wwwroot/index.html` contains display streaming, sideboard, quota UI, touch input, and settings logic in one file. This makes product review and regression prevention difficult.

Status: not fixed in this pass.

Recommended next step:

- Split quota, display streaming, input, and sideboard modules.
- Keep CSS grouped by mode.
- Add a small smoke test around each module.

### P1: Built Host executable can miss phone UI assets

Running the built `PhoneMonitor.Host.exe` directly can serve APIs while returning `404` for `/index.html` if `wwwroot` is not present beside the executable.

Status: fixed in this pass.

Mitigation:

- `wwwroot` content is copied to the build output with `PreserveNewest`.
- Host startup resolves the content root from the current working directory first, then the executable directory.
- Direct `bin/Debug/net6.0/PhoneMonitor.Host.exe --urls http://0.0.0.0:5000` runs can now serve the phone-facing web UI even when started from the repo root.

### P1: Token lifecycle UX needs explicit states

Quota cards show actions, but OAuth pending/success/failure, delete success, and CLI launch status are compressed into the summary line.

Status: improved in this pass.

Current behavior:

- Each quota account card now has its own action status line.
- Refresh, OAuth, CLI launch, and delete actions show pending/success/error on the affected card.
- Actions on the same card are disabled while one action is running.
- Status survives quota card re-rendering after refresh.

Recommended next step:

- Show when an account was last authenticated and refreshed.

### P2: Pairing model is still too open for a commercial build

The app currently assumes trusted LAN access. For a real packaged product, first-run pairing should bind a phone to the PC.

Status: partially fixed in this pass.

Current behavior:

- Added local device trust storage under `%LOCALAPPDATA%\PhoneMonitor\devices`.
- Added `X-PhoneMonitor-Device-Token`.
- Pairing can only be started from a loopback PC request with an action token.
- The PC creates a short-lived pairing QR; the phone completes it with a one-time secret and stores a persistent device token.
- Display streams, input WebSocket, read-only sensitive GET endpoints, and state-changing POST endpoints require either loopback access or a trusted device token.
- Added device revoke and clear-all APIs.
- Added a PC-local Trusted Devices UI with last-seen time, last remote address, refresh, revoke, and clear-all.
- `/api/devices/status` only returns the full device list to loopback PC requests; phones only receive their own trust state.
- Unpaired phones see a locked UI state instead of quota, sideboard, display, or stream data.

Current HTTPS behavior:

- Host startup automatically creates or refreshes the local PhoneMonitor root CA and Host certificate under `%LOCALAPPDATA%\PhoneMonitor\certs`.
- The Host keeps HTTP on port `5000` and adds HTTPS on port `5443` when the certificate files are ready.
- The Host stores a certificate state file and reissues the Host certificate when the current LAN IP list changes.
- `/cert/phone-monitor-root.cer` lets the phone download the root certificate from the bootstrap HTTP page.
- `/qr.svg` and Pair Phone QR codes prefer HTTPS when the local certificate is configured.

Recommended next step:

- Add a more guided iPhone/Android certificate trust flow on the web path (iPhone has no native app; pairing stays in Safari/PWA).

## Verification

Required checks for this pass:

- Build Host.
- Confirm built Host executable serves `/index.html`.
- Confirm POST without action token returns `403`.
- Confirm POST with action token succeeds.
- Confirm frontend JavaScript parses.
- Confirm AGY quota refresh still works.
- Confirm quota action status renders and completes in the card UI.
- Confirm pairing start requires an action token.
- Confirm pairing QR can mint a device token.
- Confirm pairing URL hash is completed by a fresh phone page and then cleared.
- Confirm device revoke removes test devices.
- Confirm PC-local clear-all removes all paired test devices.
- Confirm PC-local Pair Phone UI generates a pairing QR.
- Confirm PC-local Trusted Devices UI lists and revokes a paired phone.
- Confirm non-loopback unpaired status does not expose the device list.
- Confirm non-loopback unpaired read-only sensitive GET endpoints return `403`.
- Confirm non-loopback paired quota snapshot can be read with a device token.
- Confirm unpaired LAN UI shows locked display/quota states without layout overflow.
- Confirm local HTTPS serves `/health` and `/api/connect` on port `5443`.
- Confirm root certificate download works from the HTTP bootstrap page.
- Confirm Pair Phone QR uses an HTTPS URL when the local certificate is configured.
