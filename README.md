# VibeDeck

<p align="center">
  <img src="docs/screenshots/app-icon.png" width="96" alt="VibeDeck icon" />
</p>

<p align="center">
  <strong>EN</strong> · Turn an idle phone into a PC side display and command deck<br/>
  <strong>中文</strong> · 把閒置手機變成電腦副螢幕與指令資訊板
</p>

<p align="center">
  <a href="#english">English</a> ·
  <a href="#中文">中文</a> ·
  <a href="LICENSE">MIT License</a>
</p>

---

## Screenshots · 截圖

| Display stream · 顯示器串流 | Device connect · 裝置連線 |
|:---:|:---:|
| ![Display](docs/screenshots/01-display-stream.png) | ![Connect](docs/screenshots/02-device-connect.png) |

| Sideboard · 資訊板 | Command layout · 指揮版面 |
|:---:|:---:|
| ![Sideboard](docs/screenshots/03-sideboard.jpg) | ![Command](docs/screenshots/04-sideboard-command.jpg) |

| E-ink / BOOX · 電子紙 |
|:---:|
| ![BOOX](docs/screenshots/05-boox-eink.jpg) |

---

<a id="english"></a>

## English

### What is VibeDeck?

VibeDeck is a **Windows Host** that:

1. Creates a real **virtual display** (Indirect Display Driver)
2. Streams it to a phone over the **LAN** (WebRTC H.264 / JPEG)
3. Offers a phone-sized **Sideboard** (CPU / GPU / weather / work pulse)
4. Shows **AI quotas** (Codex / Claude Code discovery / AGY)

The user-facing name is **VibeDeck**. Internal identifiers still use `PhoneMonitor` (driver, certs, headers, deep links) for compatibility.

### Platform support

| Platform | Client | Notes |
|----------|--------|--------|
| **Windows PC** | Host + optional IDD driver | Required |
| **iPhone** | Safari / Add to Home Screen **only** | WebRTC H.264 + JPEG fallback. **No native iOS app** |
| **Android** | PWA and/or APK under `apps/android` | Native keep-awake + MediaCodec H.264 |
| **BOOX / e-ink** | Browser or Android path | Same Host protocol |

### Features

- LAN pairing with QR + PC approval, device tokens, revoke list
- Local HTTPS (`:5443`) with auto-generated root cert for Wake Lock / PWA
- JPEG WebSocket fallback; WebRTC H.264 when FFmpeg is available
- Sideboard telemetry owned by the Host (no external dashboard required)
- AI quota page with AGY OAuth (credentials **not** in the repo)
- Android native shell: deep links, Deck Window, H.264 viewer

### Quick start

```powershell
# From repo root
scripts\dev-run.ps1
```

Or:

```powershell
dotnet build PhoneMonitor.sln
src\PhoneMonitor.Host\bin\Debug\netcoreapp3.1\PhoneMonitor.Host.exe --urls http://0.0.0.0:5000
```

| URL | Use |
|-----|-----|
| `http://127.0.0.1:5000` | PC local |
| `http://<PC-LAN-IP>:5000` | Phone HTTP bootstrap |
| `https://<PC-LAN-IP>:5443` | Phone HTTPS (after trusting cert) |

### iPhone setup (canonical path)

1. Open Host **HTTP** → install `phone-monitor-root.cer`
2. iOS Settings → General → About → Certificate Trust Settings → enable full trust
3. Open Host **HTTPS** → pair once on the PC
4. Share → **Add to Home Screen**
5. Use **Display** (WebRTC H.264) and turn **Keep awake** on when needed

### Android (optional native)

```powershell
scripts\check-android-toolchain.ps1
scripts\build-android-app.ps1
scripts\install-android-app-dev.ps1
```

Debug APK: `apps\android\app\build\outputs\apk\debug\app-debug.apk`  
Release flow: `docs/android-apk-distribution.md`

### Virtual display driver (optional)

```powershell
scripts\check-driver-toolchain.ps1
scripts\install-driver-toolchain.ps1
scripts\fetch-idd-sample.ps1
scripts\build-driver.ps1
scripts\install-driver-dev.ps1
```

Windows then exposes **PhoneMonitor Display** in Settings.

### AGY OAuth credentials (local only)

Never committed. Configure either:

- Env: `AGY_GOOGLE_CLIENT_ID` / `AGY_GOOGLE_CLIENT_SECRET`
- Or file: `%LOCALAPPDATA%\PhoneMonitor\secrets\agy-google-oauth.json`

Template: [`docs/agy-google-oauth.example.json`](docs/agy-google-oauth.example.json)

### Security (LAN product)

- Host binds `0.0.0.0` so phones can connect on your LAN
- Pairing + device/action tokens protect non-loopback clients
- **Do not** expose the Host port on the public internet
- Release keystores and OAuth secrets stay on your machine

