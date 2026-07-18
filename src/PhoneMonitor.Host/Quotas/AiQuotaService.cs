using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneMonitor.Host.Quotas
{
    /// <summary>
    /// Composes provider-specific quota readers and exposes the quota actions used
    /// by the HTTP layer. Provider implementation lives in the focused modules.
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

        public AgyQuotaService.AgyAccountActionResult OpenAgyCli(bool openWindow = true)
        {
            return agy.OpenAgyCli(openWindow);
        }

        public AgyQuotaService.AgyAccountActionResult DeleteAgyAccount(string accountId, string email)
        {
            return agy.DeleteAgyAccount(accountId, email);
        }

        public AgyQuotaService.AgyAccountActionResult DeleteCodexAccount(string accountId, string email)
        {
            return agy.DeleteCodexAccount(accountId, email);
        }

        internal IReadOnlyList<CodexAccountStore.CodexProfile> ListCodexProfiles()
        {
            return CodexAccountStore.ListProfiles();
        }

        internal CodexAccountStore.CodexActionResult SwitchCodexAccount(string accountId, string email)
        {
            return CodexAccountStore.SwitchTo(accountId, email);
        }

        internal CodexAccountStore.CodexActionResult ReAuthCodex()
        {
            return CodexAccountStore.ReAuth();
        }

        internal CodexAccountStore.CodexActionResult DeleteCodexProfile(string accountId, string email)
        {
            return CodexAccountStore.DeleteProfile(accountId, email);
        }
    }
}
