# 更新日誌

## 2026-07-14

### 電子書版自動辨識 + 手動切換

- BOOX NeoBrowser 常送出一般 Android Chrome UA（沒有 `BOOX`／`ONYX`），導致 `isEinkClient()` 失敗、停在一般手機版。
- 電子書判定改為：`?eink=1` → localStorage 偏好 → cookie 備援 → UA（BOOX／ONYX／VibeDeck-EInk）→ 面板解析度啟發式。
- 自動／手動進入電子書後會**黏住偏好**（localStorage + cookie），PWA 主畫面啟動（沒有 `?eink=1`）仍開紙面版。
- 頂部 **電子書** 按鈕；開啟時預設進資訊板；未配對時仍顯示配對區塊。
- **全螢幕**：資訊板／額度也會呼叫 Fullscreen API（不再只限顯示器模式），並用 `viewer-immersive` 吃滿 100dvh。
- 電子書版隱藏 **顯示／串流／解析度** 三組設定（無虛擬螢幕不需要）。
- 電子書資訊板改**固定單欄紙面堆疊**（不再套桌面三欄），避免卡片擠在一起、底部被裁切；數字與內文可完整顯示並可捲動。

### 產品安裝一條龍（Setup）

- 新增 Windows Setup 打包：`scripts\package-windows-setup.ps1` → `VibeDeck-Setup-<version>.exe`（Inno Setup）。
- 安裝後註冊 **VibeDeck Host** Windows Service（開機自動啟動），不再依賴 `start.bat` 黑視窗當正式入口。
- 桌面／開始功能表 **VibeDeck** 圖示會啟動服務（若需要）並開啟 PC 端 Web UI（`http://127.0.0.1:5000`）。
- Host 支援 `UseWindowsService`；產品安裝資料目錄為 `%ProgramData%\VibeDeck`。
- 備援腳本：`scripts\install-windows-product.ps1` / `uninstall-windows-product.ps1`（無 Inno 時可直接裝 payload）。

## 2026-07-12

### 簡化手機使用流程

- 移除 APK 下載、原生 App 入口、原生 QR 與原生 H.264 WebSocket 等不必要流程。
- 以瀏覽器／PWA 作為 iPhone、Android 與 BOOX 的唯一正式手機入口。
- 行動瀏覽器預設優先使用 WebRTC H.264，無法使用時自動回退 JPEG。
- 使用 HTTPS Host QR → 手機提出申請 → PC 允許／拒絕的配對流程；保留 Tailscale 跨網路連線與可選的 Host 密碼登入。
- 簡化 HTTPS、配對與首次連線說明，讓使用者能更快完成設定並開始使用。
- Android 原生殼保留為 legacy 原始碼，不再列入 Host UI、文件或發行流程。

### 新手入口整理

- README 改成從「啟動 Host → 開啟手機網址 → 配對 → 先試資訊板」開始。
- `start.bat`／`dev-run.ps1` 會直接列出手機可用的 HTTP／HTTPS 網址。
- 明確區分必要設定與選用功能：虛擬顯示驅動、HTTPS、Tailscale 與 AI 額度不再混在第一次啟動流程。

### 穩定性

- AGY 額度刷新遇到單一帳號操作錯誤時，會繼續處理其他帳號。

持續方向：減少不必要的選項與設定成本，讓 VibeDeck 更快上手，並以最少步驟提供最好的使用體驗。
