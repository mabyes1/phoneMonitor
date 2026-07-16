const SOURCE_STORAGE_KEY = "phoneMonitorDashboardQuotaSource.v1";

export function createQuotaMiniCardController({ elements, fetchJsonOrThrow }) {
  const { select, value, bar, reset, state } = elements;
  let snapshot = null;
  let selectedKey = localStorage.getItem(SOURCE_STORAGE_KEY) || "";

  function familyOf(provider) {
    const family = String(provider?.Family || provider?.family || "").toLowerCase();
    if (family) return family;
    const id = String(provider?.Id || provider?.id || "").toLowerCase();
    return id.startsWith("agy") ? "agy" : id.startsWith("codex") ? "codex" : id;
  }

  function providerKey(provider) {
    return [
      familyOf(provider),
      provider?.AccountId || provider?.accountId || "",
      provider?.Id || provider?.id || "",
    ].join(":");
  }

  function providerLabel(provider) {
    const family = familyOf(provider) === "codex" ? "CODEX" : familyOf(provider).toUpperCase() || "AI";
    const providerName = String(provider?.Label || provider?.label || "").trim();
    const email = String(provider?.AccountEmail || provider?.accountEmail || "").trim();
    const details = [family];
    if (family === "AGY" && providerName) details.push(providerName);
    if (email) details.push(email);
    return details.join(" · ");
  }

  function remainingPercent(windowData) {
    const remaining = windowData?.RemainingPercent ?? windowData?.remainingPercent;
    const used = windowData?.UsedPercent ?? windowData?.usedPercent;
    if (Number.isFinite(remaining)) return Math.max(0, Math.min(100, remaining));
    if (Number.isFinite(used)) return Math.max(0, Math.min(100, 100 - used));
    return Number.NaN;
  }

  function render() {
    const providers = snapshot?.Providers || snapshot?.providers || [];
    const sorted = [...providers].sort((left, right) => {
      const active = Number(Boolean(right.IsActive ?? right.isActive)) - Number(Boolean(left.IsActive ?? left.isActive));
      if (active) return active;
      return providerLabel(left).localeCompare(providerLabel(right));
    });
    const previous = selectedKey;
    select.replaceChildren();
    for (const provider of sorted) {
      const option = document.createElement("option");
      option.value = providerKey(provider);
      option.textContent = providerLabel(provider);
      select.append(option);
    }

    if (!sorted.some(provider => providerKey(provider) === selectedKey)) {
      const active = sorted.find(provider => Boolean(provider.IsActive ?? provider.isActive));
      const usableCodex = sorted.find(provider => familyOf(provider) === "codex" && String(provider.State || provider.state).toLowerCase() === "ok");
      selectedKey = providerKey(active || usableCodex || sorted[0] || {});
    }
    select.value = selectedKey;
    const selected = sorted.find(provider => providerKey(provider) === selectedKey);
    const primary = selected?.Primary || selected?.primary || null;
    const remaining = remainingPercent(primary);
    value.textContent = Number.isFinite(remaining) ? `${Math.round(remaining)}%` : "--";
    bar.style.width = `${Number.isFinite(remaining) ? remaining : 0}%`;
    const resetsAt = primary?.ResetsAt || primary?.resetsAt;
    reset.textContent = resetsAt
      ? `重置 ${new Date(resetsAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`
      : selected ? "尚無 5 小時資料" : "尚無額度來源";
    state.textContent = selected ? "5 小時剩餘" : "等待來源";
    select.disabled = !sorted.length;
    if (previous !== selectedKey && selectedKey) localStorage.setItem(SOURCE_STORAGE_KEY, selectedKey);
  }

  select.addEventListener("change", () => {
    selectedKey = select.value;
    localStorage.setItem(SOURCE_STORAGE_KEY, selectedKey);
    render();
  });

  return {
    renderSnapshot(nextSnapshot) {
      snapshot = nextSnapshot || {};
      render();
    },
    async refresh() {
      try {
        const nextSnapshot = await fetchJsonOrThrow("/api/quotas");
        snapshot = nextSnapshot || {};
        render();
        return nextSnapshot;
      } catch (error) {
        value.textContent = "--";
        bar.style.width = "0%";
        reset.textContent = error?.message || "額度讀取失敗";
        state.textContent = "來源離線";
        return null;
      }
    },
  };
}
