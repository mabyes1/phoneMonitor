# VibeDeck

VibeDeck 把 Windows PC 變成手機可用的副螢幕、資訊板與 AI 額度面板。

## 先記住：產品只有這一條路

| 元件 | 唯一正式路線 |
|---|---|
| Windows Host | 使用 `VibeDeck-Setup-<version>.exe` 安裝，登入 Windows 後在目前桌面 Session 背景啟動 |
| Android／iPhone／BOOX | Safari、Chrome 或加入主畫面的 PWA；沒有原生手機 App |
| 虛擬顯示器 | 選用，只供「顯示器」模式；從 PC Web UI 安裝 |
| Windows 通知 | 選用的 MSIX companion；只讀通知並傳給 Host，不是第二個 Host |
| 正式更新 | 直接執行較新版本 Setup 覆蓋更新 |
| 原始碼開發 | `start.bat` 或 `scripts\dev-run.ps1`；不可與已安裝 Host 同時占用連接埠 |

Host **不能註冊成 Windows Service**。服務會進入 Session 0，看不到登入使用者的實體／虛擬顯示器。產品啟動器使用隱藏背景程序，因此不會留下黑色終端機視窗。

## 一、全新安裝

### 使用已建好的 Setup

執行：

```text
VibeDeck-Setup-<version>.exe
```

Setup 會完成：

- 安裝程式到 `C:\Program Files\VibeDeck`。
- 將可保留的產品資料放在 `%ProgramData%\VibeDeck`。
- 建立開始功能表／桌面入口。
- 建立登入後自動啟動，但讓 Host 留在使用者桌面 Session。
- 建立手機透過 LAN 存取 Host 所需的防火牆規則。
- 清除舊版誤裝的 `VibeDeckHost` Windows Service。

安裝後開啟：

```text
http://127.0.0.1:5000
```

### 從原始碼建立 Setup

需要 .NET 8 SDK 與 Inno Setup 6：

```powershell
scripts\package-windows-setup.ps1
```

版本預設讀取 [PhoneMonitor.Host.csproj](src/PhoneMonitor.Host/PhoneMonitor.Host.csproj) 的 `Version`。也可明確指定：

```powershell
scripts\package-windows-setup.ps1 -Version 0.1.1
```

產物：

```text
artifacts\windows-setup\VibeDeck-Setup-<version>.exe
```

只需要建立安裝 payload、暫時不編譯 Setup 時：

```powershell
scripts\package-windows-setup.ps1 -SkipInno
```

`package-windows-setup.ps1` 預設先跑測試、JavaScript 語法、產品路線與 payload 檢查。只有已經另外完成相同驗證時，才使用 `-SkipTests`。

## 二、更新既有安裝

1. 建立版本號較新的 Setup。
2. 直接執行新 Setup，不必先解除安裝。
3. Setup 會停止舊 Host、移除遺留 Service、替換程式與 Web 資產，再於登入桌面啟動新版 Host。
4. `%ProgramData%\VibeDeck` 不會被覆蓋，因此配對裝置、HTTPS 憑證、額度帳號、自訂卡片與通知設定會保留。

更新後執行：

```powershell
scripts\test-product-flow.ps1 -Installed
```

若這台機器已安裝虛擬顯示器：

```powershell
scripts\test-product-flow.ps1 -Installed -RequireVirtualDisplay
```

這會確認：

- 舊 Windows Service 不存在。
- Host 正在非 Session 0 的登入桌面執行。
- 5000 連接埠由正確 Host 擁有。
- Host 沒有錯把 Session 0 的 `WinDisc` 當成桌面。
- 選用檢查可找到 PhoneMonitor 虛擬顯示器。

## 三、手機連線與配對

手機與 PC 在同一 Wi-Fi 時，用 Safari／Chrome 開啟 PC 顯示的 HTTPS 網址，例如：

```text
https://192.168.1.20:5443
```

第一次使用：

1. 手機提出配對申請。
2. PC 核對裝置名稱與六位數驗證碼。
3. PC 按「允許」。
4. 手機可在「顯示器／資訊板／額度」間切換。

資訊板與額度不需要虛擬顯示器。iPhone、Android 與 BOOX 使用同一份 Host Web/PWA；平台差異只由 responsive／e-ink UI 處理。

不同網路建議使用 Tailscale，說明見 [docs/remote-access.md](docs/remote-access.md)。HTTPS 與 iPhone 憑證流程見 [docs/https-onboarding.md](docs/https-onboarding.md)。

## 四、虛擬顯示器

只有「顯示器」模式需要 PhoneMonitor Display。

在 PC Web UI 顯示找不到虛擬螢幕時，按「建立虛擬螢幕」並接受 UAC。一般使用者不需要 WDK、測試簽章或自行跑驅動開發腳本。

Host 必須在本機登入桌面 Session 執行。若 API 只回傳 `WinDisc 1024×768`，代表 Host 被錯誤放在 Session 0，**不代表驅動沒安裝**；先跑產品流程檢查，不要先重灌驅動。

