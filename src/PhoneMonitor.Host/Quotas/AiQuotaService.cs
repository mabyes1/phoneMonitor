using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneMonitor.Host.Quotas
{
    public sealed class AiQuotaService
    {
        private const int MaxSessionFiles = 120;
        private const int TailBytes = 768 * 1024;
        private const string AgyGoogleClientIdEnv = "AGY_GOOGLE_CLIENT_ID";
        private const string AgyGoogleClientSecretEnv = "AGY_GOOGLE_CLIENT_SECRET";
        private const string AgyUserAgent = "antigravity/1.20.5 windows/amd64 google-api-nodejs-client/10.3.0";
        private const string AgyOAuthScope = "openid email https://www.googleapis.com/auth/cloud-platform";
        private const string PhoneMonitorQuotaRootName = "PhoneMonitor";
        private const string PhoneMonitorQuotaFolderName = "quotas";
        private static readonly byte[] AgyTokenEntropy = Encoding.UTF8.GetBytes("PhoneMonitor.AGY.RefreshToken.v1");
        private static readonly string[] AgyQuotaApiBases =
        {
            "https://daily-cloudcode-pa.googleapis.com",
            "https://cloudcode-pa.googleapis.com"
        };
        private static readonly JsonSerializerOptions CacheJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();
        private static readonly Lazy<AgyGoogleOAuthClientConfig> AgyGoogleOAuthClient =
            new Lazy<AgyGoogleOAuthClientConfig>(LoadAgyGoogleOAuthClientConfig);
        private readonly object agyOAuthLock = new object();
        private readonly Dictionary<string, AgyOAuthSession> agyOAuthSessions = new Dictionary<string, AgyOAuthSession>(StringComparer.Ordinal);

        private static HttpClient CreateSharedHttpClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public Task<QuotaSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return BuildSnapshotAsync(false, cancellationToken);
        }

        public Task<QuotaSnapshot> RefreshSnapshotAsync(CancellationToken cancellationToken)
        {
            return BuildSnapshotAsync(true, cancellationToken);
        }

        public AgyImportResult ImportAgyAccountsFromAntigravity()
        {
            var accountStoreDir = AgyAccountStoreDirectory();
            var before = ReadPhoneMonitorAgyAccounts(accountStoreDir).Count;
            var imported = ImportAgyAccountsFromAntigravity(accountStoreDir);
            var after = ReadPhoneMonitorAgyAccounts(accountStoreDir).Count;

            return new AgyImportResult
            {
                Imported = imported,
                Accounts = after,
                StoreDirectory = accountStoreDir,
                CacheDirectory = AgyQuotaCacheDirectory(),
                Message = after > before
                    ? $"Imported {after - before} AGY account token(s) into PhoneMonitor."
                    : imported > 0
                        ? "AGY account token(s) were refreshed in the PhoneMonitor store."
                        : "No Antigravity account token was available to import."
            };
        }

        public AgyOAuthStartResult StartAgyOAuth(string redirectUri, bool openBrowser)
        {
            if (!TryGetAgyGoogleOAuthClient(out var oauthClient, out var configError))
            {
                return new AgyOAuthStartResult
                {
                    Opened = false,
                    Message = configError
                };
            }

            var state = GenerateOAuthToken(32);
            var verifier = GenerateOAuthToken(64);
            string challenge;
            using (var sha256 = SHA256.Create())
            {
                challenge = Base64UrlEncode(sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            }
            var now = DateTimeOffset.UtcNow;

            lock (agyOAuthLock)
            {
                PruneAgyOAuthSessions(now);
                agyOAuthSessions[state] = new AgyOAuthSession
                {
                    State = state,
                    RedirectUri = redirectUri,
                    CodeVerifier = verifier,
                    ExpiresAt = now.AddMinutes(10)
                };
            }

            var query = BuildQuery(new Dictionary<string, string>
            {
                ["client_id"] = oauthClient.ClientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = AgyOAuthScope,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["state"] = state
            });
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
            var opened = openBrowser && TryOpenBrowser(authUrl);

            return new AgyOAuthStartResult
            {
                State = state,
                AuthUrl = authUrl,
                RedirectUri = redirectUri,
                Opened = opened,
                ExpiresAt = now.AddMinutes(10),
                Message = opened
                    ? "AGY OAuth was opened in the PC browser."
                    : "Open the AGY OAuth URL on this PC to continue."
            };
        }

        public async Task<AgyOAuthCallbackResult> CompleteAgyOAuthAsync(
            string state,
            string code,
            string error,
            string errorDescription,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                if (!string.IsNullOrWhiteSpace(state))
                {
                    lock (agyOAuthLock)
                    {
                        agyOAuthSessions.Remove(state);
                    }
                }

                return AgyOAuthCallbackResult.Fail(errorDescription ?? error);
            }

            if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
            {
                return AgyOAuthCallbackResult.Fail("Google OAuth callback was missing state or code.");
            }

            AgyOAuthSession session;
            lock (agyOAuthLock)
            {
                PruneAgyOAuthSessions(DateTimeOffset.UtcNow);
                if (!agyOAuthSessions.TryGetValue(state, out session))
                {
                    return AgyOAuthCallbackResult.Fail("OAuth session expired. Start AGY sign-in again.");
                }

                agyOAuthSessions.Remove(state);
            }

            try
            {
                var token = await ExchangeAgyAuthorizationCodeAsync(code, session.RedirectUri, session.CodeVerifier, cancellationToken);
                if (string.IsNullOrWhiteSpace(token.RefreshToken))
                {
                    return AgyOAuthCallbackResult.Fail("Google did not return a refresh token. Start sign-in again and approve offline access.");
                }

                var account = new AgyAccountToken
                {
                    AccountId = token.Subject ?? token.Email ?? Guid.NewGuid().ToString("N"),
                    Email = token.Email,
                    RefreshToken = token.RefreshToken
                };
                WriteAgyAccountToken(AgyAccountStoreDirectory(), account);

                return new AgyOAuthCallbackResult
                {
                    Success = true,
                    AccountId = account.AccountId,
                    Email = account.Email,
                    StoreDirectory = AgyAccountStoreDirectory(),
                    Message = string.IsNullOrWhiteSpace(account.Email)
                        ? "AGY sign-in completed."
                        : $"AGY sign-in completed for {account.Email}."
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                return AgyOAuthCallbackResult.Fail(ex.Message);
            }
        }

        public AgyAccountActionResult OpenAgyCli(string accountId, string email, bool openWindow = true)
        {
            var agyExe = AgyExecutablePath();
            if (!File.Exists(agyExe))
            {
                return AgyAccountActionResult.Fail("AGY executable was not found.", agyExe);
            }

            var account = FindPhoneMonitorAgyAccount(accountId, email);
            if (account == null)
            {
                return AgyAccountActionResult.Fail("AGY account was not found in the PhoneMonitor store.", AgyAccountStoreDirectory());
            }

            var launcher = WriteAgyCliLauncher(agyExe, account);
            if (!openWindow)
            {
                return new AgyAccountActionResult
                {
                    Success = true,
                    AccountId = account.AccountId,
                    Email = account.Email,
                    Path = launcher,
                    Message = "AGY CLI launcher is ready."
                };
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"\"{launcher}\"\"",
                    UseShellExecute = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                });

                return new AgyAccountActionResult
                {
                    Success = true,
                    AccountId = account.AccountId,
                    Email = account.Email,
                    Path = launcher,
                    Message = string.IsNullOrWhiteSpace(account.Email)
                        ? "AGY CLI opened with the selected PhoneMonitor account context."
                        : $"AGY CLI opened for {account.Email}."
                };
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                return AgyAccountActionResult.Fail(ex.Message, launcher);
            }
        }

        public AgyAccountActionResult DeleteAgyAccount(string accountId, string email)
        {
            var accountStoreDir = AgyAccountStoreDirectory();
            var quotaCacheDir = AgyQuotaCacheDirectory();
            var deleted = 0;

            foreach (var accountFile in FindMatchingAgyAccountFiles(accountStoreDir, accountId, email))
            {
                TryDeleteFile(accountFile, ref deleted);
            }

            foreach (var cacheFile in FindMatchingAgyCacheFiles(quotaCacheDir, accountId, email))
            {
                TryDeleteFile(cacheFile, ref deleted);
            }

            return new AgyAccountActionResult
            {
                Success = deleted > 0,
                AccountId = accountId,
                Email = email,
                Path = accountStoreDir,
                Deleted = deleted,
                Message = deleted > 0
                    ? $"Deleted {deleted} AGY account/cache file(s)."
                    : "No matching AGY account/cache file was found."
            };
        }

        public AgyAccountActionResult DeleteCodexAccount(string accountId, string email)
        {
            var cacheDirectory = CodexQuotaCacheDirectory();
            var deleted = 0;
            foreach (var cacheFile in FindJsonFiles(cacheDirectory))
            {
                try
                {
                    var status = JsonSerializer.Deserialize<AiQuotaStatus>(File.ReadAllText(cacheFile), CacheJsonOptions);
                    if (status != null && AccountMatches(status.AccountId, status.AccountEmail, accountId, email))
                    {
                        TryDeleteFile(cacheFile, ref deleted);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return new AgyAccountActionResult
            {
                Success = deleted > 0,
                AccountId = accountId,
                Email = email,
                Path = cacheDirectory,
                Deleted = deleted,
                Message = deleted > 0
                    ? $"Deleted {deleted} Codex profile cache file(s)."
                    : "No matching Codex profile cache file was found."
            };
        }

        private async Task<QuotaSnapshot> BuildSnapshotAsync(bool forceAgyRefresh, CancellationToken cancellationToken)
        {
            var snapshot = new QuotaSnapshot
            {
                Providers = new List<AiQuotaStatus>()
            };
            snapshot.Providers.AddRange(ReadCodexQuotas());
            // Claude Code: detection-only was a dead end (no ingest path). Do not surface until real quota exists.
            snapshot.Providers.AddRange(await ReadAgyQuotasAsync(forceAgyRefresh, cancellationToken));

            return snapshot;
        }

        private IEnumerable<AiQuotaStatus> ReadCodexQuotas()
        {
            var codexHomes = ResolveCodexHomes().ToList();
            var cacheDirectory = CodexQuotaCacheDirectory();
            var identities = codexHomes
                .SelectMany(ReadCodexAuthIdentities)
                .GroupBy(identity => FirstNonEmpty(identity.AccountId, identity.Email, identity.Source), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(identity => string.Equals(Path.GetFileName(identity.Source), "auth.json", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(identity => File.GetLastWriteTimeUtc(identity.Source))
                    .First())
                .ToList();
            var activeIdentity = identities
                .FirstOrDefault(identity => string.Equals(Path.GetFileName(identity.Source), "auth.json", StringComparison.OrdinalIgnoreCase))
                ?? identities.FirstOrDefault()
                ?? new CodexAccountIdentity
                {
                    AccountId = "local",
                    Source = codexHomes.FirstOrDefault() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
                };

            var activeHome = Directory.Exists(activeIdentity.Source)
                ? activeIdentity.Source
                : Path.GetDirectoryName(activeIdentity.Source);
            var activeQuota = !string.IsNullOrWhiteSpace(activeHome)
                ? ReadCodexQuota(activeHome)
                : Unavailable("codex", "Codex", "Codex session directory was not found.", activeHome);
            if (!IsUsableQuota(activeQuota))
            {
                activeQuota = codexHomes
                    .Select(ReadCodexQuota)
                    .Where(IsUsableQuota)
                    .OrderByDescending(status => status.ObservedAt ?? DateTimeOffset.MinValue)
                    .FirstOrDefault()
                    ?? activeQuota;
            }
            if (IsUsableQuota(activeQuota) && IsCodexQuotaCompatibleWithIdentity(activeQuota, activeIdentity))
            {
                ApplyCodexIdentity(activeQuota, activeIdentity);
                WriteCodexQuotaCache(cacheDirectory, activeQuota);
            }

            var statuses = ReadCodexQuotaCache(cacheDirectory).ToList();
            if (IsUsableQuota(activeQuota) &&
                !string.Equals(activeQuota.AccountId, "local", StringComparison.OrdinalIgnoreCase) &&
                !statuses.Any(status => SameCodexAccount(status, activeQuota)))
            {
                statuses.Add(activeQuota);
            }

            foreach (var identity in identities)
            {
                if (!statuses.Any(status => SameCodexAccount(status, identity)))
                {
                    statuses.Add(BuildCodexPlaceholder(identity));
                }
            }

            foreach (var status in statuses)
            {
                status.IsActive = SameCodexAccount(status, activeIdentity);
            }

            if (statuses.Any())
            {
                return statuses
                    .OrderBy(status => status.State == "ok" ? 0 : 1)
                    .ThenByDescending(status => status.ObservedAt ?? DateTimeOffset.MinValue)
                    .ThenBy(status => status.AccountEmail ?? status.AccountId ?? status.Id)
                    .ToList();
            }

            return new[] { activeQuota };
        }

        private static IReadOnlyList<string> ResolveCodexHomes()
        {
            var homes = new List<string>();

            void AddHome(string home)
            {
                if (string.IsNullOrWhiteSpace(home))
                {
                    return;
                }

                var fullPath = Path.GetFullPath(home);
                if (Directory.Exists(fullPath) && !homes.Any(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    homes.Add(fullPath);
                }
            }

            AddHome(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));

            try
            {
                var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
                var usersRoot = string.IsNullOrWhiteSpace(systemRoot)
                    ? null
                    : Path.Combine(systemRoot, "Users");
                if (Directory.Exists(usersRoot))
                {
                    foreach (var profile in Directory.EnumerateDirectories(usersRoot))
                    {
                        AddHome(Path.Combine(profile, ".codex"));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
            }

            return homes;
        }

        private AiQuotaStatus ReadCodexQuota(string codexHome)
        {
            var sessionsRoot = Path.Combine(codexHome, "sessions");

            if (!Directory.Exists(sessionsRoot))
            {
                return Unavailable("codex", "Codex", "Codex session directory was not found.", sessionsRoot);
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.LastWriteTimeUtc)
                    .Take(MaxSessionFiles))
                {
                    var status = TryReadCodexQuotaFromFile(file.FullName);
                    if (status != null)
                    {
                        return status;
                    }
                }

                return Unavailable("codex", "Codex", "No recent Codex rate_limits event was found.", sessionsRoot);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                return Unavailable("codex", "Codex", ex.Message, sessionsRoot);
            }
        }

        private AiQuotaStatus TryReadCodexQuotaFromFile(string path)
        {
            var lines = ReadTailLines(path);
            for (var index = lines.Count - 1; index >= 0; index--)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"rate_limits\""))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!TryGetProperty(root, "payload", out var payload) ||
                    !TryGetProperty(payload, "rate_limits", out var limits) ||
                    limits.ValueKind == JsonValueKind.Null ||
                    limits.ValueKind == JsonValueKind.Undefined)
                {
                    continue;
                }

                var observedAt = TryGetDateTimeOffset(root, "timestamp");
                return new AiQuotaStatus
                {
                    Id = "codex",
                    Label = "Codex",
                    Family = "codex",
                    AccountId = "local",
                    State = "ok",
                    Source = path,
                    Detail = TryGetString(limits, "limit_name") ?? TryGetString(limits, "limit_id") ?? "Latest Codex rate limit event.",
                    ObservedAt = observedAt,
                    AccountTier = TryGetString(limits, "plan_type"),
                    Primary = ReadWindow(limits, "primary", "5h"),
                    Secondary = ReadWindow(limits, "secondary", "Weekly")
                };
            }

            return null;
        }

        private static bool IsUsableQuota(AiQuotaStatus status)
        {
            return status != null &&
                string.Equals(status.State, "ok", StringComparison.OrdinalIgnoreCase) &&
                (status.Primary != null || status.Secondary != null);
        }

        private static void ApplyCodexIdentity(AiQuotaStatus status, CodexAccountIdentity identity)
        {
            if (status == null || identity == null)
            {
                return;
            }

            var accountId = FirstNonEmpty(identity.AccountId, status.AccountId, identity.Email, "local");
            status.Id = BuildCodexStatusId(accountId);
            status.AccountId = accountId;
            status.AccountEmail = identity.Email;
            status.AccountTier = FirstNonEmpty(identity.Tier, status.AccountTier);
            status.Family = "codex";
            status.Label = "Codex";
        }

        private static AiQuotaStatus BuildCodexPlaceholder(CodexAccountIdentity identity)
        {
            var accountId = FirstNonEmpty(identity.AccountId, identity.Email, "local");
            return new AiQuotaStatus
            {
                Id = BuildCodexStatusId(accountId),
                Label = "Codex",
                Family = "codex",
                AccountId = accountId,
                AccountEmail = identity.Email,
                AccountTier = identity.Tier,
                State = "source-needed",
                Source = identity.Source,
                Detail = "Codex account was seen, but PhoneMonitor has not recorded a quota snapshot for it yet."
            };
        }

        private static bool IsCodexQuotaCompatibleWithIdentity(AiQuotaStatus status, CodexAccountIdentity identity)
        {
            if (status?.ObservedAt == null ||
                identity == null ||
                string.IsNullOrWhiteSpace(identity.Source) ||
                !File.Exists(identity.Source))
            {
                return true;
            }

            var authWriteTime = new DateTimeOffset(File.GetLastWriteTimeUtc(identity.Source), TimeSpan.Zero);
            return status.ObservedAt.Value >= authWriteTime.AddMinutes(-2);
        }

        private static IEnumerable<AiQuotaStatus> ReadCodexQuotaCache(string cacheDirectory)
        {
            if (!Directory.Exists(cacheDirectory))
            {
                return Enumerable.Empty<AiQuotaStatus>();
            }

            var statuses = new List<AiQuotaStatus>();
            foreach (var cacheFile in FindJsonFiles(cacheDirectory))
            {
                try
                {
                    var status = JsonSerializer.Deserialize<AiQuotaStatus>(File.ReadAllText(cacheFile), CacheJsonOptions);
                    if (status == null)
                    {
                        continue;
                    }

                    status.Id = string.IsNullOrWhiteSpace(status.Id)
                        ? BuildCodexStatusId(status.AccountId ?? status.AccountEmail ?? Path.GetFileNameWithoutExtension(cacheFile))
                        : status.Id;
                    status.Label = string.IsNullOrWhiteSpace(status.Label) ? "Codex" : status.Label;
                    status.Family = "codex";
                    status.Source = string.IsNullOrWhiteSpace(status.Source) ? cacheFile : status.Source;
                    statuses.Add(status);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return statuses
                .GroupBy(status => status.AccountId ?? status.AccountEmail ?? status.Id ?? Guid.NewGuid().ToString("N"), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(status => status.ObservedAt ?? DateTimeOffset.MinValue)
                    .First())
                .ToList();
        }

        private static void WriteCodexQuotaCache(string cacheDirectory, AiQuotaStatus status)
        {
            if (!IsUsableQuota(status))
            {
                return;
            }

            Directory.CreateDirectory(cacheDirectory);
            var accountKey = FirstNonEmpty(status.AccountId, status.AccountEmail, status.Id, "local");
            var path = Path.Combine(cacheDirectory, $"{SafeFileName(accountKey)}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(status, CacheJsonOptions));
        }

        private static IReadOnlyList<CodexAccountIdentity> ReadCodexAuthIdentities(string codexHome)
        {
            if (!Directory.Exists(codexHome))
            {
                return Array.Empty<CodexAccountIdentity>();
            }

            var identities = new List<CodexAccountIdentity>();
            // Backups are retained for recovery, but they are not live Codex
            // profiles and must not appear as extra quota accounts.
            foreach (var authFile in Directory.EnumerateFiles(codexHome, "auth*.json", SearchOption.TopDirectoryOnly)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var identity = TryReadCodexIdentityFromAuthFile(authFile);
                if (identity != null)
                {
                    identities.Add(identity);
                }
            }

            return identities
                .GroupBy(identity => FirstNonEmpty(identity.AccountId, identity.Email, Path.GetFileNameWithoutExtension(identity.Source)), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(identity => string.Equals(Path.GetFileName(identity.Source), "auth.json", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(identity => File.GetLastWriteTimeUtc(identity.Source))
                    .First())
                .ToList();
        }

        private static CodexAccountIdentity TryReadCodexIdentityFromAuthFile(string authFile)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(authFile));
                var root = doc.RootElement;
                if (!TryGetProperty(root, "tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var identity = new CodexAccountIdentity
                {
                    AccountId = TryGetString(tokens, "account_id"),
                    Source = authFile
                };

                MergeCodexIdentity(identity, ReadCodexIdentityFromJwt(TryGetString(tokens, "id_token")));
                MergeCodexIdentity(identity, ReadCodexIdentityFromJwt(TryGetString(tokens, "access_token")));

                if (string.IsNullOrWhiteSpace(identity.AccountId) && string.IsNullOrWhiteSpace(identity.Email))
                {
                    return null;
                }

                return identity;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is FormatException || ex is ArgumentException)
            {
                return null;
            }
        }

        private static CodexAccountIdentity ReadCodexIdentityFromJwt(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new CodexAccountIdentity();
            }

            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                return new CodexAccountIdentity();
            }

            try
            {
                var payload = Encoding.UTF8.GetString(DecodeBase64Url(parts[1]));
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var identity = new CodexAccountIdentity
                {
                    AccountId = TryGetString(root, "sub"),
                    Email = TryGetString(root, "email")
                };

                if (TryGetProperty(root, "https://api.openai.com/auth", out var auth) && auth.ValueKind == JsonValueKind.Object)
                {
                    identity.AccountId = FirstNonEmpty(
                        TryGetString(auth, "chatgpt_account_id"),
                        TryGetString(auth, "account_id"),
                        TryGetString(auth, "chatgpt_user_id"),
                        TryGetString(auth, "user_id"),
                        identity.AccountId);
                    identity.Tier = TryGetString(auth, "chatgpt_plan_type");
                }

                if (TryGetProperty(root, "https://api.openai.com/profile", out var profile) && profile.ValueKind == JsonValueKind.Object)
                {
                    identity.Email = FirstNonEmpty(TryGetString(profile, "email"), identity.Email);
                }

                return identity;
            }
            catch (Exception ex) when (ex is FormatException || ex is JsonException || ex is ArgumentException)
            {
                return new CodexAccountIdentity();
            }
        }

        private static void MergeCodexIdentity(CodexAccountIdentity target, CodexAccountIdentity source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.AccountId = FirstNonEmpty(target.AccountId, source.AccountId);
            target.Email = FirstNonEmpty(target.Email, source.Email);
            target.Tier = FirstNonEmpty(target.Tier, source.Tier);
        }

        private static bool SameCodexAccount(AiQuotaStatus status, AiQuotaStatus other)
        {
            return status != null && other != null &&
                string.Equals(
                    FirstNonEmpty(status.AccountId, status.AccountEmail, status.Id),
                    FirstNonEmpty(other.AccountId, other.AccountEmail, other.Id),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameCodexAccount(AiQuotaStatus status, CodexAccountIdentity identity)
        {
            return status != null && identity != null &&
                string.Equals(
                    FirstNonEmpty(status.AccountId, status.AccountEmail, status.Id),
                    FirstNonEmpty(identity.AccountId, identity.Email),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCodexStatusId(string accountId)
        {
            return string.IsNullOrWhiteSpace(accountId) || string.Equals(accountId, "local", StringComparison.OrdinalIgnoreCase)
                ? "codex"
                : $"codex-{SafeFileName(accountId)}";
        }

        private static List<string> ReadTailLines(string path)
        {
            var file = new FileInfo(path);
            var start = Math.Max(0, file.Length - TailBytes);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private async Task<IEnumerable<AiQuotaStatus>> ReadAgyQuotasAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            var agyExe = AgyExecutablePath();
            var accountStoreDir = AgyAccountStoreDirectory();
            var quotaCacheDir = AgyQuotaCacheDirectory();

            if (!File.Exists(agyExe))
            {
                return new[] { Unavailable("agy", "AGY", "AGY executable was not found.", agyExe) };
            }

            var accounts = ReadPhoneMonitorAgyAccounts(accountStoreDir).ToList();
            if (!accounts.Any())
            {
                ImportAgyAccountsFromAntigravity(accountStoreDir);
                accounts = ReadPhoneMonitorAgyAccounts(accountStoreDir).ToList();
            }

            if (!accounts.Any())
            {
                return new[]
                {
                    new AiQuotaStatus
                    {
                        Id = "agy",
                        Label = "AGY",
                        Family = "agy",
                        State = "source-needed",
                        Source = accountStoreDir,
                        Detail = "AGY is installed, but PhoneMonitor has no AGY account token yet. Import once from Antigravity or complete PhoneMonitor OAuth."
                    }
                };
            }

            var cacheStatuses = ReadAgyQuotasFromAuthorizedCache(quotaCacheDir, accounts).ToList();
            if (!forceRefresh && cacheStatuses.Any() && IsAgyCacheFresh(quotaCacheDir))
            {
                return cacheStatuses;
            }

            var refreshedStatuses = await RefreshAgyQuotasAsync(accounts, quotaCacheDir, cancellationToken);
            if (refreshedStatuses.Any())
            {
                return MergeAgyStatuses(cacheStatuses, refreshedStatuses);
            }

            if (cacheStatuses.Any())
            {
                return cacheStatuses;
            }

            return accounts.Select(account => new AiQuotaStatus
            {
                Id = string.IsNullOrWhiteSpace(account.AccountId) ? "agy" : $"agy-{account.AccountId}",
                Label = "AGY",
                Family = "agy",
                AccountId = account.AccountId,
                AccountEmail = account.Email,
                AccountTier = account.Tier,
                State = "source-needed",
                Source = account.Source,
                Detail = "PhoneMonitor has an AGY token, but no quota cache could be refreshed."
            }).ToList();
        }

        private IEnumerable<AiQuotaStatus> ReadAgyQuotasFromAuthorizedCache(string cacheDirectory, IReadOnlyList<AgyAccountToken> accounts)
        {
            if (!Directory.Exists(cacheDirectory))
            {
                return Enumerable.Empty<AiQuotaStatus>();
            }

            var metadataByEmail = accounts
                .Where(meta => !string.IsNullOrWhiteSpace(meta.Email))
                .GroupBy(meta => meta.Email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var metadataByAccountId = accounts
                .Where(meta => !string.IsNullOrWhiteSpace(meta.AccountId))
                .GroupBy(meta => meta.AccountId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var statuses = new List<AiQuotaStatus>();

            foreach (var cacheFile in FindJsonFiles(cacheDirectory))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(cacheFile));
                    var root = doc.RootElement;
                    var email = TryGetString(root, "email");
                    if (!string.IsNullOrWhiteSpace(email) && !seenEmails.Add(email))
                    {
                        continue;
                    }

                    if (!TryGetProperty(root, "payload", out var payload) ||
                        !TryGetProperty(payload, "quota_summary", out var summary) ||
                        !TryGetProperty(summary, "groups", out var groups) ||
                        groups.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var buckets = new Dictionary<string, QuotaBucketInfo>(StringComparer.OrdinalIgnoreCase);
                    foreach (var group in groups.EnumerateArray())
                    {
                        if (!TryGetProperty(group, "buckets", out var bucketArray) || bucketArray.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var bucket in bucketArray.EnumerateArray())
                        {
                            var bucketId = TryGetString(bucket, "bucketId");
                            if (string.IsNullOrWhiteSpace(bucketId) || buckets.ContainsKey(bucketId))
                            {
                                continue;
                            }

                            buckets[bucketId] = ReadAgyCacheBucket(bucket);
                        }
                    }

                    metadataByEmail.TryGetValue(email ?? string.Empty, out var metadata);
                    if (metadata == null)
                    {
                        metadataByAccountId.TryGetValue(TryGetString(root, "accountId") ?? string.Empty, out metadata);
                    }

                    var accountId = metadata?.AccountId ?? TryGetString(root, "accountId") ?? email ?? Path.GetFileNameWithoutExtension(cacheFile);
                    var tier = metadata?.Tier;
                    var observedAt = TryGetUnixTimeMilliseconds(root, "updatedAt") ??
                        new DateTimeOffset(File.GetLastWriteTimeUtc(cacheFile), TimeSpan.Zero);
                    var detail = string.Join(" · ", new[] { email, tier }.Where(value => !string.IsNullOrWhiteSpace(value)));

                    statuses.Add(BuildAgyStatusFromBucketInfo("agy-claude", "AGY Claude", accountId, email, tier, cacheFile, detail, observedAt, buckets, "3p-5h", "3p-weekly"));
                    statuses.Add(BuildAgyStatusFromBucketInfo("agy-gemini", "AGY Gemini", accountId, email, tier, cacheFile, detail, observedAt, buckets, "gemini-5h", "gemini-weekly"));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return statuses;
        }

        private static string PhoneMonitorQuotaRoot()
        {
            return AppPaths.EnsureDirectory(AppPaths.QuotasDirectory);
        }

        private static string AgyExecutablePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "agy",
                "bin",
                "agy.exe");
        }

        private static string AgyAccountStoreDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "agy", "accounts");
        }

        private static string AgyQuotaCacheDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "agy", "cache");
        }

        private static string CodexQuotaCacheDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "codex", "accounts");
        }

        private static string AgyLauncherDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "agy", "launch");
        }

        private static AgyAccountToken FindPhoneMonitorAgyAccount(string accountId, string email)
        {
            return ReadPhoneMonitorAgyAccounts(AgyAccountStoreDirectory())
                .FirstOrDefault(account => AccountMatches(account.AccountId, account.Email, accountId, email));
        }

        private static IReadOnlyList<AgyAccountToken> ReadPhoneMonitorAgyAccounts(string accountStoreDir)
        {
            if (!Directory.Exists(accountStoreDir))
            {
                return Array.Empty<AgyAccountToken>();
            }

            var accounts = new List<AgyAccountToken>();
            foreach (var accountFile in FindJsonFiles(accountStoreDir))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(accountFile));
                    var root = doc.RootElement;
                    var refreshToken = ReadProtectedSecret(root, "refresh_token_protected") ??
                        TryGetString(root, "refresh_token");
                    if (string.IsNullOrWhiteSpace(refreshToken))
                    {
                        continue;
                    }

                    accounts.Add(new AgyAccountToken
                    {
                        AccountId = TryGetString(root, "account_id") ?? Path.GetFileNameWithoutExtension(accountFile),
                        Email = TryGetString(root, "email"),
                        Tier = TryGetString(root, "tier"),
                        RefreshToken = refreshToken,
                        Source = accountFile
                    });
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return accounts
                .GroupBy(account => account.AccountId ?? account.Email ?? Guid.NewGuid().ToString("N"), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<string> FindMatchingAgyAccountFiles(string accountStoreDir, string accountId, string email)
        {
            var files = new List<string>();
            foreach (var accountFile in FindJsonFiles(accountStoreDir))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(accountFile));
                    if (AccountMatches(TryGetString(doc.RootElement, "account_id"), TryGetString(doc.RootElement, "email"), accountId, email))
                    {
                        files.Add(accountFile);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return files;
        }

        private static IEnumerable<string> FindMatchingAgyCacheFiles(string cacheDirectory, string accountId, string email)
        {
            var files = new List<string>();
            foreach (var cacheFile in FindJsonFiles(cacheDirectory))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(cacheFile));
                    if (AccountMatches(TryGetString(doc.RootElement, "accountId"), TryGetString(doc.RootElement, "email"), accountId, email))
                    {
                        files.Add(cacheFile);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return files;
        }

        private static bool AccountMatches(string storedAccountId, string storedEmail, string requestedAccountId, string requestedEmail)
        {
            return (!string.IsNullOrWhiteSpace(requestedAccountId) &&
                    string.Equals(storedAccountId, requestedAccountId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(requestedEmail) &&
                    string.Equals(storedEmail, requestedEmail, StringComparison.OrdinalIgnoreCase));
        }

        private static void TryDeleteFile(string path, ref int deleted)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        private static int ImportAgyAccountsFromAntigravity(string accountStoreDir)
        {
            var cockpitAccountsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".antigravity_cockpit",
                "accounts");
            var imported = 0;

            foreach (var accountFile in FindJsonFiles(cockpitAccountsDir))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(accountFile));
                    var root = doc.RootElement;
                    if (!TryGetProperty(root, "token", out var token))
                    {
                        continue;
                    }

                    var refreshToken = TryGetString(token, "refresh_token");
                    if (string.IsNullOrWhiteSpace(refreshToken))
                    {
                        continue;
                    }

                    var accountId = TryGetString(root, "id") ?? Path.GetFileNameWithoutExtension(accountFile);
                    var email = TryGetString(root, "email") ?? TryGetString(token, "email");
                    var tier = TryGetProperty(root, "quota", out var quota)
                        ? TryGetString(quota, "subscription_tier")
                        : null;

                    WriteAgyAccountToken(accountStoreDir, new AgyAccountToken
                    {
                        AccountId = accountId,
                        Email = email,
                        Tier = tier,
                        RefreshToken = refreshToken
                    });
                    imported++;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return imported;
        }

        private static void WriteAgyAccountToken(string accountStoreDir, AgyAccountToken account)
        {
            Directory.CreateDirectory(accountStoreDir);
            var accountId = !string.IsNullOrWhiteSpace(account.AccountId) ? account.AccountId : Guid.NewGuid().ToString("N");
            var path = ResolveAgyAccountTokenFile(accountStoreDir, accountId, account.Email);
            var envelope = new Dictionary<string, object>
            {
                ["provider"] = "agy",
                ["account_id"] = accountId,
                ["email"] = account.Email,
                ["tier"] = account.Tier,
                ["refresh_token_protected"] = ProtectSecret(account.RefreshToken),
                ["protection"] = "windows-dpapi-current-user",
                ["imported_at"] = DateTimeOffset.UtcNow,
                ["source"] = "phone-monitor"
            };
            File.WriteAllText(path, JsonSerializer.Serialize(envelope, CacheJsonOptions), Encoding.UTF8);
        }

        private static string ResolveAgyAccountTokenFile(string accountStoreDir, string accountId, string email)
        {
            foreach (var accountFile in FindJsonFiles(accountStoreDir))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(accountFile));
                    var root = doc.RootElement;
                    var existingAccountId = TryGetString(root, "account_id");
                    var existingEmail = TryGetString(root, "email");

                    if (!string.IsNullOrWhiteSpace(accountId) &&
                        string.Equals(existingAccountId, accountId, StringComparison.OrdinalIgnoreCase))
                    {
                        return accountFile;
                    }

                    if (!string.IsNullOrWhiteSpace(email) &&
                        string.Equals(existingEmail, email, StringComparison.OrdinalIgnoreCase))
                    {
                        return accountFile;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            return Path.Combine(accountStoreDir, $"{SafeFileName(accountId)}.json");
        }

        private static string WriteAgyCliLauncher(string agyExe, AgyAccountToken account)
        {
            var launchDir = AgyLauncherDirectory();
            Directory.CreateDirectory(launchDir);
            var accountId = string.IsNullOrWhiteSpace(account.AccountId) ? "agy" : account.AccountId;
            var launcher = Path.Combine(launchDir, $"open-{SafeFileName(accountId)}.cmd");
            var email = account.Email ?? string.Empty;
            var lines = new[]
            {
                "@echo off",
                $"title AGY - {EscapeBatchValue(email.Length > 0 ? email : accountId)}",
                $"set \"PHONEMONITOR_AGY_ACCOUNT_ID={EscapeBatchValue(accountId)}\"",
                $"set \"PHONEMONITOR_AGY_ACCOUNT_EMAIL={EscapeBatchValue(email)}\"",
                $"set \"AGY_ACCOUNT_ID={EscapeBatchValue(accountId)}\"",
                $"set \"AGY_ACCOUNT_EMAIL={EscapeBatchValue(email)}\"",
                "cd /d \"%USERPROFILE%\"",
                $"echo PhoneMonitor selected AGY account: {EscapeBatchValue(email.Length > 0 ? email : accountId)}",
                "echo If AGY prompts for sign-in, use the account above.",
                $"\"{agyExe}\""
            };
            File.WriteAllLines(launcher, lines, Encoding.ASCII);
            return launcher;
        }

        private static string ProtectSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(bytes, AgyTokenEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string ReadProtectedSecret(JsonElement root, string propertyName)
        {
            var protectedValue = TryGetString(root, propertyName);
            if (string.IsNullOrWhiteSpace(protectedValue))
            {
                return null;
            }

            try
            {
                var protectedBytes = Convert.FromBase64String(protectedValue);
                var bytes = ProtectedData.Unprotect(protectedBytes, AgyTokenEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                return null;
            }
        }

        private async Task<List<AiQuotaStatus>> RefreshAgyQuotasAsync(
            IReadOnlyList<AgyAccountToken> accounts,
            string quotaCacheDir,
            CancellationToken cancellationToken)
        {
            var refreshedStatuses = new List<AiQuotaStatus>();
            if (accounts == null || !accounts.Any())
            {
                return refreshedStatuses;
            }

            Directory.CreateDirectory(quotaCacheDir);

            foreach (var account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (string.IsNullOrWhiteSpace(account.RefreshToken))
                    {
                        continue;
                    }

                    var accessToken = await RefreshAgyAccessTokenAsync(account.RefreshToken, cancellationToken);
                    await WarmAgyLoadCodeAssistAsync(accessToken, cancellationToken);

                    var quotaSummary = await RetrieveAgyQuotaSummaryAsync(accessToken, cancellationToken);
                    var observedAt = DateTimeOffset.UtcNow;
                    var cacheFile = ResolveAgyCacheFile(quotaCacheDir, account.Email, account.AccountId);
                    WriteAgyQuotaCache(cacheFile, account.Email, account.AccountId, observedAt, quotaSummary);

                    var buckets = ReadAgyCacheBuckets(quotaSummary);
                    var detail = string.Join(" · ", new[] { account.Email, account.Tier }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    refreshedStatuses.Add(BuildAgyStatusFromBucketInfo("agy-claude", "AGY Claude", account.AccountId, account.Email, account.Tier, cacheFile, detail, observedAt, buckets, "3p-5h", "3p-weekly"));
                    refreshedStatuses.Add(BuildAgyStatusFromBucketInfo("agy-gemini", "AGY Gemini", account.AccountId, account.Email, account.Tier, cacheFile, detail, observedAt, buckets, "gemini-5h", "gemini-weekly"));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is HttpRequestException || ex is InvalidOperationException)
                {
                }
            }

            return refreshedStatuses;
        }

        private static bool IsAgyCacheFresh(string cacheDirectory)
        {
            if (!Directory.Exists(cacheDirectory))
            {
                return false;
            }

            var newestCacheFile = Directory.EnumerateFiles(cacheDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newestCacheFile == null)
            {
                return false;
            }

            return newestCacheFile.LastWriteTimeUtc >= DateTime.UtcNow.AddMinutes(-15);
        }

        private static IEnumerable<AiQuotaStatus> MergeAgyStatuses(
            IEnumerable<AiQuotaStatus> cachedStatuses,
            IEnumerable<AiQuotaStatus> refreshedStatuses)
        {
            var merged = new Dictionary<string, AiQuotaStatus>(StringComparer.OrdinalIgnoreCase);
            foreach (var status in cachedStatuses.Where(item => item != null))
            {
                merged[status.Id ?? Guid.NewGuid().ToString("N")] = status;
            }

            foreach (var status in refreshedStatuses.Where(item => item != null))
            {
                merged[status.Id ?? Guid.NewGuid().ToString("N")] = status;
            }

            return merged.Values
                .OrderBy(item => item.AccountEmail ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Label ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyDictionary<string, QuotaBucketInfo> ReadAgyCacheBuckets(JsonElement quotaSummary)
        {
            var buckets = new Dictionary<string, QuotaBucketInfo>(StringComparer.OrdinalIgnoreCase);
            if (!TryGetProperty(quotaSummary, "groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
            {
                return buckets;
            }

            foreach (var group in groups.EnumerateArray())
            {
                if (!TryGetProperty(group, "buckets", out var bucketArray) || bucketArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var bucket in bucketArray.EnumerateArray())
                {
                    var bucketId = TryGetString(bucket, "bucketId");
                    if (string.IsNullOrWhiteSpace(bucketId) || buckets.ContainsKey(bucketId))
                    {
                        continue;
                    }

                    buckets[bucketId] = ReadAgyCacheBucket(bucket);
                }
            }

            return buckets;
        }

        private static string ResolveAgyCacheFile(string cacheDirectory, string email, string accountId)
        {
            foreach (var cacheFile in FindJsonFiles(cacheDirectory))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(cacheFile));
                    var cachedEmail = TryGetString(doc.RootElement, "email");
                    var cachedAccountId = TryGetString(doc.RootElement, "accountId");
                    if (!string.IsNullOrWhiteSpace(email) &&
                        string.Equals(email, cachedEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        return cacheFile;
                    }

                    if (!string.IsNullOrWhiteSpace(accountId) &&
                        string.Equals(accountId, cachedAccountId, StringComparison.OrdinalIgnoreCase))
                    {
                        return cacheFile;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                }
            }

            var fileName = !string.IsNullOrWhiteSpace(accountId) ? accountId : Guid.NewGuid().ToString("N");
            return Path.Combine(cacheDirectory, $"{SafeFileName(fileName)}.json");
        }

        private static void WriteAgyQuotaCache(string cacheFile, string email, string accountId, DateTimeOffset observedAt, JsonElement quotaSummary)
        {
            var envelope = new Dictionary<string, object>
            {
                ["email"] = email,
                ["accountId"] = accountId,
                ["updatedAt"] = observedAt.ToUnixTimeMilliseconds(),
                ["source"] = "phone-monitor-api",
                ["payload"] = new Dictionary<string, object>
                {
                    ["quota_summary"] = quotaSummary
                }
            };

            File.WriteAllText(cacheFile, JsonSerializer.Serialize(envelope, CacheJsonOptions), Encoding.UTF8);
        }

        private static async Task<AgyOAuthTokenResult> ExchangeAgyAuthorizationCodeAsync(
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            var oauthClient = RequireAgyGoogleOAuthClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = oauthClient.ClientId,
                ["client_secret"] = oauthClient.ClientSecret,
                ["code"] = code,
                ["code_verifier"] = codeVerifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            });
            using var response = await SharedHttpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Google OAuth token exchange failed with {(int)response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var idToken = TryGetString(root, "id_token");
            var identity = ReadGoogleIdentityFromIdToken(idToken);

            return new AgyOAuthTokenResult
            {
                AccessToken = TryGetString(root, "access_token"),
                RefreshToken = TryGetString(root, "refresh_token"),
                IdToken = idToken,
                Email = identity.Email,
                Subject = identity.Subject
            };
        }

        private static async Task<string> RefreshAgyAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            var oauthClient = RequireAgyGoogleOAuthClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = oauthClient.ClientId,
                ["client_secret"] = oauthClient.ClientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });
            using var response = await SharedHttpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var accessToken = TryGetString(doc.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Google OAuth token response did not include access_token.");
            }

            return accessToken;
        }

        private static AgyGoogleOAuthClientConfig RequireAgyGoogleOAuthClient()
        {
            if (!TryGetAgyGoogleOAuthClient(out var oauthClient, out var configError))
            {
                throw new InvalidOperationException(configError);
            }

            return oauthClient;
        }

        private static bool TryGetAgyGoogleOAuthClient(out AgyGoogleOAuthClientConfig oauthClient, out string error)
        {
            oauthClient = AgyGoogleOAuthClient.Value;
            if (oauthClient != null &&
                !string.IsNullOrWhiteSpace(oauthClient.ClientId) &&
                !string.IsNullOrWhiteSpace(oauthClient.ClientSecret))
            {
                error = null;
                return true;
            }

            error =
                "AGY Google OAuth is not configured. Set environment variables " +
                AgyGoogleClientIdEnv + " and " + AgyGoogleClientSecretEnv +
                ", or create %LOCALAPPDATA%\\PhoneMonitor\\secrets\\agy-google-oauth.json " +
                "with clientId and clientSecret.";
            oauthClient = null;
            return false;
        }

        private static AgyGoogleOAuthClientConfig LoadAgyGoogleOAuthClientConfig()
        {
            var clientId = Environment.GetEnvironmentVariable(AgyGoogleClientIdEnv);
            var clientSecret = Environment.GetEnvironmentVariable(AgyGoogleClientSecretEnv);
            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                return new AgyGoogleOAuthClientConfig
                {
                    ClientId = clientId.Trim(),
                    ClientSecret = clientSecret.Trim(),
                    Source = "environment"
                };
            }

            var secretsPath = AgyGoogleOAuthSecretsPath();
            if (!File.Exists(secretsPath))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(secretsPath));
                var root = doc.RootElement;
                clientId = TryGetString(root, "clientId") ?? TryGetString(root, "client_id");
                clientSecret = TryGetString(root, "clientSecret") ?? TryGetString(root, "client_secret");
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    return null;
                }

                return new AgyGoogleOAuthClientConfig
                {
                    ClientId = clientId.Trim(),
                    ClientSecret = clientSecret.Trim(),
                    Source = "local-secrets-file"
                };
            }
            catch
            {
                return null;
            }
        }

        private static string AgyGoogleOAuthSecretsPath()
        {
            return Path.Combine(
                AppPaths.EnsureDirectory(AppPaths.SecretsDirectory),
                "agy-google-oauth.json");
        }

        private static async Task WarmAgyLoadCodeAssistAsync(string accessToken, CancellationToken cancellationToken)
        {
            using var response = await PostAgyApiAsync("v1internal:loadCodeAssist", accessToken, cancellationToken);
        }

        private static async Task<JsonElement> RetrieveAgyQuotaSummaryAsync(string accessToken, CancellationToken cancellationToken)
        {
            using var response = await PostAgyApiAsync("v1internal:retrieveUserQuotaSummary", accessToken, cancellationToken);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        private static async Task<HttpResponseMessage> PostAgyApiAsync(string path, string accessToken, CancellationToken cancellationToken)
        {
            Exception lastError = null;
            foreach (var baseUrl in AgyQuotaApiBases)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{path}")
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Headers.TryAddWithoutValidation("x-goog-api-client", "gl-node/22.21.1");
                    request.Headers.TryAddWithoutValidation("User-Agent", AgyUserAgent);

                    var response = await SharedHttpClient.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }

                    lastError = new HttpRequestException($"AGY quota API returned {(int)response.StatusCode} for {request.RequestUri}.");
                    response.Dispose();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new HttpRequestException($"AGY quota API request failed for {path}.");
        }

        private static AiQuotaStatus BuildAgyStatusFromBucketInfo(
            string id,
            string label,
            string accountId,
            string accountEmail,
            string accountTier,
            string source,
            string detail,
            DateTimeOffset? observedAt,
            IReadOnlyDictionary<string, QuotaBucketInfo> buckets,
            string fiveHourBucket,
            string weeklyBucket)
        {
            buckets.TryGetValue(fiveHourBucket, out var primary);
            buckets.TryGetValue(weeklyBucket, out var secondary);

            return new AiQuotaStatus
            {
                Id = string.IsNullOrWhiteSpace(accountId) ? id : $"{id}-{accountId}",
                Label = label,
                Family = "agy",
                AccountId = accountId,
                AccountEmail = accountEmail,
                AccountTier = accountTier,
                State = primary != null || secondary != null ? "ok" : "source-needed",
                Source = source,
                Detail = string.IsNullOrWhiteSpace(detail) ? "Antigravity desktop quota cache." : detail,
                ObservedAt = observedAt,
                Primary = ReadAgyWindow(primary, "5h", 300),
                Secondary = ReadAgyWindow(secondary, "Weekly", 10080)
            };
        }

        private static QuotaWindow ReadAgyWindow(QuotaBucketInfo bucket, string label, int defaultWindowMinutes)
        {
            if (bucket == null)
            {
                return null;
            }

            return new QuotaWindow
            {
                Label = label,
                RemainingPercent = bucket.RemainingPercent,
                WindowMinutes = bucket.WindowMinutes ?? defaultWindowMinutes,
                ResetsAt = bucket.ResetsAt
            };
        }

        private static QuotaBucketInfo ReadAgyCacheBucket(JsonElement bucket)
        {
            var remainingFraction = TryGetDouble(bucket, "remainingFraction");
            return new QuotaBucketInfo
            {
                RemainingPercent = remainingFraction.HasValue ? remainingFraction.Value * 100d : (double?)null,
                ResetsAt = TryGetDateTimeOffset(bucket, "resetTime"),
                WindowMinutes = ParseWindowMinutes(TryGetString(bucket, "window"))
            };
        }

        private static IEnumerable<string> FindJsonFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName);
        }

        private static string FindExecutable(string fileName)
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in pathValue.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                try
                {
                    var path = Path.Combine(directory.Trim(), fileName);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
        }

        private static AiQuotaStatus Unavailable(string id, string label, string detail, string source)
        {
            return new AiQuotaStatus
            {
                Id = id,
                Label = label,
                Family = id.StartsWith("agy", StringComparison.OrdinalIgnoreCase)
                    ? "agy"
                    : id.StartsWith("claude", StringComparison.OrdinalIgnoreCase)
                        ? "claude-code"
                        : id,
                State = "unavailable",
                Source = source,
                Detail = detail
            };
        }

        private static QuotaWindow ReadWindow(JsonElement limits, string propertyName, string label)
        {
            if (!TryGetProperty(limits, propertyName, out var window) ||
                window.ValueKind == JsonValueKind.Null ||
                window.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            return new QuotaWindow
            {
                Label = label,
                UsedPercent = TryGetDouble(window, "used_percent"),
                WindowMinutes = TryGetInt(window, "window_minutes"),
                ResetsAt = TryGetUnixTime(window, "resets_at")
            };
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static string TryGetString(JsonElement element, string name)
        {
            return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static double? TryGetDouble(JsonElement element, string name)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int? TryGetInt(JsonElement element, string name)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static DateTimeOffset? TryGetUnixTime(JsonElement element, string name)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }

            return null;
        }

        private static DateTimeOffset? TryGetUnixTimeMilliseconds(JsonElement element, string name)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var milliseconds))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            }

            return null;
        }

        private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string name)
        {
            var value = TryGetString(element, name);
            if (value != null && DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int? ParseWindowMinutes(string window)
        {
            return window?.ToLowerInvariant() switch
            {
                "5h" => 300,
                "weekly" => 10080,
                _ => null
            };
        }

        private void PruneAgyOAuthSessions(DateTimeOffset now)
        {
            foreach (var state in agyOAuthSessions
                .Where(pair => pair.Value.ExpiresAt <= now)
                .Select(pair => pair.Key)
                .ToList())
            {
                agyOAuthSessions.Remove(state);
            }
        }

        private static bool TryOpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static string GenerateOAuthToken(int byteCount)
        {
            var bytes = new byte[byteCount];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Base64UrlEncode(bytes);
        }

        private static string BuildQuery(IReadOnlyDictionary<string, string> values)
        {
            return string.Join("&", values
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        private static AgyGoogleIdentity ReadGoogleIdentityFromIdToken(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return new AgyGoogleIdentity();
            }

            var parts = idToken.Split('.');
            if (parts.Length < 2)
            {
                return new AgyGoogleIdentity();
            }

            try
            {
                var payload = Encoding.UTF8.GetString(DecodeBase64Url(parts[1]));
                using var doc = JsonDocument.Parse(payload);
                return new AgyGoogleIdentity
                {
                    Subject = TryGetString(doc.RootElement, "sub"),
                    Email = TryGetString(doc.RootElement, "email")
                };
            }
            catch (Exception ex) when (ex is FormatException || ex is JsonException || ex is ArgumentException)
            {
                return new AgyGoogleIdentity();
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] DecodeBase64Url(string value)
        {
            var padded = value
                .Replace('-', '+')
                .Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return Convert.FromBase64String(padded);
        }

        private static string SafeFileName(string value)
        {
            var raw = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                builder.Append(invalid.Contains(ch) ? '_' : ch);
            }

            return builder.ToString();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string EscapeBatchValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\"", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);
        }

        private sealed class AgyAccountToken
        {
            public string AccountId { get; set; }
            public string Email { get; set; }
            public string Tier { get; set; }
            public string RefreshToken { get; set; }
            public string Source { get; set; }
        }

        private sealed class CodexAccountIdentity
        {
            public string AccountId { get; set; }
            public string Email { get; set; }
            public string Tier { get; set; }
            public string Source { get; set; }
        }

        private sealed class AgyGoogleOAuthClientConfig
        {
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string Source { get; set; }
        }

        private sealed class AgyOAuthSession
        {
            public string State { get; set; }
            public string RedirectUri { get; set; }
            public string CodeVerifier { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }

        private sealed class AgyOAuthTokenResult
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public string IdToken { get; set; }
            public string Email { get; set; }
            public string Subject { get; set; }
        }

        private sealed class AgyGoogleIdentity
        {
            public string Subject { get; set; }
            public string Email { get; set; }
        }

        public sealed class AgyImportResult
        {
            public int Imported { get; set; }
            public int Accounts { get; set; }
            public string StoreDirectory { get; set; }
            public string CacheDirectory { get; set; }
            public string Message { get; set; }
        }

        public sealed class AgyOAuthStartResult
        {
            public string State { get; set; }
            public string AuthUrl { get; set; }
            public string RedirectUri { get; set; }
            public bool Opened { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
            public string Message { get; set; }
        }

        public sealed class AgyOAuthCallbackResult
        {
            public bool Success { get; set; }
            public string AccountId { get; set; }
            public string Email { get; set; }
            public string StoreDirectory { get; set; }
            public string Message { get; set; }

            public static AgyOAuthCallbackResult Fail(string message)
            {
                return new AgyOAuthCallbackResult
                {
                    Success = false,
                    Message = string.IsNullOrWhiteSpace(message) ? "AGY sign-in failed." : message
                };
            }
        }

        public sealed class AgyAccountActionResult
        {
            public bool Success { get; set; }
            public string AccountId { get; set; }
            public string Email { get; set; }
            public string Path { get; set; }
            public int Deleted { get; set; }
            public string Message { get; set; }

            public static AgyAccountActionResult Fail(string message, string path = null)
            {
                return new AgyAccountActionResult
                {
                    Success = false,
                    Path = path,
                    Message = string.IsNullOrWhiteSpace(message) ? "AGY account action failed." : message
                };
            }
        }

        private sealed class QuotaBucketInfo
        {
            public double? RemainingPercent { get; set; }
            public DateTimeOffset? ResetsAt { get; set; }
            public int? WindowMinutes { get; set; }
        }
    }
}
