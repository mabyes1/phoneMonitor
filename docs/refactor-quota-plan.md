# AiQuotaService 去耦內聚重構計畫

> 目標：把 `Quotas/AiQuotaService.cs`（2,016 行 / 82KB）從「7 個責任擠一個類別」拆成 7 個內聚單元。
> 原則：每個檔案 = 一個改動理由，grep 進去就是全部真相，不需跨檔追邏輯（不做千層麵）。
> 硬約束：**行為零改變**。這是純結構重構，對外的 `GetSnapshotAsync` / `RefreshSnapshotAsync` 簽名不動，API endpoint 不動，快取檔案格式不動。

---

## 現況診斷

一個 `AiQuotaService` 同時是：OAuth client + 帳號 store + CLI launcher + JWT/identity reader + 快取層 + DPAPI 加密 + 額度讀取器。真正「讀額度」只佔約 20%。

共用可變狀態（拆分時的地雷，先盤點）：
- `agyOAuthLock`、`agyOAuthSessions`（只被 OAuth 流程用 → 跟著 OAuth client 走）
- `SharedHttpClient`（被 OAuth refresh + quota API 共用 → 放共用層或注入）
- `AgyTokenEntropy`（只被 ProtectSecret/ReadProtectedSecret 用 → 跟著 crypto 走）
- 一堆 `static` JSON helper（TryGetString / TryGetProperty / FindJsonFiles / SafeFileName …）→ 抽 internal static helper 類別

---

## 目標檔案結構（`Quotas/` 底下新增子命名空間）

| 新檔案 | 責任 | 搬進去的方法 |
|---|---|---|
| `AiQuotaService.cs`（瘦身後保留） | **只做編排** | `GetSnapshotAsync`、`RefreshSnapshotAsync`、`BuildSnapshotAsync`（呼叫下面各 provider 組裝 snapshot） |
| `Codex/CodexQuotaReader.cs` | Codex 額度讀取 | `ReadCodexQuotas`、`ReadCodexQuota`、`TryReadCodexQuotaFromFile`、`IsUsableQuota`、`BuildCodexStatusId` |
| `Codex/CodexIdentityReader.cs` | Codex JWT / 身份 | `ReadCodexAuthIdentities`、`TryReadCodexIdentityFromAuthFile`、`ReadCodexIdentityFromJwt`、`MergeCodexIdentity`、`SameCodexAccount`×2、`ApplyCodexIdentity`、`BuildCodexPlaceholder`、`IsCodexQuotaCompatibleWithIdentity` |
| `Codex/CodexQuotaCache.cs` | Codex 快取讀寫 | `ReadCodexQuotaCache`、`WriteCodexQuotaCache`、`CodexQuotaCacheDirectory` |
| `Agy/AgyOAuthClient.cs` | AGY OAuth 全流程 | `StartAgyOAuth`、`CompleteAgyOAuthAsync`、`ExchangeAgyAuthorizationCodeAsync`、`RefreshAgyAccessTokenAsync`、`WarmAgyLoadCodeAssistAsync`、`RequireAgyGoogleOAuthClient`、`agyOAuthLock`、`agyOAuthSessions`、`AgyGoogleOAuthClient`、OAuth 相關 const（scope/client id/secret env/user agent） |
| `Agy/AgyAccountStore.cs` | AGY 帳號 CRUD + 匯入 | `ReadPhoneMonitorAgyAccounts`、`WriteAgyAccountToken`、`ResolveAgyAccountTokenFile`、`FindPhoneMonitorAgyAccount`、`FindMatchingAgyAccountFiles`、`ImportAgyAccountsFromAntigravity`×2、`DeleteAgyAccount`、`DeleteCodexAccount`、`AccountMatches` |
| `Agy/AgyQuotaReader.cs` | AGY 額度讀取 | `ReadAgyQuotasAsync`、`ReadAgyQuotasFromAuthorizedCache`、`RefreshAgyQuotasAsync`、`RetrieveAgyQuotaSummaryAsync`、`ReadAgyCacheBuckets`、`ReadAgyCacheBucket`、`BuildAgyStatusFromBucketInfo`、`BuildAgyStatusFromBucketInfo`、`MergeAgyStatuses` |
| `Agy/AgyQuotaCache.cs` | AGY 快取 | `ResolveAgyCacheFile`、`WriteAgyQuotaCache`、`IsAgyCacheFresh`、`FindMatchingAgyCacheFiles`、`AgyQuotaCacheDirectory` |
| `Agy/AgyCliLauncher.cs` | AGY CLI 啟動 | `OpenAgyCli`、`WriteAgyCliLauncher`、`AgyLauncherDirectory`、`EscapeBatchValue` |
| `SecretProtector.cs` | DPAPI 加解密 | `ProtectSecret`、`ReadProtectedSecret`、`AgyTokenEntropy` |
| `QuotaJsonHelpers.cs`（internal static） | 共用 JSON/檔案 helper | `TryGetString`、`TryGetProperty`、`TryGetUnixTimeMilliseconds`、`FindJsonFiles`、`SafeFileName`、`ReadTailLines`、`Unavailable`、`TryDeleteFile` |
| `QuotaPaths.cs`（internal static） | 路徑集中 | `PhoneMonitorQuotaRoot`、`AgyExecutablePath`、`AgyAccountStoreDirectory` … 所有 `*Directory()` |

