# VibeDeck Windows Setup

Product installer assets for the one-click Windows install path.

## What the user gets

1. **Setup.exe** — the only supported Windows product release and update path.
2. **VibeDeck Host** desktop-session background app — starts automatically after Windows sign-in so display capture sees the interactive desktop.
3. **Desktop / Start Menu icon** — opens `http://127.0.0.1:5000` (starts the Host if needed).
4. **Firewall rule** — inbound allow for `VibeDeck.Host.exe` (LAN phone access).
5. **Data** — `%ProgramData%\VibeDeck` (certs, devices, quotas, custom sources).

## Build

From repo root:

```powershell
scripts\package-windows-setup.ps1
```

Output:

```text
artifacts\windows-setup\VibeDeck-Setup-<version>.exe
artifacts\windows-setup\payload\          # published self-contained Host
```

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php). Optional:

```powershell
scripts\package-windows-setup.ps1 -InstallInno
scripts\package-windows-setup.ps1 -SkipInno   # payload only
```

For a complete local one-click build and installation, double-click `install.bat`. For an in-place update, double-click `update.bat`; it silently stops the old Host, runs Setup, preserves `%ProgramData%\VibeDeck`, restarts the Host, and verifies the installed product.

Fallback install without Setup.exe (elevated):

```powershell
scripts\package-windows-setup.ps1 -SkipInno
scripts\install-windows-product.ps1
```

This fallback is for local development/deployment only. Public distribution must use Setup so upgrades, app cleanup, autostart, shortcuts, and uninstall metadata stay consistent.

## Files

| File | Role |
|------|------|
| `VibeDeck.iss` | Inno Setup script |
| `Start-VibeDeck-Host.vbs` | Hidden desktop-session Host launcher |
| `Open-VibeDeck.vbs` / `.cmd` | Icon entry → ensure Host → open Web UI |
| `product-install.json` | Marker so Host uses installed data paths |
| `vibedeck.ico` | Setup + shortcut icon |

## Upgrade behavior

- A newer Setup uses the same AppId and updates in place.
- The legacy `VibeDeckHost` service is removed before files are replaced.
- Replaceable web/runtime directories are cleared so deleted modules do not survive.
- `%ProgramData%\VibeDeck` is outside `{app}` and remains intact.
- The updated Host restarts in the signed-in desktop session.

## Dev vs product

| Mode | How | Data |
|------|-----|------|
| Dev | `start.bat` / `dev-run.ps1` | `%LocalAppData%\PhoneMonitor` |
| Product | Setup / signed-in desktop Host | `%ProgramData%\VibeDeck` |
