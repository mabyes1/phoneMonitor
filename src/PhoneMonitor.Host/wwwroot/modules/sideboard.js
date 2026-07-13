export function createSideboardController({
  elements,
  fetchJsonOrThrow,
  isTrustRequiredError,
  getActiveMode,
  setText,
  setBar,
  formatters,
}) {
  const {
    sideHeadline,
    sideSummary,
    sideError,
    sideLoad,
    sideHost,
    sideUptime,
    sideHealth,
    sideCpu,
    sideCpuSub,
    sideCpuBar,
    sideRam,
    sideRamSub,
    sideRamBar,
    sideGpu,
    sideGpuSub,
    sideGpuBar,
    sideVram,
    sideVramSub,
    sideVramBar,
    sideNet,
    sideNetSub,
    sideNetBar,
    sideDisk,
    sideDiskSub,
    sideDiskBar,
    sideDiskIo,
    sideWeather,
    sideWeatherSub,
    sideProcessList,
    sideWorkList,
  } = elements;
  const {
    formatPercent,
    formatGb,
    formatMbps,
    formatTemperature,
    describeWeatherCode,
    formatWeatherLocation,
    formatSeconds,
    averagePercent,
  } = formatters;

  function renderProcesses(processes) {
    sideProcessList.replaceChildren();
    for (const process of processes.slice(0, 4)) {
      const li = document.createElement("li");
      const name = document.createElement("span");
      const value = document.createElement("b");
      name.textContent = process.name || process.Name || "process";
      value.textContent = process.memoryMb || process.MemoryMb
        ? `${Math.round(process.memoryMb || process.MemoryMb)}MB`
        : "";
      li.append(name, value);
      sideProcessList.append(li);
    }

    if (!sideProcessList.children.length) {
      const li = document.createElement("li");
      li.textContent = "沒有程序資料";
      sideProcessList.append(li);
    }
  }

  function renderWorkPulse(workPulse) {
    sideWorkList.replaceChildren();
    const focus = workPulse?.focus || [];
    const recent = workPulse?.recent || [];
    const items = focus.length ? focus.map(item => item.text) : recent.map(item => item.text);

    for (const text of items.slice(0, 4)) {
      const li = document.createElement("li");
      li.textContent = text;
      sideWorkList.append(li);
    }

    if (!sideWorkList.children.length) {
      const li = document.createElement("li");
      li.textContent = workPulse?.summary?.headline || "目前沒有工作脈搏。";
      sideWorkList.append(li);
    }
  }

  function renderStats(stats) {
    const cpu = stats.cpu || {};
    const memory = stats.memory || {};
    const gpu = stats.gpu || {};
    const network = stats.network || {};
    const disk = stats.disk || {};
    const weather = stats.weather || {};
    const system = stats.system || {};
    const load = averagePercent([
      cpu.usagePercent,
      memory.usagePercent,
      gpu.usagePercent,
      disk.usagePercent,
    ]);

    sideHeadline.textContent = system.hostname || "VibeDeck 資訊板";
    sideSummary.textContent = `${new Date(stats.generatedAt || Date.now()).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
    sideLoad.textContent = formatPercent(load);
    sideHost.textContent = `主機 ${system.localIp || "--"}`;
    sideUptime.textContent = `已運行 ${formatSeconds(system.uptimeSeconds)}`;
    sideHealth.textContent = stats.error ? `收集器：${stats.error}` : "收集器正常";

    setText(sideCpu, formatPercent(cpu.usagePercent));
    setText(sideCpuSub, `溫度 ${formatTemperature(cpu.temperatureC)}`);
    setBar(sideCpuBar, cpu.usagePercent);

    setText(sideRam, formatPercent(memory.usagePercent));
    setText(sideRamSub, `${formatGb(memory.usedGb)} / ${formatGb(memory.totalGb)}`);
    setBar(sideRamBar, memory.usagePercent);

    setText(sideGpu, formatPercent(gpu.usagePercent));
    setText(sideGpuSub, [gpu.name, formatTemperature(gpu.temperatureC)].filter(Boolean).join(" · "));
    setBar(sideGpuBar, gpu.usagePercent);

    setText(sideVram, formatPercent(gpu.memoryUsagePercent));
    setText(sideVramSub, `${Math.round(gpu.memoryUsedMb || 0)} / ${Math.round(gpu.memoryTotalMb || 0)} MB`);
    setBar(sideVramBar, gpu.memoryUsagePercent);

    setText(sideNet, `${formatMbps(network.downMbps)}↓`);
    setText(sideNetSub, `${formatMbps(network.upMbps)} Mbps 上傳`);
    setBar(sideNetBar, Math.min(100, Math.max(network.downMbps || 0, network.upMbps || 0) * 5));

    setText(sideDisk, formatPercent(disk.usagePercent));
    setText(sideDiskSub, `${disk.drive || "磁碟"} · ${formatGb(disk.usedGb)} / ${formatGb(disk.totalGb)}`);
    setBar(sideDiskBar, disk.usagePercent);

    const weatherTemp = Number.isFinite(weather.temperatureC) ? formatTemperature(weather.temperatureC) : "";
    const weatherFeels = Number.isFinite(weather.apparentTemperatureC) ? formatTemperature(weather.apparentTemperatureC) : "--";
    const weatherDescription = describeWeatherCode(weather.weatherCode ?? weather.WeatherCode, weather.description || weather.Description);
    const weatherParts = [
      formatWeatherLocation(weather.location || weather.Location),
      weatherDescription,
      weatherTemp,
    ].filter(Boolean);
    setText(sideWeather, weatherParts.length > 1 ? weatherParts.join(" · ") : "天氣資料暫不可用");
    setText(sideWeatherSub, `體感 ${weatherFeels}`);
    setText(sideDiskIo, `磁碟 IO 讀 ${formatMbps(disk.readMBps)} / 寫 ${formatMbps(disk.writeMBps)} MB/s`);
    renderProcesses(stats.processes || []);
  }

  async function refresh() {
    if (getActiveMode() !== "sideboard") return;

    try {
      const [stats, workPulse] = await Promise.all([
        fetchJsonOrThrow("/api/sideboard/stats"),
        fetchJsonOrThrow("/api/sideboard/work-pulse").catch(() => null),
      ]);

      sideError.textContent = "";
      renderStats(stats);
      renderWorkPulse(workPulse);
    } catch (error) {
      if (isTrustRequiredError(error)) {
        sideHeadline.textContent = "資訊板已鎖定";
        sideSummary.textContent = "請先配對手機。";
        sideError.textContent = "需要信任裝置。";
        sideWorkList.replaceChildren();
        return;
      }

      sideHeadline.textContent = "資訊板無法使用";
      sideSummary.textContent = "VibeDeck 無法讀取本機電腦資訊。";
      sideError.textContent = error.message || "資料收集器無法使用。";
      sideWorkList.replaceChildren();
    }
  }

  return { refresh };
}
