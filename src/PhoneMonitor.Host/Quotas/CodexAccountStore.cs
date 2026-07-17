using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static PhoneMonitor.Host.Quotas.QuotaJsonHelpers;
using static PhoneMonitor.Host.Quotas.QuotaPaths;

namespace PhoneMonitor.Host.Quotas
{
    /// <summary>
    /// Multi-account support for the Codex CLI, which only ever reads a single
    /// ~/.codex/auth.json. This store "captures" the currently logged-in auth.json
    /// into a named profile library so accounts are not lost when you log into
    /// another, and swaps a chosen profile back into auth.json to switch.
    ///
    /// Switch sequence (order matters): kill running codex/chatgpt FIRST (a live
    /// session's token refresh would clobber the swapped file), capture current,
    /// swap, then relaunch.
    /// </summary>
    internal static class CodexAccountStore
    {
        internal sealed class CodexProfile
        {
            public string AccountId { get; set; }
            public string Email { get; set; }
            public string Tier { get; set; }
            public string Path { get; set; }
            public bool IsActive { get; set; }
        }

        internal sealed class CodexActionResult
        {
            public bool Success { get; set; }
            public string AccountId { get; set; }
            public string Email { get; set; }
            public string Message { get; set; }
            public int Affected { get; set; }

            public static CodexActionResult Fail(string message) =>
                new CodexActionResult { Success = false, Message = message };
        }

        /// <summary>
        /// Snapshots the current ~/.codex/auth.json into the profile library so the
        /// active account survives a future switch. No-op if not logged in.
        /// Returns the captured account id, or null.
        /// </summary>
        internal static string CaptureCurrent()
        {
            var authFile = CodexAuthFile();
            if (!File.Exists(authFile))
            {
                return null;
            }

            var (accountId, email, _) = CodexQuotaReader.ReadAuthFileIdentity(authFile);
            var key = FirstNonEmpty(accountId, email);
            if (string.IsNullOrWhiteSpace(key))
            {
                return null; // not a logged-in account, nothing worth capturing
            }

            var profileDir = CodexProfileDirectory();
            Directory.CreateDirectory(profileDir);
            var target = System.IO.Path.Combine(profileDir, $"{SafeFileName(key)}.json");
            try
            {
                CopyAtomic(authFile, target);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            return key;
        }

        internal static IReadOnlyList<CodexProfile> ListProfiles()
        {
            CaptureCurrent();

            var authFile = CodexAuthFile();
            var (activeId, activeEmail, _) = File.Exists(authFile)
                ? CodexQuotaReader.ReadAuthFileIdentity(authFile)
                : (null, null, null);
            var activeKey = FirstNonEmpty(activeId, activeEmail);

            var profiles = new List<CodexProfile>();
            var dir = CodexProfileDirectory();
            foreach (var file in FindJsonFiles(dir))
            {
                var (accountId, email, tier) = CodexQuotaReader.ReadAuthFileIdentity(file);
                var key = FirstNonEmpty(accountId, email, System.IO.Path.GetFileNameWithoutExtension(file));
                profiles.Add(new CodexProfile
                {
                    AccountId = accountId,
                    Email = email,
                    Tier = tier,
                    Path = file,
                    IsActive = !string.IsNullOrWhiteSpace(activeKey) &&
                        string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase)
                });
            }

            return profiles
                .GroupBy(p => FirstNonEmpty(p.AccountId, p.Email, p.Path), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.Email ?? p.AccountId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Switches the active Codex account: kill running codex/chatgpt, capture the
        /// current account, swap the chosen profile into ~/.codex/auth.json, relaunch.
        /// </summary>
        internal static CodexActionResult SwitchTo(string accountId, string email, bool relaunch = true)
        {
            var profile = FindProfile(accountId, email);
            if (profile == null)
            {
                return CodexActionResult.Fail("找不到對應的 Codex 帳號 profile。");
            }

            // Kill first so no live session refreshes/clobbers auth.json during the swap.
            CliProcessManager.KillByNames(CliProcessManager.CodexProcessNames);

            // Preserve whatever is currently active before we overwrite it.
            CaptureCurrent();

            var authFile = CodexAuthFile();
            try
            {
                Directory.CreateDirectory(CodexHome());
                if (File.Exists(authFile))
                {
                    File.Copy(authFile, authFile + ".bak", overwrite: true);
                }

                CopyAtomic(profile.Path, authFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return CodexActionResult.Fail($"寫入 auth.json 失敗:{ex.Message}");
            }

            if (relaunch)
            {
                LaunchCodex("codex");
            }

            return new CodexActionResult
            {
                Success = true,
                AccountId = profile.AccountId,
                Email = profile.Email,
                Message = string.IsNullOrWhiteSpace(profile.Email)
                    ? "已切換 Codex 帳號並重新開啟。"
                    : $"已切換至 {profile.Email} 並重新開啟 Codex。"
            };
        }

        /// <summary>Launches `codex login` in a new terminal for re-authentication.</summary>
        internal static CodexActionResult ReAuth()
        {
            // The running session must not race the login writing a fresh auth.json.
            CliProcessManager.KillByNames(CliProcessManager.CodexProcessNames);
            if (!LaunchCodex("codex login"))
            {
                return CodexActionResult.Fail("無法開啟 codex login。");
            }

            return new CodexActionResult
            {
                Success = true,
                Message = "已開啟 codex login,請在該視窗完成登入。"
            };
        }

        internal static CodexActionResult DeleteProfile(string accountId, string email)
        {
            var deleted = 0;
            foreach (var file in FindMatchingProfiles(accountId, email))
            {
                TryDeleteFile(file, ref deleted);
            }

            return new CodexActionResult
            {
                Success = deleted > 0,
                AccountId = accountId,
                Email = email,
                Affected = deleted,
                Message = deleted > 0
                    ? $"已刪除 {deleted} 個 Codex profile。"
                    : "找不到符合的 Codex profile。"
            };
        }

        private static CodexProfile FindProfile(string accountId, string email)
        {
            return ListProfiles().FirstOrDefault(p => ProfileMatches(p.AccountId, p.Email, accountId, email));
        }

        private static IEnumerable<string> FindMatchingProfiles(string accountId, string email)
        {
            foreach (var file in FindJsonFiles(CodexProfileDirectory()))
            {
                var (storedId, storedEmail, _) = CodexQuotaReader.ReadAuthFileIdentity(file);
                if (ProfileMatches(storedId, storedEmail, accountId, email))
                {
                    yield return file;
                }
            }
        }

        private static bool ProfileMatches(string storedId, string storedEmail, string requestedId, string requestedEmail)
        {
            return (!string.IsNullOrWhiteSpace(requestedId) &&
                    string.Equals(storedId, requestedId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(requestedEmail) &&
                    string.Equals(storedEmail, requestedEmail, StringComparison.OrdinalIgnoreCase));
        }

        private static void CopyAtomic(string source, string destination)
        {
            var tmp = destination + ".tmp";
            File.Copy(source, tmp, overwrite: true);
            File.Move(tmp, destination, overwrite: true);
        }

        private static bool LaunchCodex(string command)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k {command}",
                    UseShellExecute = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                });
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
    }
}
