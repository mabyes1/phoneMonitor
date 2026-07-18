using System;
using System.IO;

namespace PhoneMonitor.Host.Quotas
{
    /// <summary>
    /// Central filesystem path resolution for VibeDeck quota storage.
    /// </summary>
    internal static class QuotaPaths
    {
        internal static string PhoneMonitorQuotaRoot()
        {
            return AppPaths.EnsureDirectory(AppPaths.QuotasDirectory);
        }

        internal static string AgyExecutablePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "agy",
                "bin",
                "agy.exe");
        }

        internal static string AgyAccountStoreDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "agy", "accounts");
        }

        internal static string AgyQuotaCacheDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "agy", "cache");
        }

        internal static string CodexQuotaCacheDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "codex", "accounts");
        }

        internal static string CodexProfileDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppPaths.ProductName,
                "codex",
                "profiles");
        }

        internal static string CodexHome()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");
        }

        internal static string CodexAuthFile()
        {
            return Path.Combine(CodexHome(), "auth.json");
        }

        internal static string AgyGoogleOAuthSecretsPath()
        {
            return Path.Combine(
                AppPaths.EnsureDirectory(AppPaths.SecretsDirectory),
                "agy-google-oauth.json");
        }
    }
}
