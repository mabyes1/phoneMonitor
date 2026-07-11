# VibeDeck HTTPS Onboarding

Last updated: 2026-07-04

The zero-install phone path still starts from the Host web page, but HTTPS is now available for local LAN use. `PhoneMonitor` remains in the certificate file names and local storage path for compatibility. This matters for two product reasons:

- iPhone/Safari Wake Lock behavior is better when the page is opened over HTTPS.
- Pairing and device tokens should not travel over plain HTTP on a real LAN.

## PC Setup

Normal Host startup checks the local certificate automatically. If the certificate set is missing, incomplete, close to renewal, or does not match the current LAN IP list, the Host generates or refreshes it before opening the HTTPS listener.

The certificate files live in:

```text
%LOCALAPPDATA%\PhoneMonitor\certs
```

Managed files:

- `phone-monitor-root.pfx`
- `phone-monitor-root.cer`
- `phone-monitor-host.pfx`
- `phone-monitor-host.cer`
- `phone-monitor-certificate-state.json`

The root PFX stays local to the PC so the Host can reissue the Host certificate when the LAN IP changes without forcing the phone to trust a new root certificate.

Manual repair command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-https.ps1
```

After the certificate files exist, the Host listens on:

- `http://0.0.0.0:5000`
- `https://0.0.0.0:5443`

`scripts\dev-run.ps1` runs the setup script automatically before starting the Host.

If you want to throw away the local root and start over:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-https.ps1 -Force
```

## Phone Setup

Open the normal HTTP page first:

```text
http://<pc-lan-ip>:5000/
```

Use the `安裝 HTTPS 憑證` link to download the root certificate onto the phone. Then install and trust that certificate in the phone OS.

Android path:

- Preferred native path: open the Android app from `開啟手機 App`, then tap `憑證`. The app downloads the root certificate to Android Downloads and opens security settings.
- In Android settings, install `phone-monitor-root.cer` as a CA certificate.
- Browser-only path: install the downloaded `.cer` as a CA certificate from Android settings.
- Chrome/PWA can then open the HTTPS URL for Wake Lock testing.
- The native Android shell also trusts user-installed CAs, but it can keep the screen awake over HTTP because it uses Android window flags.
- ADB is not part of the product path. It is only useful for developer-side install/test automation.
- Android does not allow normal apps to silently install CA certificates; the final CA install confirmation must stay in system settings.

iPhone path:

- Install the downloaded certificate profile.
- Enable full trust for the root certificate in iOS settings.
- Open the HTTPS URL in Safari for the best zero-install Wake Lock path.

After trust is enabled, open the secure URL:

```text
https://<pc-lan-ip>:5443/
```

The Host reports both URLs through `/api/connect`. When local HTTPS is ready, `/qr.svg` and Pair Phone QR codes prefer the HTTPS URL.

## Current Limits

This is local development HTTPS, not a public CA certificate. It is good enough for a trusted personal LAN and Wake Lock testing, but a packaged product still needs a guided certificate trust flow or a native app path that avoids manual certificate setup.
