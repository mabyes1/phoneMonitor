using System;
using System.IO;
using System.Text.Json;
using PhoneMonitor.Host.Quotas;
using Xunit;

namespace PhoneMonitor.Host.Tests
{
    public sealed class AiQuotaServiceTests
    {
        [Fact]
        public void ReadsCodexCreditBalanceFromLatestRateLimitEvent()
        {
            var fixturePath = Path.Combine(Path.GetTempPath(), $"vibedeck-codex-quota-{Guid.NewGuid():N}.jsonl");
            File.WriteAllLines(fixturePath, new[]
            {
                "{\"timestamp\":\"2026-07-17T06:40:00Z\",\"payload\":{\"rate_limits\":{\"credits\":{\"balance\":\"100.5\",\"unlimited\":false},\"primary\":{\"used_percent\":10,\"window_minutes\":300,\"resets_at\":1780000000}}}}",
                "{\"timestamp\":\"2026-07-17T06:48:41Z\",\"payload\":{\"rate_limits\":{\"credits\":{\"balance\":\"2250.7270450000\",\"unlimited\":false},\"primary\":{\"used_percent\":15,\"window_minutes\":300,\"resets_at\":1780000500}}}}"
            });

            try
            {
                var status = CodexQuotaReader.TryReadCodexQuotaFromFile(fixturePath);

                Assert.Equal(2250.727045d, status.CreditBalance.GetValueOrDefault(), 6);
                Assert.False(status.CreditUnlimited.GetValueOrDefault(true));
            }
            finally
            {
                File.Delete(fixturePath);
            }
        }

        [Fact]
        public void AtomicallyReplacesCodexAuthFileFromSavedProfile()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"vibedeck-codex-profile-{Guid.NewGuid():N}");
            var sourcePath = Path.Combine(tempDirectory, "saved-profile.json");
            var destinationPath = Path.Combine(tempDirectory, "auth.json");
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(sourcePath, "saved account");
            File.WriteAllText(destinationPath, "old account");

            try
            {
                CodexAccountStore.CopyAtomic(sourcePath, destinationPath);

                Assert.Equal("saved account", File.ReadAllText(destinationPath));
                Assert.False(File.Exists(destinationPath + ".tmp"));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void CodexProfileSerializationDoesNotExposeLocalFilePath()
        {
            var profile = new CodexAccountStore.CodexProfile
            {
                AccountId = "account-1",
                Email = "member@example.com",
                Tier = "plus",
                Path = @"C:\\private\\auth.json",
                IsActive = true
            };

            var json = JsonSerializer.Serialize(profile);

            Assert.DoesNotContain("Path", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CodexProfilesStayInCurrentUsersLocalApplicationData()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppPaths.ProductName,
                "codex",
                "profiles");

            Assert.Equal(expected, QuotaPaths.CodexProfileDirectory());
        }

        [Fact]
        public void CodexAccountSwitchNeverTargetsChatGptDesktopProcess()
        {
            Assert.All(CliProcessManager.CodexProcessNames, processName =>
                Assert.Equal("codex", processName, ignoreCase: true));
        }
    }
}
