# VibeDeck 產品更新

## 使用者流程

1. 在 **PC 本機** 開啟 VibeDeck，不在手機或遠端瀏覽器操作。
2. 按右上角「檢查更新」。
3. 有新版時，按「安裝 vX.Y.Z」。
4. 等待下載與 SHA-256 驗證完成；Windows Setup 會自行開啟。
5. 若 Windows 要求管理員權限，按「是」。Setup 會短暫停止 Host、更新檔案、保留 `%ProgramData%\VibeDeck` 的配對與版面資料，最後重新啟動 Host。

更新不是靜默安裝：使用者必須在 PC 上主動確認，且 Windows 仍會保有它應有的權限提示。

## 信任範圍

- 只查詢 `https://api.github.com/repos/mabyes1/phoneMonitor/releases/latest`。
- 只接受 `github.com/mabyes1/phoneMonitor` Release 的 `VibeDeck-Setup-X.Y.Z.exe` 與同名 `.sha256`。
- 安裝檔下載完成後必須通過 SHA-256 比對，才會啟動 Setup。
- 更新 API 只接受 `127.0.0.1` 本機請求與 VibeDeck 動作權杖；已配對手機、LAN 裝置與遠端登入不能觸發 PC 更新。

SHA-256 保護傳輸與發佈資產的一致性；正式商業發佈前仍應設定 Windows 程式碼簽章，降低 SmartScreen 的未知發行者提示。

## 發佈者流程

1. 更新 `src/PhoneMonitor.Host/PhoneMonitor.Host.csproj` 的版本與 `CHANGELOG.md`。
2. 提交後建立並推送符合版本的 tag，例如 `v0.1.18`。
3. GitHub Actions `Release Windows Setup` 會建置單一 `VibeDeck-Setup-X.Y.Z.exe`、生成 `.sha256`，並建立 GitHub Release。
4. 使用者之後按「檢查更新」即可取得該 Release；不必下載或執行任何 `.ps1`、`.vbs` 或 `.cmd`。

## 語言規則

- Windows Setup 依 Windows 顯示語言自動預選繁體中文、English 或日本語；不支援的語言回退英文。
- PC、手機與 BOOX 的網頁 App 各自依該瀏覽器語言選擇介面，仍可在右上角手動切換。
- 因此 PC 用繁中、手機用英文或日文是正常且預期的行為。
