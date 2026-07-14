# VibeDeck Windows Setup

Product installer assets for the one-click Windows install path.

## What the user gets

1. **Setup.exe** — modern Inno Setup wizard (admin).
2. **VibeDeck Host** Windows Service (`VibeDeckHost`) — Automatic start, restart on failure.
3. **Desktop / Start Menu icon** — opens `http://127.0.0.1:5000` (starts the service if needed).
4. **Firewall rule** — inbound allow for `PhoneMonitor.Host.exe` (LAN phone access).
5. **Data** — `%ProgramData%\VibeDeck` (certs, devices, quotas, custom sources).

## Build

From repo root:

```powershell
scripts\package-windows-setup.ps1 -Version 0.1.0
```

Output:

```text
artifacts\windows-setup\VibeDeck-Setup-0.1.0.exe
artifacts\windows-setup\payload\          # published self-contained Host
```

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php). Optional:

```powershell
scripts\package-windows-setup.ps1 -InstallInno
scripts\package-windows-setup.ps1 -SkipInno   # payload only
```

Fallback install without Setup.exe (elevated):

```powershell
scripts\package-windows-setup.ps1 -SkipInno
scripts\install-windows-product.ps1
```

## Files

| File | Role |
|------|------|
| `VibeDeck.iss` | Inno Setup script |
| `Open-VibeDeck.vbs` / `.cmd` | Icon entry → ensure service → open Web UI |
| `product-install.json` | Marker so Host uses installed data paths |
| `vibedeck.ico` | Setup + shortcut icon |

## Dev vs product

| Mode | How | Data |
|------|-----|------|
| Dev | `start.bat` / `dev-run.ps1` | `%LocalAppData%\PhoneMonitor` |
| Product | Setup / Windows Service | `%ProgramData%\VibeDeck` |
