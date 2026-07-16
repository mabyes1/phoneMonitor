# VibeDeck HTTPS Onboarding

Last updated: 2026-07-16

The zero-install phone path still starts from the Host web page. HTTPS is required for real LAN pairing and is strongly preferred for Wake Lock / home-screen use.

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
