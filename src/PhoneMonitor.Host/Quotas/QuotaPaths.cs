using System;
using System.IO;

namespace PhoneMonitor.Host.Quotas
{
    /// <summary>
    /// Central filesystem path resolution for PhoneMonitor quota storage.
    /// Extracted from AiQuotaService (refactor/quota-split step 1). No behavior change.
    /// </summary>
    internal static class QuotaPaths
    {
        private const string PhoneMonitorQuotaRootName = "PhoneMonitor";
        private const string PhoneMonitorQuotaFolderName = "quotas";

        internal static string PhoneMonitorQuotaRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                PhoneMonitorQuotaRootName,
                PhoneMonitorQuotaFolderName);
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

        internal static string AgyLauncherDirectory()
        {
            return Path.Combine(PhoneMonitorQuotaRoot(), "agy", "launch");
        }

        internal static string AgyGoogleOAuthSecretsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                PhoneMonitorQuotaRootName,
                "secrets",
                "agy-google-oauth.json");
        }
    }
}
