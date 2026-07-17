using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneMonitor.Host.Quotas
{
    /// <summary>
    /// Orchestrates the AI quota snapshot by composing the per-provider readers
    /// (Codex + AGY) and exposing the account/OAuth actions used by the HTTP layer.
    /// Provider logic lives in CodexQuotaReader and AgyQuotaService.
    /// </summary>
    public sealed class AiQuotaService
    {
        private readonly AgyQuotaService agy = new AgyQuotaService();

        public Task<QuotaSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return BuildSnapshotAsync(false, cancellationToken);
        }

        public Task<QuotaSnapshot> RefreshSnapshotAsync(CancellationToken cancellationToken)
        {
            return BuildSnapshotAsync(true, cancellationToken);
        }

        private async Task<QuotaSnapshot> BuildSnapshotAsync(bool forceAgyRefresh, CancellationToken cancellationToken)
        {
            var snapshot = new QuotaSnapshot
            {
                Providers = new List<AiQuotaStatus>()
            };
            snapshot.Providers.AddRange(CodexQuotaReader.ReadCodexQuotas());
            // Claude Code: detection-only was a dead end (no ingest path). Do not surface until real quota exists.
            snapshot.Providers.AddRange(await agy.ReadAgyQuotasAsync(forceAgyRefresh, cancellationToken));

            return snapshot;
        }

        public AgyQuotaService.AgyImportResult ImportAgyAccountsFromAntigravity()
        {
            return agy.ImportAgyAccountsFromAntigravity();
        }

        public AgyQuotaService.AgyOAuthStartResult StartAgyOAuth(string redirectUri, bool openBrowser)
        {
            return agy.StartAgyOAuth(redirectUri, openBrowser);
        }

        public Task<AgyQuotaService.AgyOAuthCallbackResult> CompleteAgyOAuthAsync(
            string state,
            string code,
            string error,
            string errorDescription,
            CancellationToken cancellationToken)
        {
            return agy.CompleteAgyOAuthAsync(state, code, error, errorDescription, cancellationToken);
        }

        public AgyQuotaService.AgyAccountActionResult OpenAgyCli(string accountId, string email, bool openWindow = true)
        {
            return agy.OpenAgyCli(accountId, email, openWindow);
        }

        public AgyQuotaService.AgyAccountActionResult DeleteAgyAccount(string accountId, string email)
        {
            return agy.DeleteAgyAccount(accountId, email);
        }

        public AgyQuotaService.AgyAccountActionResult DeleteCodexAccount(string accountId, string email)
        {
            return agy.DeleteCodexAccount(accountId, email);
        }
    }
}