拆完預估：編排本體 ~150 行，其餘每檔 150–400 行。沒有一個檔案超過 400 行。

---

## DI 接線（`Startup.cs` ConfigureServices）

現在只有 `services.AddSingleton<AiQuotaService>();`。拆完新增：

```csharp
services.AddSingleton<SecretProtector>();
services.AddSingleton<CodexQuotaReader>();
services.AddSingleton<CodexIdentityReader>();
services.AddSingleton<CodexQuotaCache>();
services.AddSingleton<AgyOAuthClient>();
services.AddSingleton<AgyAccountStore>();
services.AddSingleton<AgyQuotaReader>();
services.AddSingleton<AgyQuotaCache>();
services.AddSingleton<AgyCliLauncher>();
// AiQuotaService 建構子改注入上述依賴
```

Endpoint（`Startup` 裡呼叫 quota 的地方）只跟瘦身後的 `AiQuotaService` 對話，不直接碰新類別 → 對外零改動。

---

## 執行順序（每步可獨立 build + test，隨時能停）

1. **抽 helper（零風險）**：先把 `QuotaJsonHelpers` + `QuotaPaths` + `SecretProtector` 抽出來，原類別改呼叫。build 綠 → commit。
2. **抽 Codex 三兄弟**：`CodexQuotaCache` → `CodexIdentityReader` → `CodexQuotaReader`。每抽一個 build 一次。
3. **抽 AGY**：`AgyQuotaCache` → `AgyAccountStore` → `AgyCliLauncher` → `AgyOAuthClient` → `AgyQuotaReader`（OAuth 先於 QuotaReader，因為 reader 依賴它）。
4. **瘦身 AiQuotaService**：只留 3 個編排方法 + 建構子注入。
5. **接 DI**：更新 ConfigureServices。
6. **全綠驗證**：`scripts\test-product-flow.ps1 -Source` + 手動開一次 quota 卡確認數字沒變。

---

## 驗證 / 回滾

- 每步都在 `refactor/quota-split` 分支，master 保持可交件。
- 每個 commit 都能 build。任何一步壞掉 → `git reset` 回上一個綠 commit。
- 驗收 = 重構前後同一台機器，quota 卡顯示的 Codex / AGY Claude / AGY Gemini 數字完全一致。
- **補測試機會**：拆完後 `SecretProtector`（DPAPI round-trip）、`CodexIdentityReader`（JWT 解析）、`AgyQuotaReader`（bucket 解析）各補一個單元測試——這三個現在零測試，是安全/正確性最痛的點。

---

## 不做的事（避免 scope creep）

- 不改快取檔案格式 / 路徑（會讓現有使用者的 cache 失效）。
- 不改 OAuth 流程邏輯，只搬家。
- 不動 endpoint 路由。
- 不順手改 naming / 重排無關 code。
