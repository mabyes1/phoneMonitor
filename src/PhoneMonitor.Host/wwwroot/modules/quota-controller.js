export function createQuotaController({
  elements,
  getActiveMode,
  fetchJsonOrThrow,
  isTrustRequiredError,
  renderSnapshot,
  renderErrorHelp,
}) {
  let oauthPollTimer = null;

  async function refresh(options = {}) {
    if (getActiveMode() !== "quota") return;

    try {
      const endpoint = options.force ? "/api/quotas/refresh" : "/api/quotas";
      const init = options.force ? { method: "POST" } : undefined;
      renderSnapshot(await fetchJsonOrThrow(endpoint, init));
    } catch (error) {
      const requiresTrust = isTrustRequiredError(error);
      elements.quotaSummary.textContent = requiresTrust
        ? "請先配對手機，才能查看 AI 額度。"
        : error.message || "額度來源無法使用。";
      elements.quotaUpdated.textContent = "--";
      if (elements.quotaHelp) {
        elements.quotaHelp.replaceChildren();
        elements.quotaHelp.append(renderErrorHelp(error, requiresTrust));
      }
      elements.quotaGrid.replaceChildren();
    }
  }

  function startOAuthPolling() {
    if (oauthPollTimer) clearInterval(oauthPollTimer);

    let attempts = 0;
    oauthPollTimer = setInterval(async () => {
      attempts += 1;
      if (getActiveMode() !== "quota" || attempts > 40) {
        clearInterval(oauthPollTimer);
        oauthPollTimer = null;
        return;
      }

      await refresh({ force: true });
    }, 3000);
  }

  function stopOAuthPolling() {
    if (!oauthPollTimer) return;
    clearInterval(oauthPollTimer);
    oauthPollTimer = null;
  }

  return {
    refresh,
    startOAuthPolling,
    stopOAuthPolling,
  };
}
