# Windows 通知橋接

Windows Notification Listener 需要應用程式套件宣告 `userNotificationListener` capability，並取得使用者授權。一般的 `dotnet run`／`start.bat` 是非封裝模式，所以資訊面板會顯示「需要 MSIX」而不會假裝已經在監聽。

## 本機開發啟用

在 PowerShell 執行：

```powershell
.\scripts\package-windows-notifications.ps1 -RegisterOnly
```

這會發布 x64 Host、建立簽署過的完整 MSIX，並安裝到目前使用者。接著從 Windows 開始功能表啟動 PhoneMonitor，再於 PC 本機資訊面板的「自訂資訊」頁按「啟用 Windows 通知」。不要用一般 `dotnet run` 測試通知權限。

腳本會自動檢查已安裝版本並遞增套件版本，讓程式內容更新時可以直接重新安裝。

如果安裝時看到 `0x800B0109` 或「certificate chain root must be trusted」，代表這台電腦的 Windows 部署服務需要系統層級信任開發憑證。請用「系統管理員」PowerShell 重新執行：

```powershell
.\scripts\package-windows-notifications.ps1 -RegisterOnly -InstallCertificateMachine
```

這個開關只會把本專案的開發憑證匯入 `LocalMachine\TrustedPeople`，不會替其他憑證放寬信任設定。

如果目前有一般模式的 Host 在跑，先停止它再啟動上述發布版，避免單例鎖讓新版本直接退出。

若要移除開發註冊：

```powershell
.\scripts\package-windows-notifications.ps1 -Uninstall
```

第一次啟用時 Windows 會要求通知存取權。新通知會進入內建的「Windows 通知」訊息卡片；啟用時既有通知只建立基準，不會把歷史通知全部灌進卡片。
