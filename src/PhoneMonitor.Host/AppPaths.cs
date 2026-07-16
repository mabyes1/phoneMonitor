using System;
using System.IO;

namespace PhoneMonitor.Host
{
    /// <summary>
    /// Resolves product data directories for source development and Setup installs.
    /// </summary>
    public static class AppPaths
    {
        public const string ProductName = "VibeDeck";
        public const string InstallMarkerFileName = "product-install.json";
        public const string WebUiUrl = "http://127.0.0.1:5000";
        public const string LegacyProductFolderName = "PhoneMonitor";

        private static readonly Lazy<string> DataRootLazy = new Lazy<string>(ResolveDataRoot);
        private static readonly Lazy<bool> IsInstalledLazy = new Lazy<bool>(DetectInstalledLayout);

        public static string DataRoot => DataRootLazy.Value;

        public static bool IsInstalledLayout => IsInstalledLazy.Value;

        public static string CertsDirectory => Path.Combine(DataRoot, "certs");

        public static string DevicesDirectory => Path.Combine(DataRoot, "devices");

        public static string CustomSourcesDirectory => Path.Combine(DataRoot, "custom-sources");

        public static string WindowsNotificationsDirectory => Path.Combine(DataRoot, "windows-notifications");

        public static string DashboardDirectory => Path.Combine(DataRoot, "dashboard");

        public static string QuotasDirectory => Path.Combine(DataRoot, "quotas");

        public static string SecretsDirectory => Path.Combine(DataRoot, "secrets");

        public static string LogsDirectory => Path.Combine(DataRoot, "logs");

        public static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// First installed product start: pull legacy LocalAppData\PhoneMonitor state into
        /// ProgramData\VibeDeck so browser/PWA pairings and the already-trusted root CA
        /// survive Setup. Only fills empty product state; never clobbers real product data.
        /// </summary>
        public static string TryMigrateLegacyData()
        {
            try
            {
                if (!IsInstalledLayout)
                {
                    return null;
                }

                var targetRoot = DataRoot;
                var legacyRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    LegacyProductFolderName);
                if (!Directory.Exists(legacyRoot))
                {
                    return null;
                }

                EnsureDirectory(targetRoot);
                var migrated = 0;

                var legacyDevices = Path.Combine(legacyRoot, "devices", "trusted-devices.json");
                var targetDevices = Path.Combine(targetRoot, "devices", "trusted-devices.json");
                var productDevicesEmpty = !File.Exists(targetDevices) ||
                    new FileInfo(targetDevices).Length < 8 ||
                    File.ReadAllText(targetDevices).IndexOf("DeviceId", StringComparison.OrdinalIgnoreCase) < 0;

                // Fresh Setup often creates empty ProgramData then mints NEW certs before
                // any phone re-pairs. If product has no devices yet, restore the whole
                // legacy trust pack (devices + certs) so BOOX/PWA keep working.
                if (productDevicesEmpty && File.Exists(legacyDevices))
                {
                    EnsureDirectory(Path.GetDirectoryName(targetDevices));
                    File.Copy(legacyDevices, targetDevices, overwrite: true);
                    migrated++;

                    foreach (var name in new[]
                    {
                        "phone-monitor-root.pfx",
                        "phone-monitor-root.cer",
                        "phone-monitor-host.pfx",
                        "phone-monitor-host.cer",
                        "phone-monitor-certificate-state.json"
                    })
                    {
                        var source = Path.Combine(legacyRoot, "certs", name);
                        var destination = Path.Combine(targetRoot, "certs", name);
                        if (!File.Exists(source))
                        {
                            continue;
                        }

                        EnsureDirectory(Path.GetDirectoryName(destination));
                        File.Copy(source, destination, overwrite: true);
                        migrated++;
                    }
                }
                else
                {
                    foreach (var relative in new[]
                    {
                        Path.Combine("devices", "trusted-devices.json"),
                        Path.Combine("certs", "phone-monitor-root.pfx"),
                        Path.Combine("certs", "phone-monitor-root.cer"),
                        Path.Combine("certs", "phone-monitor-host.pfx"),
                        Path.Combine("certs", "phone-monitor-host.cer"),
                        Path.Combine("certs", "phone-monitor-certificate-state.json"),
                        Path.Combine("windows-notifications", "settings.json")
                    })
                    {
                        var source = Path.Combine(legacyRoot, relative);
                        var destination = Path.Combine(targetRoot, relative);
                        if (!File.Exists(source) || File.Exists(destination))
                        {
                            continue;
                        }

                        EnsureDirectory(Path.GetDirectoryName(destination));
                        File.Copy(source, destination, overwrite: false);
                        migrated++;
                    }
                }

                migrated += CopyDirectoryIfTargetEmpty(
                    Path.Combine(legacyRoot, "quotas"),
                    Path.Combine(targetRoot, "quotas"));
                migrated += CopyDirectoryIfTargetEmpty(
                    Path.Combine(legacyRoot, "custom-sources"),
                    Path.Combine(targetRoot, "custom-sources"));

                return migrated > 0
                    ? $"Migrated {migrated} legacy PhoneMonitor item(s) into {targetRoot}."
                    : null;
            }
            catch (Exception ex)
            {
                return $"Legacy data migration skipped: {ex.Message}";
            }
        }

        private static int CopyDirectoryIfTargetEmpty(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                return 0;
            }

            if (Directory.Exists(targetDir) && Directory.GetFileSystemEntries(targetDir).Length > 0)
            {
                return 0;
            }

            EnsureDirectory(targetDir);
            var count = 0;
            foreach (var sourcePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, sourcePath);
                var destination = Path.Combine(targetDir, relative);
                EnsureDirectory(Path.GetDirectoryName(destination));
                if (File.Exists(destination))
                {
                    continue;
                }

                File.Copy(sourcePath, destination, overwrite: false);
                count++;
            }

            return count;
        }

        private static string ResolveDataRoot()
        {
            var env = Environment.GetEnvironmentVariable("VIBEDECK_DATA")
                ?? Environment.GetEnvironmentVariable("PHONEMONITOR_DATA");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return Path.GetFullPath(env.Trim());
            }

            // Setup installs keep product state outside the replaceable app directory.
            if (IsInstalledLayout)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    ProductName);
            }

            // Source / portable runs keep the existing LocalAppData layout.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhoneMonitor");
        }

        private static bool DetectInstalledLayout()
        {
            try
            {
                var baseDirectory = AppContext.BaseDirectory;
                if (string.IsNullOrWhiteSpace(baseDirectory))
                {
                    return false;
                }

                if (File.Exists(Path.Combine(baseDirectory, InstallMarkerFileName)))
                {
                    return true;
                }

                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if ((!string.IsNullOrWhiteSpace(programFiles) &&
                     baseDirectory.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(programFilesX86) &&
                     baseDirectory.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch
            {
                // Fall through to non-installed layout.
            }

            return false;
        }

    }
}