自有驅動開發工具仍位於 `driver/` 與 `scripts/*driver*.ps1`，但不屬於一般產品安裝流程。

## 五、Windows 通知 companion（選用）

Windows 的 `userNotificationListener` 必須有封裝身分，因此通知使用獨立 MSIX companion。後端資料來源仍與 Host 分開，前端才合併成「活動動態」。

開發簽章安裝／更新：

```powershell
scripts\package-windows-notifications.ps1 -Install
```

若 Windows 要求機器層級信任開發憑證，使用系統管理員 PowerShell：

```powershell
scripts\package-windows-notifications.ps1 -RegisterOnly -InstallCertificateMachine
```

程序名稱應為 `VibeDeck.Notifications.exe`。它不應監聽 5000／5443，也不要把它當成另一個 Host 除錯。詳細說明見 [docs/windows-notifications.md](docs/windows-notifications.md)。

## 六、原始碼開發

需要 .NET 8 SDK：

```text
start.bat
```

或：

```powershell
scripts\dev-run.ps1
```

開發資料位於 `%LocalAppData%\PhoneMonitor`；正式安裝資料位於 `%ProgramData%\VibeDeck`。開發前若 5000 已被安裝版占用，先關閉安裝版 Host。通知 companion 可以保持執行。

完整來源檢查：

```powershell
scripts\test-product-flow.ps1 -Source
```

## 七、解除安裝

優先使用 Windows「已安裝的應用程式」移除 VibeDeck，或在系統管理員 PowerShell 執行：

```powershell
scripts\uninstall-windows-product.ps1
```

保留 `%ProgramData%\VibeDeck`：

```powershell
scripts\uninstall-windows-product.ps1 -KeepData
```

通知 companion 為獨立 MSIX；需要一併移除時：

```powershell
scripts\package-windows-notifications.ps1 -Uninstall
```

## 故障判斷

| 現象 | 先做什麼 |
|---|---|
| PC 頁面打不開 | 點 VibeDeck 圖示，再跑 `scripts\test-product-flow.ps1 -Installed` |
| 虛擬顯示器明明安裝卻找不到 | 查 Host Session；API 出現 `WinDisc` 時不要重灌驅動 |
| 手機無法連線 | 確認同一 Wi-Fi／Tailscale、PC 防火牆及 HTTPS 網址 |
| 手機 UI 看起來像舊 App | 關掉錯誤程式，只用 Safari／Chrome／PWA 開 Host URL |
| Windows 通知沒進活動動態 | 查 companion 是否 `Connected / Allowed`，不要把通知與 Host 程序混為一談 |
| 更新後資料消失 | 正式資料應在 `%ProgramData%\VibeDeck`；不要用開發模式資料路徑判斷產品資料 |
| 額度沒有資料 | 額度讀 Host PC 的本機帳號資料；先在同一台 PC 使用對應 CLI／帳號 |

## 專案結構

- `src/PhoneMonitor.Host`：Windows Host、API、串流與手機 Web/PWA。
- `packaging/windows-setup`：唯一正式 Windows Setup。
- `packaging/windows-notifications`：Windows 通知 companion MSIX。
- `scripts/test-product-flow.ps1`：來源、payload、已安裝產品的共同檢查。
- `driver/`：虛擬顯示器開發模組。
- `docs/`：協定、遠端連線、HTTPS、通知與發佈文件。
- [AGENTS.md](AGENTS.md)：後續開發與除錯不可違反的產品路線規則。

## Release checklist

正式交付前依序執行：

```powershell
scripts\test-product-flow.ps1 -Source
scripts\package-windows-setup.ps1
# 執行新 Setup 完成全新安裝或覆蓋更新
scripts\test-product-flow.ps1 -Installed
```

完整人工檢核見 [docs/release-checklist.md](docs/release-checklist.md)。

## OpenAI Build Week

VibeDeck entered OpenAI Build Week as an existing Windows virtual-display prototype. During the submission period, it was meaningfully extended into a more complete product workflow with Codex and GPT-5.6.

Build Week work includes:

- redesigned responsive phone and e-paper interfaces, including rotation and overlay fixes;
- a Windows Setup and upgrade path that preserves product data and starts the Host in the signed-in desktop session;
- improved virtual-display discovery and setup guidance;
- a single browser/PWA product path for iPhone, Android, and BOOX devices;
- configurable dashboard layouts, activity updates, quota cards, and optional Windows notification integration;
- installed-product and source-product flow checks for packaging and release verification.

Codex was used as an engineering partner for product planning, implementation, debugging, review, testing, and packaging. GPT-5.6 helped reason across Windows desktop-session behavior, display enumeration, browser media constraints, mobile/e-paper layouts, and installer lifecycle. Dated commits and Codex session logs document the work completed during the event.

The current interface is primarily available in Traditional Chinese. Planned work includes a maintainable internationalization layer, beginning with English and expanding to additional languages.

## License

[MIT](LICENSE)
