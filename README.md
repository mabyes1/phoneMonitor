# VibeDeck

把 Windows PC 變成手機可以使用的副螢幕與桌邊資訊板。

手機端只有一條正式路徑：用 Safari、Chrome 或 PWA 開啟 Host 網頁。不需要安裝手機 App，也不需要先安裝虛擬顯示驅動才能試用資訊板。

## 新手：第一次使用

### 你需要

- Windows 10/11 PC（x64）
- 手機與 PC 在同一個 Wi‑Fi，或兩邊都登入同一個 Tailscale Tailnet
- 虛擬顯示驅動只在你要使用「顯示器」模式時需要；資訊板與額度不需要
- **產品安裝不需要**安裝 .NET SDK（Setup 已內含 self-contained Host）

### 1. 安裝 VibeDeck（推薦）

若你已有發行檔，雙擊：

```text
artifacts\windows-setup\VibeDeck-Setup-0.1.0.exe
```

或從本機原始碼重新打包後再安裝：

```powershell
scripts\package-windows-setup.ps1 -Version 0.1.0
# 需要 Inno Setup 6：winget install JRSoftware.InnoSetup
# 或加 -InstallInno 自動安裝編譯器
```

Setup 會完成一條龍：

| 項目 | 說明 |
|------|------|
| 程式檔 | 安裝到 `C:\Program Files\VibeDeck` |
| 背景服務 | Windows Service **VibeDeck Host**（`VibeDeckHost`）自動啟動 |
| 桌面／開始功能表圖示 | 點開進入 PC 端 Web UI（`http://127.0.0.1:5000`） |
| 資料目錄 | `%ProgramData%\VibeDeck`（憑證、配對裝置、額度快取等） |

安裝後不需再留著黑視窗；開機後服務會自己起來。點 **VibeDeck** 圖示就是開 Web 控制台。

開發／原始碼直接跑仍可用：

```text
start.bat
```

或：

```powershell
scripts\dev-run.ps1
```

（開發模式需要 .NET 6 SDK，且要保持終端機視窗開啟。）

### 2. 先在 PC 確認頁面

用桌面圖示開啟，或瀏覽器開：

```text
http://127.0.0.1:5000
```

看到 VibeDeck 頁面，就代表 Host 已經啟動。

### 3. 用手機開啟

手機與 PC 在同一個 Wi‑Fi 時，使用啟動視窗列出的 PC 網址，例如：

```text
http://192.168.1.20:5000
```

也可以直接用 PC 頁面上的 QR Code。配對 QR 只會開啟 Host 網址，不會直接授權手機；手機只需要瀏覽器，不需要 APK。

### 4. 完成一次配對

手機第一次開啟後，請在手機頁面按「提出配對申請」；PC 頁面會出現等待中的手機與六位數驗證碼：

1. 確認名稱與驗證碼是自己的手機。
2. 在 PC 按「允許」。
3. 手機等待結果；PC 允許後才會取得裝置權限。

如果沒有出現等待項目，確認手機開的是 `https://<PC-IP>:5443`，再回手機按一次「提出配對申請」。

### 5. 先試資訊板

手機點「資訊板」即可確認基本連線。這個模式不需要虛擬顯示驅動。

「顯示器」模式需要 Windows 看得到 **PhoneMonitor Display**。驅動安裝放在下面的進階設定，不影響先試用資訊板。

## 連線方式

### 同一個 Wi‑Fi

使用：

```text
http://<PC 的區網 IP>:5000
```

Android Chrome 可以先用 HTTP 查看頁面；正式配對一律使用 HTTPS。iPhone 若要使用 WebRTC、長亮與加入主畫面，也請使用 HTTPS。

### 不同網路：推薦 Tailscale

在 PC 與手機都安裝 Tailscale，登入同一個帳號／Tailnet。PC 執行：

```powershell
tailscale ip -4
```

假設得到 `100.71.158.38`，手機開：

```text
http://100.71.158.38:5000
```

不需要 Port Forwarding，也不需要 Subnet Router。Tailscale 只負責安全網路通道，VibeDeck 的第一次配對仍然要在 PC 按「允許」。

若你要把 Host 暴露到 Tailscale 以外的 Internet，再設定額外的遠端密碼與 HTTPS：

```powershell
$env:PHONEMONITOR_REMOTE_PASSWORD = "請換成長且唯一的密碼"
scripts\dev-run.ps1
```

完整說明：[docs/remote-access.md](docs/remote-access.md)

## HTTPS 與 iPhone

Host 啟動時會自動建立本機 HTTPS 憑證，網址是：

```text
https://<PC 的 IP>:5443
```

第一次使用 iPhone：

1. 先用 HTTP 開啟 Host。
2. 點「安裝 HTTPS 憑證」下載 `phone-monitor-root.cer`。
3. 在 iPhone 設定中安裝並完整信任根憑證。
4. 改開 HTTPS 網址。
5. 分享 → 加入主畫面。

