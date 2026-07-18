# VibeDeck HTTPS Onboarding

Last updated: 2026-07-16

The zero-install phone path still starts from the Host web page. HTTPS is required for real LAN pairing and is strongly preferred for Wake Lock / home-screen use.

## Preferred product path: VibeDeck secure URL

From `0.1.22`, the normal product connection can use one browser-trusted URL per Windows Host:

```text
https://<installation-id>.vibedeck.pp.ua/
```

The Host creates and persists its `installation-id` under the product data root. A Cloudflare Tunnel (or future VibeDeck control plane) publishes that exact first-level hostname and forwards it to `http://127.0.0.1:5000`. The Windows Host never stores a Cloudflare API token or tunnel token.

When the local PC records that URL in **進階連線資訊 → VibeDeck 安全網址**:

1. The PC QR code switches to the trusted public HTTPS URL.
2. Safari, Chrome, Android, iPhone, and BOOX open without a certificate warning.
3. Phone pairing still requires the existing six-digit code and an explicit **Allow** action on the PC.

### E-paper readers without a camera

When a trusted public URL is configured, the PC can create an eight-character, one-time **e-paper connection code**. On the reader, open the short shared address `https://vibedeck.pp.ua/`, enter the code, and the browser is redirected once to that PC's installation-specific secure URL. The reader then completes the normal PC-approved pairing flow and can be added to its home screen.

The connection code lasts ten minutes and is deleted as soon as it resolves. It only reveals the already-configured VibeDeck URL; it cannot pair a device or bypass the PC's approval boundary.

The shared landing address is implemented by `workers/vibedeck-connect-code`. It uses a Cloudflare Durable Object so a code is immediately available across edge locations and can only resolve once. Deploy it to the root route after authenticating Wrangler:

```powershell
cd workers\vibedeck-connect-code
npx wrangler deploy
```

The Worker verifies `https://vd-<installation-id>.vibedeck.pp.ua/` through `/api/connect` before accepting a code registration. No Cloudflare credential or Worker secret is stored by VibeDeck Host or sent to the reader.

The Host accepts a public pairing request only when all of these are true:

- the request was forwarded by a connector on `127.0.0.1` or `::1`;
- forwarded HTTPS and hostname exactly match this Host's stored URL;
- the phone completes the usual PC approval flow.

This keeps router ports closed while preserving the local approval boundary. The old local certificate path below remains a recovery/offline fallback until Tunnel provisioning is automated in Setup.

## Where certificates live

Installed product (Setup):

```text
%ProgramData%\VibeDeck\certs
```

Source / dev runs:

```text
%LOCALAPPDATA%\PhoneMonitor\certs
```

On-disk filenames keep the legacy `phone-monitor-*` names so existing installs keep working. The phone download links use product names:

- `/cert/vibedeck-root.cer` (preferred)
- `/cert/vibedeck-host.cer`
- `/cert/phone-monitor-root.cer` (legacy alias, same file)
- `/cert/phone-monitor-host.cer` (legacy alias, same file)

Managed files on disk:

- `phone-monitor-root.pfx` / `phone-monitor-root.cer`
- `phone-monitor-host.pfx` / `phone-monitor-host.cer`
- `phone-monitor-certificate-state.json`

New roots are issued as `CN=VibeDeck Local Root CA`. Already-trusted older roots continue to work until they are renewed.

## PC Setup

Normal Host startup checks the local certificate automatically. If the set is missing, incomplete, close to renewal, or does not match the current LAN IP list, the Host generates or refreshes it before opening the HTTPS listener.

Manual repair (dev / recovery):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-https.ps1
```

After the certificate files exist, the Host listens on:

- `http://0.0.0.0:5000`
- `https://0.0.0.0:5443`

If you want to throw away the local root and start over:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-https.ps1 -Force
```

Phones must reinstall and re-trust the root after a forced reset.

## Phone Setup

Open the bootstrap HTTP page first (or scan the PC QR, which prefers HTTPS when ready):

```text
http://<pc-lan-ip>:5000/
```

Use **安裝 HTTPS 憑證** to download the root certificate. Install and fully trust it in the phone OS.

### Android / Chrome

- Install the `.cer` as a CA certificate from system settings.
- Open the HTTPS URL for WebRTC H.264 with JPEG fallback.
- Android never silently installs CAs from a normal web page.

### iPhone / Safari

- Install the downloaded certificate profile.
- Settings → General → About → Certificate Trust Settings → enable full trust.
- Open the HTTPS URL in Safari (or re-add to Home Screen from HTTPS).

Secure URL:

```text
https://<pc-lan-ip>:5443/
```

Pairing request and approval polling require HTTPS. `/api/connect` reports both HTTP and HTTPS URLs.

## Current Limits

This is local development HTTPS, not a public CA certificate. It is good enough for a trusted personal LAN. For remote access, use Tailscale or a reverse proxy with a publicly trusted certificate.