### Docs

| Doc | Topic |
|-----|--------|
| [docs/protocol.md](docs/protocol.md) | Pairing & streams |
| [docs/https-onboarding.md](docs/https-onboarding.md) | Cert / HTTPS |
| [docs/mobile-app.md](docs/mobile-app.md) | PWA + Android |
| [docs/product-vision.md](docs/product-vision.md) | Product direction |
| [docs/remote-desktop-streaming.md](docs/remote-desktop-streaming.md) | H.264 / WebRTC notes |
| [docs/ai-quota-sources.md](docs/ai-quota-sources.md) | Quota providers |

### License

[MIT](LICENSE)

---

<a id="中文"></a>

## 中文

### VibeDeck 是什麼？

VibeDeck 是跑在 **Windows** 上的 Host，用來：

1. 建立真實的 **虛擬螢幕**（Indirect Display Driver）
2. 透過 **區網** 串流到手機（WebRTC H.264 / JPEG）
3. 提供手機尺寸的 **資訊板**（CPU / GPU / 天氣 / 工作脈搏）
4. 顯示 **AI 額度**（Codex / Claude Code 發現 / AGY）

對外產品名是 **VibeDeck**；驅動、憑證、HTTP header、deep link 等內部相容名稱仍可能是 `PhoneMonitor`。

### 平台支援

| 平台 | 用戶端 | 說明 |
|------|--------|------|
| **Windows PC** | Host + 可選虛擬顯示驅動 | 必要 |
| **iPhone** | **僅** Safari / 加入主畫面 | WebRTC H.264 + JPEG。**沒有 iOS 原生 App** |
| **Android** | PWA 與／或 `apps/android` APK | 原生長亮 + MediaCodec H.264 |
| **BOOX / 電子紙** | 瀏覽器或 Android 路徑 | 同一套 Host 協定 |

### 功能重點

- 區網配對：QR + PC 核准、裝置 token、可撤銷
- 本機 HTTPS（`:5443`）自動憑證，支援長亮 / PWA
- JPEG WebSocket 備援；有 FFmpeg 時走 WebRTC H.264
- 資訊板遙測由 Host 自己收集
- AI 額度頁；AGY OAuth **憑證不進 repo**
- Android 原生殼：deep link、Deck 視窗、H.264 檢視

### 快速啟動

```powershell
# 在 repo 根目錄
scripts\dev-run.ps1
```

或：

```powershell
dotnet build PhoneMonitor.sln
src\PhoneMonitor.Host\bin\Debug\netcoreapp3.1\PhoneMonitor.Host.exe --urls http://0.0.0.0:5000
```

| 網址 | 用途 |
|------|------|
| `http://127.0.0.1:5000` | 本機 PC |
| `http://<電腦區網IP>:5000` | 手機 HTTP 起步 |
| `https://<電腦區網IP>:5443` | 手機 HTTPS（憑證信任後） |

### iPhone 標準路徑（推薦）

1. 用 **HTTP** 開 Host → 安裝 `phone-monitor-root.cer`
2. 設定 → 一般 → 關於本機 → 憑證信任設定 → 開啟完整信任
3. 改開 **HTTPS** → 在 PC 上完成一次配對
4. 分享 → **加入主畫面**
5. 用 **顯示器**（WebRTC H.264），需要時開 **長亮**

### Android（可選原生）

```powershell
scripts\check-android-toolchain.ps1
scripts\build-android-app.ps1
scripts\install-android-app-dev.ps1
```

除錯 APK：`apps\android\app\build\outputs\apk\debug\app-debug.apk`  
發行流程：`docs/android-apk-distribution.md`

### 虛擬顯示驅動（可選）

```powershell
scripts\check-driver-toolchain.ps1
scripts\install-driver-toolchain.ps1
scripts\fetch-idd-sample.ps1
scripts\build-driver.ps1
scripts\install-driver-dev.ps1
```

安裝後 Windows 設定會出現 **PhoneMonitor Display**。

### AGY OAuth 憑證（只放本機）

不會進 git。請設定：

- 環境變數：`AGY_GOOGLE_CLIENT_ID` / `AGY_GOOGLE_CLIENT_SECRET`
- 或檔案：`%LOCALAPPDATA%\PhoneMonitor\secrets\agy-google-oauth.json`

範本：[`docs/agy-google-oauth.example.json`](docs/agy-google-oauth.example.json)

### 安全（區網產品）

- Host 聽 `0.0.0.0`，方便區網手機連
- 配對 + device/action token 保護非本機請求
- **不要**把 Host 埠暴露到公網
- 簽章 keystore、OAuth secret 留在你自己的電腦

### 授權

[MIT](LICENSE)

---

<p align="center">
  Built for desk-side phones · 給桌邊那支閒置手機
</p>
