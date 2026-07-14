# VibeDeck

把 Windows PC 變成手機可以使用的副螢幕與桌邊資訊板。

手機端只有一條正式路徑：用 Safari、Chrome 或 PWA 開啟 Host 網頁。不需要安裝手機 App，也不需要先安裝虛擬顯示驅動才能試用資訊板。

## 新手：第一次使用

### 你需要

- Windows PC
- 手機與 PC 在同一個 Wi‑Fi，或兩邊都登入同一個 Tailscale Tailnet
- 虛擬顯示驅動只在你要使用「顯示器」模式時需要；資訊板與額度不需要

如果你拿到的是發佈 ZIP，解壓後直接執行 `PhoneMonitor.Host.exe`，不需要安裝 .NET SDK。

如果你是從原始碼啟動，才需要 .NET 8 SDK，並使用下方的 `start.bat`。

### 1. 啟動 PC Host

發佈 ZIP：雙擊 `PhoneMonitor.Host.exe`。

原始碼版本：在 Repo 根目錄雙擊：

```text
start.bat
```

或在 PowerShell 執行：

```powershell
scripts\dev-run.ps1
```

不要關閉這個視窗；它就是正在執行的 VibeDeck Host。

### 2. 先在 PC 確認頁面

用 PC 瀏覽器開啟：

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

## 建立虛擬螢幕（顯示器模式）

只有「顯示器」模式需要這個步驟。當頁面顯示「這台電腦還沒有虛擬螢幕」時，在 PC 的 VibeDeck 頁面按「建立虛擬螢幕」，再接受一次 Windows 管理員確認。VibeDeck 會下載已簽章的 Virtual Display Driver、驗證版本與完整性，並等待 Windows 建立新的延伸桌面。

這個動作只能從 PC 本機頁面開始；已配對手機不能遠端觸發管理員安裝。安裝時需要網路，但不需要 WDK、不會開啟測試簽章模式，通常也不必重新開機。使用的第三方元件與固定雜湊見 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

VibeDeck Host 必須在 PC 的本機 Windows 桌面工作階段執行。若透過 Windows 遠端桌面（RDP）啟動，Windows 會改用 RDP 顯示驅動，Host 看不到真正的虛擬螢幕；Web 會直接提示回到本機桌面重新啟動。

### 自有驅動開發（非一般使用者流程）

`driver/PhoneMonitor.Idd` 仍是開發中的自有驅動；下列命令只供驅動開發與測試，不是產品安裝流程：

```powershell
scripts\check-driver-toolchain.ps1
scripts\install-driver-toolchain.ps1
scripts\fetch-idd-sample.ps1
scripts\build-driver.ps1
scripts\install-driver-dev.ps1
```

這條開發流程可能需要系統管理員權限、測試簽章與重新開機。一般使用者不要執行。

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
| PC 頁面打不開 | 確認 `start.bat` 視窗仍在執行，並開 `http://127.0.0.1:5000`。 |
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
- [docs/release-checklist.md](docs/release-checklist.md)
- [docs/custom-data-sources-spec.md](docs/custom-data-sources-spec.md)
- [docs/remote-access.md](docs/remote-access.md)
- [docs/https-onboarding.md](docs/https-onboarding.md)
- [docs/mobile-app.md](docs/mobile-app.md)
- [docs/protocol.md](docs/protocol.md)
- [docs/remote-desktop-streaming.md](docs/remote-desktop-streaming.md)
- [docs/windows-virtual-display.md](docs/windows-virtual-display.md)

## English quick start

1. On Windows, run `start.bat` from the repository root.
2. Open `http://127.0.0.1:5000` on the PC.
3. Open the PC's LAN or Tailscale URL in Safari/Chrome on the phone.
4. Approve the six-digit pairing request on the PC.
5. Use Sideboard first; install the optional virtual display driver only for Display mode.

The phone client is browser/PWA-only. WebRTC H.264 is preferred, with JPEG fallback.

## License

[MIT](LICENSE)