Android 也可以安裝憑證來使用 HTTPS；若只是測試資訊板，通常可先用 HTTP。

詳細說明：[docs/https-onboarding.md](docs/https-onboarding.md)

## 虛擬顯示器驅動（選用）

只有「顯示器」模式需要這個步驟。它會讓 Windows 出現 **PhoneMonitor Display**，手機才能接收真正的延伸桌面。

```powershell
scripts\check-driver-toolchain.ps1
scripts\install-driver-toolchain.ps1
scripts\fetch-idd-sample.ps1
scripts\build-driver.ps1
scripts\install-driver-dev.ps1
```

這是 Windows 驅動開發流程，可能需要系統管理員權限、測試簽章與重新開機。只是想看資訊板時可以先跳過。

## 額度功能（選用）

額度資料在跑 Host 的 PC 上讀取，手機只負責顯示。

### Codex

1. 在同一台 PC 登入 Codex。
2. 使用一次 Codex，讓本機 session 產生 `rate_limits`。
3. 開啟 VibeDeck →「額度」→「Codex」→ 重新整理。

VibeDeck 不會要求你貼 Codex token，也沒有 Codex OAuth 匯入按鈕。

### AGY

請先在 Host PC 設定 Google OAuth，方式見：[docs/agy-google-oauth.example.json](docs/agy-google-oauth.example.json)。

## 常見問題

| 問題 | 處理方式 |
|---|---|
| PC 頁面打不開 | 產品版：在「服務」確認 **VibeDeck Host** 正在執行，再點桌面圖示或開 `http://127.0.0.1:5000`。開發版：確認 `start.bat` 視窗仍在執行。 |
| 手機連不到 | 確認手機與 PC 在同一 Wi‑Fi，或兩邊 Tailscale 都顯示在線；也可手動輸入 PC IP。 |
| 手機一直等待配對 | 回 PC 看六位數驗證碼並按「允許」；不要重複使用過期 QR。 |
| iPhone 顯示憑證錯誤 | 重新下載根憑證，並在 iPhone「憑證信任設定」開啟完整信任。 |
| 顯示器畫面是空的 | 先確認 Windows 已安裝並啟用 PhoneMonitor Display 驅動；資訊板不需要驅動。 |
| WebRTC 沒有成功 | 使用 HTTPS；若瀏覽器或 FFmpeg 不支援，VibeDeck 會自動回退 JPEG。 |
| 額度沒有資料 | 額度讀的是 Host PC 本機資料，先在同一台 PC 登入並使用 Codex／設定 AGY。 |

## 專案內容

- `src/PhoneMonitor.Host`：PC Host 與手機 Web/PWA
- `driver/`：選用的 Windows 虛擬顯示驅動
- `scripts/`：啟動、HTTPS、驅動與開發工具腳本
- `docs/`：進階協定、串流、HTTPS、遠端存取與產品文件
- `apps/android/`：舊版 Android 原生殼的 legacy 原始碼，不是目前支援的產品路徑

## 進階文件

- [CHANGELOG.md](CHANGELOG.md)
- [docs/custom-data-sources-spec.md](docs/custom-data-sources-spec.md)
- [docs/remote-access.md](docs/remote-access.md)
- [docs/https-onboarding.md](docs/https-onboarding.md)
- [docs/mobile-app.md](docs/mobile-app.md)
- [docs/protocol.md](docs/protocol.md)
- [docs/remote-desktop-streaming.md](docs/remote-desktop-streaming.md)
- [docs/windows-virtual-display.md](docs/windows-virtual-display.md)

## 打包 Setup 安裝檔

```powershell
# 產物：artifacts\windows-setup\VibeDeck-Setup-<version>.exe
scripts\package-windows-setup.ps1 -Version 0.1.0
```

- 需要 [Inno Setup 6](https://jrsoftware.org/isinfo.php)（`ISCC.exe`）。沒裝可加 `-InstallInno`。
- 只發布 payload、不編譯 Setup：`-SkipInno`，再用系統管理員跑 `scripts\install-windows-product.ps1`。
- 移除產品安裝：`scripts\uninstall-windows-product.ps1`（或用「新增或移除程式」）。

## English quick start

1. Prefer `artifacts\windows-setup\VibeDeck-Setup-*.exe`, or build it with `scripts\package-windows-setup.ps1`. For source dev, run `start.bat` from the repository root.
2. Open `http://127.0.0.1:5000` on the PC.
3. Open the PC's LAN or Tailscale URL in Safari/Chrome on the phone.
4. Approve the six-digit pairing request on the PC.
5. Use Sideboard first; install the optional virtual display driver only for Display mode.

The phone client is browser/PWA-only. WebRTC H.264 is preferred, with JPEG fallback.

## License

[MIT](LICENSE)
