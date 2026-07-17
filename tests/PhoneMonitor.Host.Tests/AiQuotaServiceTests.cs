using System;
using System.IO;
using System.Reflection;
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
                var method = typeof(AiQuotaService).GetMethod("TryReadCodexQuotaFromFile", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);

                var status = Assert.IsType<AiQuotaStatus>(method.Invoke(new AiQuotaService(), new object[] { fixturePath }));

                Assert.Equal(2250.727045d, status.CreditBalance.GetValueOrDefault(), 6);
                Assert.False(status.CreditUnlimited.GetValueOrDefault(true));
            }
            finally
            {
                File.Delete(fixturePath);
            }
        }
    }
}
