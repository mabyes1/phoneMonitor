# VibeDeck 發佈檢查表

這份檢查表只管「能不能交付給一般使用者」，不取代功能開發文件。

## 建立一般 Windows 發佈包

```powershell
scripts\package-release.ps1
```

產物會放在 `artifacts/release/`：

- `VibeDeck-<version>-win-x64.zip`：免安裝 .NET SDK，解壓後直接執行。
- 同名 `.sha256`：提供下載後完整性核對。

若要使用 Windows 通知轉送，需另建有封裝身分與通知權限的 MSIX：

```powershell
scripts\package-windows-notifications.ps1
```

## 每次發佈前

1. `dotnet test PhoneMonitor.sln -c Release` 全數通過。
2. `node --check` 驗證 `wwwroot/index.js` 與 `wwwroot/modules/*.js`。
3. 實際啟動發佈資料夾內的 `PhoneMonitor.Host.exe`。
4. PC 開啟首頁，確認 QR Code、HTTPS 狀態與待配對裝置可見。
5. 手機完成一次新配對，確認拒絕、允許、撤銷三條路徑。
6. 手機各開一次顯示器、資訊板、額度、自訂卡片。
7. 在未安裝虛擬顯示驅動的乾淨 Windows 測試「建立虛擬螢幕」：取消 UAC、正常安裝、下載驗證失敗三條路徑。
8. 安裝完成後確認 Windows 顯示設定出現延伸桌面，且 VibeDeck 會自動從空狀態切回畫面。
9. 斷開網路再恢復，確認畫面會重連且不被舊遮罩卡住。
10. 核對 ZIP 的 SHA-256，並把版本與變更寫入 `CHANGELOG.md`。

## 暫不宣稱支援

- 未簽正式憑證的 MSIX 不視為公開安裝包。
- Android 原生殼是 legacy，不列入發佈驗收。
- `driver/PhoneMonitor.Idd` 自有驅動仍是開發模組，不宣稱已完成或可公開安裝。
