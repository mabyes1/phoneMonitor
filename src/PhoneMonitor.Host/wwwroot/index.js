    const screen = document.getElementById("screen");
    const statusText = document.getElementById("status");
    const dot = document.getElementById("dot");
    const rotation = document.getElementById("rotation");
    const orientation = document.getElementById("orientation");
    const displayMode = document.getElementById("displayMode");
    const sideboardMode = document.getElementById("sideboardMode");
    const quotaMode = document.getElementById("quotaMode");
    const refresh = document.getElementById("refresh");
    const fullscreen = document.getElementById("fullscreen");
    const exitViewer = document.getElementById("exitViewer");
    const streamPreset = document.getElementById("streamPreset");
    const streamFps = document.getElementById("streamFps");
    const streamQuality = document.getElementById("streamQuality");
    const applyStream = document.getElementById("applyStream");
    const modePreset = document.getElementById("modePreset");
    const modeWidth = document.getElementById("modeWidth");
    const modeHeight = document.getElementById("modeHeight");
    const modeRefresh = document.getElementById("modeRefresh");
    const applyMode = document.getElementById("applyMode");
    const driverState = document.getElementById("driverState");
    const qrCode = document.getElementById("qrCode");
    const qrCaption = document.getElementById("qrCaption");
    const prettyLink = document.getElementById("prettyLink");
    const httpsLink = document.getElementById("httpsLink");
    const httpsCertLink = document.getElementById("httpsCertLink");
    const httpLink = document.getElementById("httpLink");
    const androidApkLink = document.getElementById("androidApkLink");
    const androidApkQrLink = document.getElementById("androidApkQrLink");
    const androidApkShaLink = document.getElementById("androidApkShaLink");
    const nativeAppLink = document.getElementById("nativeAppLink");
    const nativeCertLink = document.getElementById("nativeCertLink");
    const nativeQrLink = document.getElementById("nativeQrLink");
    const pairPhone = document.getElementById("pairPhone");
    const copyNativePairingLink = document.getElementById("copyNativePairingLink");
    const launchDeckWindow = document.getElementById("launchDeckWindow");
    const pairingStepInstall = document.getElementById("pairingStepInstall");
    const pairingStepPair = document.getElementById("pairingStepPair");
    const pairingStepOpen = document.getElementById("pairingStepOpen");
    const trustState = document.getElementById("trustState");
    const streamCapabilityState = document.getElementById("streamCapabilityState");
    const trustedDevicesPanel = document.getElementById("trustedDevicesPanel");
    const pendingPairingPanel = document.getElementById("pendingPairingPanel");
    const pendingPairingList = document.getElementById("pendingPairingList");
    const trustedDeviceList = document.getElementById("trustedDeviceList");
    const clearTrustedDevices = document.getElementById("clearTrustedDevices");
    const refreshTrustedDevices = document.getElementById("refreshTrustedDevices");
    const wakeState = document.getElementById("wakeState");
    const apkState = document.getElementById("apkState");
    const appState = document.getElementById("appState");
    const deviceState = document.getElementById("deviceState");
    const displayView = document.getElementById("displayView");
    const sideboardView = document.getElementById("sideboardView");
    const quotaView = document.getElementById("quotaView");
    const sideboardShell = document.getElementById("sideboardShell");
    const rtcScreen = document.getElementById("rtcScreen");
    const sideHeadline = document.getElementById("sideHeadline");
    const sideSummary = document.getElementById("sideSummary");
    const sideError = document.getElementById("sideError");
    const sideLoad = document.getElementById("sideLoad");
    const sideHost = document.getElementById("sideHost");
    const sideUptime = document.getElementById("sideUptime");
    const sideHealth = document.getElementById("sideHealth");
    const sideCpu = document.getElementById("sideCpu");
    const sideCpuSub = document.getElementById("sideCpuSub");
    const sideCpuBar = document.getElementById("sideCpuBar");
    const sideRam = document.getElementById("sideRam");
    const sideRamSub = document.getElementById("sideRamSub");
    const sideRamBar = document.getElementById("sideRamBar");
    const sideGpu = document.getElementById("sideGpu");
    const sideGpuSub = document.getElementById("sideGpuSub");
    const sideGpuBar = document.getElementById("sideGpuBar");
    const sideVram = document.getElementById("sideVram");
    const sideVramSub = document.getElementById("sideVramSub");
    const sideVramBar = document.getElementById("sideVramBar");
    const sideNet = document.getElementById("sideNet");
    const sideNetSub = document.getElementById("sideNetSub");
    const sideNetBar = document.getElementById("sideNetBar");
    const sideDisk = document.getElementById("sideDisk");
    const sideDiskSub = document.getElementById("sideDiskSub");
    const sideDiskBar = document.getElementById("sideDiskBar");
    const sideDiskIo = document.getElementById("sideDiskIo");
    const sideWeather = document.getElementById("sideWeather");
    const sideWeatherSub = document.getElementById("sideWeatherSub");
    const sideProcessList = document.getElementById("sideProcessList");
    const sideWorkList = document.getElementById("sideWorkList");
    const quotaSummary = document.getElementById("quotaSummary");
    const quotaUpdated = document.getElementById("quotaUpdated");
    const quotaTabs = document.getElementById("quotaTabs");
    const quotaHelp = document.getElementById("quotaHelp");
    const quotaGrid = document.getElementById("quotaGrid");
    const wsBase = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}`;
    let selectedDisplayName = "";
    let lastUrl = null;
    let inputSocket = null;
    let videoSocket = null;
    // JPEG frames must be decoded one at a time on Safari.  Feeding every
    // WebSocket message straight into <img> builds a large WebKit decode and
    // object-URL queue, which turns a live view into seconds of latency.
    let pendingJpegFrame = null;
    let jpegFrameDecoding = false;
    let activeJpegObjectUrl = null;
    let jpegFallbackReason = "";
    let rtcPeer = null;
    let rtcActive = false;
    let rtcConnectGeneration = 0;
    let rtcDisconnectTimer = null;
    let rtcStatsTimer = null;
    let wakeLock = null;
    let keepAwakeDesired = localStorage.getItem("phoneMonitorKeepAwake") !== "0";
    let keepAwakeVideoPlaying = false;
    let keepAwakeWatchTimer = null;
    let streamStats = null;
    let activeMode = "display";
    let sideboardTimer = null;
    let quotaTimer = null;
    let dashboardEvents = null;
    const dashboardRefreshState = {
      sideboard: { last: 0, timer: null, dirty: false },
      quota: { last: 0, timer: null, dirty: false }
    };
    let quotaOAuthPollTimer = null;
    let actionToken = "";
    let actionHeaderName = "X-PhoneMonitor-Action-Token";
    const DEVICE_TOKEN_KEY = "phoneMonitorDeviceToken";
    const DEVICE_ID_KEY = "phoneMonitorDeviceId";
    const DEVICE_COOKIE = "PhoneMonitor-Device-Token";
    const IPHONE_XS_EXACT_PRESET = "iphonexs-css-812x375";
    const IPHONE_XS_ASPECT_VERSION = "1";

    function readCookie(name) {
      const parts = (`; ${document.cookie || ""}`).split(`; ${name}=`);
      if (parts.length < 2) return "";
      return decodeURIComponent(parts.pop().split(";").shift() || "");
    }

    function writeCookie(name, value, days) {
      const maxAge = Math.max(1, Math.floor(days * 24 * 60 * 60));
      const secure = location.protocol === "https:" ? "; Secure" : "";
      document.cookie = `${name}=${encodeURIComponent(value || "")}; Path=/; Max-Age=${maxAge}; SameSite=Lax${secure}`;
    }

    function loadStoredDeviceCredentials() {
      const token = localStorage.getItem(DEVICE_TOKEN_KEY)
        || sessionStorage.getItem(DEVICE_TOKEN_KEY)
        || readCookie(DEVICE_COOKIE)
        || "";
      const id = localStorage.getItem(DEVICE_ID_KEY)
        || sessionStorage.getItem(DEVICE_ID_KEY)
        || "";
      return { token, id };
    }

    function persistDeviceCredentials(token, id) {
      const nextToken = (token || "").trim();
      const nextId = (id || "").trim();
      deviceToken = nextToken;
      deviceId = nextId;
      try {
        if (nextToken) {
          localStorage.setItem(DEVICE_TOKEN_KEY, nextToken);
          sessionStorage.setItem(DEVICE_TOKEN_KEY, nextToken);
          writeCookie(DEVICE_COOKIE, nextToken, 400);
        } else {
          localStorage.removeItem(DEVICE_TOKEN_KEY);
          sessionStorage.removeItem(DEVICE_TOKEN_KEY);
          writeCookie(DEVICE_COOKIE, "", 0);
        }
        if (nextId) {
          localStorage.setItem(DEVICE_ID_KEY, nextId);
          sessionStorage.setItem(DEVICE_ID_KEY, nextId);
        } else {
          localStorage.removeItem(DEVICE_ID_KEY);
          sessionStorage.removeItem(DEVICE_ID_KEY);
        }
      } catch {
        // Private mode / storage blocked: cookie may still work.
        if (nextToken) writeCookie(DEVICE_COOKIE, nextToken, 400);
      }
      notifyNativeDeviceTrust();
    }

    const storedDevice = loadStoredDeviceCredentials();
    let deviceToken = storedDevice.token;
    let deviceId = storedDevice.id;
    let deviceHeaderName = "X-PhoneMonitor-Device-Token";
    let deviceTrusted = false;
    let deviceLocalRequest = false;
    let androidApkAvailable = false;
    let androidApkQrUrl = "/qr/apk.svg";
    let pairingQrActive = false;
    let quotaSnapshotData = null;
    let quotaActiveTab = localStorage.getItem("phoneMonitorQuotaTab") || "agy";
    const quotaAccountIndexByTab = {};
    const quotaActionStatusByKey = new Map();
    let quotaSwipeStartX = null;
    let installPromptEvent = null;
    let approvalPollTimer = null;
    let pendingApprovalTimer = null;
    let serviceWorkerRegistration = null;
    const touchLongPressMs = 460;
    const touchDragThresholdPx = 12;
    let touchInputState = null;

    function isMobileClient() {
      return isIos() || /Android|Mobile|webOS|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent || "");
    }

    function isNativeShell() {
      try {
        return !!(window.PhoneMonitorShell);
      } catch {
        return false;
      }
    }

    function isEinkClient() {
      const requested = new URLSearchParams(location.search).get("eink");
      return requested === "1" || requested === "true" || /VibeDeck-EInk|BOOX|ONYX/i.test(navigator.userAgent || "");
    }

    function dashboardMinInterval(topic) {
      if (topic === "sideboard") return isEinkClient() ? 8000 : 1000;
      return isEinkClient() ? 20000 : 5000;
    }

    function scheduleDashboardRefresh(topic, immediate = false) {
      const state = dashboardRefreshState[topic];
      if (!state) return;
      state.dirty = true;
      if (document.visibilityState === "hidden" || state.timer) return;
      const elapsed = Date.now() - state.last;
      const delay = immediate ? 0 : Math.max(0, dashboardMinInterval(topic) - elapsed);
      state.timer = setTimeout(async () => {
        state.timer = null;
        if (!state.dirty || document.visibilityState === "hidden") return;
        state.dirty = false;
        state.last = Date.now();
        if (topic === "sideboard") await refreshSideboard();
        else await refreshQuotas();
        if (state.dirty) scheduleDashboardRefresh(topic);
      }, delay);
    }

    function connectDashboardEvents() {
      if (dashboardEvents || typeof EventSource === "undefined") return;
      dashboardEvents = new EventSource("/api/dashboard/events");
      dashboardEvents.addEventListener("sideboard", () => scheduleDashboardRefresh("sideboard"));
      dashboardEvents.addEventListener("quota", () => scheduleDashboardRefresh("quota"));
      dashboardEvents.addEventListener("sync", () => {
        scheduleDashboardRefresh("sideboard");
        scheduleDashboardRefresh("quota");
      });
      dashboardEvents.onerror = () => {
        // EventSource reconnects automatically. Low-frequency fallback timers remain active.
      };
    }

    function defaultStreamPreset() {
      // iPhone Safari JPEG is heavier; prefer battery/balanced over 60fps.
      if (isIos()) return "battery";
      return isMobileClient() || isNativeShell() ? "balanced" : "smooth";
    }

    function applyClientChrome() {
      const nativeShell = isNativeShell();
      const localConsole = Boolean(deviceLocalRequest);
      const phoneClient = !localConsole && (isMobileClient() || nativeShell);
      const ios = isIos();

      document.body.classList.toggle("native-shell", nativeShell);
      document.body.classList.toggle("eink-client", isEinkClient());
      document.body.classList.toggle("phone-client", phoneClient);
      document.body.classList.toggle("ios-client", ios && !localConsole);
      document.body.classList.toggle("device-trusted", Boolean(deviceTrusted) && !localConsole);
      document.body.classList.toggle("pc-console", localConsole);
      document.body.classList.toggle("standalone-app", isStandaloneApp());
      applyForcedLandscape();
      updateIosHomeTip();
    }

    function updateIosHomeTip() {
      const tip = document.getElementById("iosHomeTip");
      if (!tip) return;
      tip.classList.toggle("show-install", isIos() && !isStandaloneApp());
      if (!isIos()) return;
      if (location.protocol !== "https:") {
        tip.innerHTML = "iPhone 必須用 <strong>HTTPS</strong>。請走憑證 → 開啟 HTTPS，不要用 HTTP。";
        return;
      }
      if (!deviceTrusted) {
        tip.innerHTML = "HTTPS 就緒。回 PC 配對並掃 QR，成功後同一頁加入主畫面。";
      } else if (!isStandaloneApp()) {
        tip.innerHTML = "已配對。分享 → <strong>加入主畫面</strong>，打開後點 <strong>長亮 ON</strong>。";
      } else {
        tip.innerHTML = "主畫面模式。用副螢幕時點 <strong>長亮 ON</strong>。";
      }
    }

    function shouldForceLandscape() {
      return orientation?.value === "landscape" &&
        !isDeckWindow() &&
        !deviceLocalRequest &&
        (isMobileClient() || isNativeShell() || document.body.classList.contains("phone-client"));
    }

    function applyForcedLandscape() {
      if (!shouldForceLandscape()) {
        document.documentElement.classList.remove("phone-force-landscape");
        document.body.classList.remove("force-landscape");
        return;
      }

      const physicalWidth = window.visualViewport ? window.visualViewport.width : window.innerWidth;
      const physicalHeight = window.visualViewport ? window.visualViewport.height : window.innerHeight;
      const physicalPortrait = physicalHeight >= physicalWidth;

      document.documentElement.classList.toggle("phone-force-landscape", physicalPortrait);
      document.body.classList.toggle("force-landscape", physicalPortrait);

      // Browser may allow lock only after user gesture / fullscreen; best-effort.
      if (orientation?.value !== "auto" && screen.orientation && typeof screen.orientation.lock === "function") {
        screen.orientation.lock(orientation.value).catch(() => {});
      }
    }

    function isIos() {
      return /iPad|iPhone|iPod/.test(navigator.userAgent) ||
        (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
    }

    function isIphone() {
      return /iPhone|iPod/.test(navigator.userAgent || "");
    }

    function prefersWebRtcDisplay() {
      return isIos() || new URLSearchParams(location.search).get("webrtc") === "1";
    }

    function isStandaloneApp() {
      return window.matchMedia("(display-mode: standalone)").matches ||
        window.navigator.standalone === true;
    }

    function isDeckWindow() {
      const value = new URLSearchParams(location.search).get("deck");
      return value === "1" || value === "true";
    }

    function updateViewportSize() {
      const viewport = window.visualViewport;
      let width = viewport ? viewport.width : window.innerWidth;
      let height = viewport ? viewport.height : window.innerHeight;
      const forceLandscape = shouldForceLandscape() && height >= width;

      // When CSS rotates portrait → landscape, layout metrics are swapped.
      if (forceLandscape) {
        const swapped = width;
        width = height;
        height = swapped;
      }

      document.documentElement.style.setProperty("--viewer-width", `${width}px`);
      document.documentElement.style.setProperty("--viewer-height", `${height}px`);
      document.body.classList.toggle("viewport-portrait", !forceLandscape && height >= width);
      document.body.classList.toggle("viewport-landscape", forceLandscape || width > height);
      applyForcedLandscape();
    }

    function notifyNativeViewerMode(enabled) {
      try {
        if (window.PhoneMonitorShell) {
          window.PhoneMonitorShell.setViewerMode(Boolean(enabled));
        }
      } catch {
      }
    }

    function notifyNativeDeviceTrust() {
      try {
        if (window.PhoneMonitorShell) {
          window.PhoneMonitorShell.setDeviceToken(deviceToken || "", deviceId || "");
        }
      } catch {
      }
    }

    function describeClient() {
      updateInstallState();
      if (isIos()) {
        deviceState.textContent = location.protocol === "https:"
          ? (isStandaloneApp() ? "iPhone HTTPS 主畫面模式。" : "iPhone HTTPS。可分享 → 加入主畫面。")
          : "iPhone 必須改用 HTTPS。";
        return;
      }

      deviceState.textContent = "手機模式。";
    }

    function buildHttpsUrlFromCurrent() {
      const host = location.hostname;
      if (!host || host === "localhost" || host === "127.0.0.1") {
        return "";
      }
      const path = location.pathname || "/index.html";
      const search = location.search || "";
      const hash = location.hash || "";
      return `https://${host}:5443${path}${search}${hash}`;
    }

    function isLoopbackHost() {
      const host = location.hostname;
      return host === "localhost" || host === "127.0.0.1" || host === "[::1]";
    }

    /** iPhone single path: never use HTTP for the app UI. */
    function enforceIosHttpsPath() {
      if (!isIos() || isNativeShell() || isLoopbackHost()) {
        document.body.classList.remove("ios-http-blocked");
        return false;
      }

      if (location.protocol === "https:") {
        document.body.classList.remove("ios-http-blocked");
        return false;
      }

      // HTTP on a real LAN host → block UI and force HTTPS gate.
      document.body.classList.add("ios-http-blocked");
      const httpsUrl = buildHttpsUrlFromCurrent();
      const openBtn = document.getElementById("iosGateOpenHttps");
      const link = document.getElementById("iosGateHttpsUrl");
      const cert = document.getElementById("iosGateCertLink");
      if (link && httpsUrl) {
        link.href = httpsUrl;
        link.textContent = httpsUrl;
      }
      if (cert) {
        cert.href = "/cert/phone-monitor-root.cer";
      }
      if (openBtn) {
        openBtn.onclick = () => {
          if (httpsUrl) {
            location.replace(httpsUrl);
          }
        };
      }
      return true;
    }

    function setMode(mode) {
      activeMode = mode;
      const isSideboard = mode === "sideboard";
      const isQuota = mode === "quota";
      document.body.classList.toggle("mode-display", !isSideboard && !isQuota);
      document.body.classList.toggle("mode-sideboard", isSideboard);
      document.body.classList.toggle("mode-quota", isQuota);
      displayView.classList.toggle("active", !isSideboard && !isQuota);
      sideboardView.classList.toggle("active", isSideboard);
      quotaView.classList.toggle("active", isQuota);
      displayMode.classList.toggle("active", !isSideboard && !isQuota);
      sideboardMode.classList.toggle("active", isSideboard);
      quotaMode.classList.toggle("active", isQuota);
      localStorage.setItem("phoneMonitorViewMode", mode);

      if (isSideboard) {
        scheduleDashboardRefresh("sideboard", true);
        if (document.body.classList.contains("viewer-fullscreen")) {
          exitLandscapeViewer();
        }
      } else if (isQuota) {
        scheduleDashboardRefresh("quota", true);
        if (document.body.classList.contains("viewer-fullscreen")) {
          exitLandscapeViewer();
        }
      } else if (isIos() && deviceTrusted && !deviceLocalRequest) {
        // iPhone display mode: keep stream connected; user taps image for fullscreen.
        connectVideo();
      }
    }

    function getInitialMode() {
      const requestedMode = new URLSearchParams(location.search).get("mode");
      if (["display", "sideboard", "quota"].includes(requestedMode)) {
        return isEinkClient() && requestedMode === "display" ? "sideboard" : requestedMode;
      }

      const stored = localStorage.getItem("phoneMonitorViewMode") || "";
      if (isEinkClient() && (stored === "display" || !stored)) return "sideboard";
      return stored || "display";
    }

    function shouldStartInViewer() {
      const value = new URLSearchParams(location.search).get("viewer");
      return value === "1" || value === "true";
    }

    function setSideSkin(skin) {
      const nextSkin = ["command", "dial", "focus"].includes(skin) ? skin : "command";
      sideboardShell.classList.remove("skin-command", "skin-dial", "skin-focus");
      sideboardShell.classList.add(`skin-${nextSkin}`);
      for (const button of document.querySelectorAll("[data-side-skin]")) {
        button.classList.toggle("active", button.dataset.sideSkin === nextSkin);
      }
      localStorage.setItem("phoneMonitorSideSkin", nextSkin);
    }

    function formatPercent(value) {
      return Number.isFinite(value) ? `${Math.round(value)}%` : "--";
    }

    function formatGb(value) {
      return Number.isFinite(value) ? `${value.toFixed(value >= 10 ? 0 : 1)}GB` : "--";
    }

    function formatFileSize(bytes) {
      if (!Number.isFinite(bytes) || bytes <= 0) return "";
      const units = ["B", "KB", "MB", "GB"];
      let value = bytes;
      let unitIndex = 0;
      while (value >= 1024 && unitIndex < units.length - 1) {
        value /= 1024;
        unitIndex += 1;
      }

      return `${value.toFixed(value >= 10 || unitIndex === 0 ? 0 : 1)}${units[unitIndex]}`;
    }

    function formatMbps(value) {
      return Number.isFinite(value) ? `${value.toFixed(value >= 10 ? 0 : 1)}` : "--";
    }

    function formatTemperature(value) {
      return Number.isFinite(value) ? `${Math.round(value)}°C` : "N/A";
    }

    function describeWeatherCode(code, fallback) {
      const value = Number(code);
      if (!Number.isFinite(value)) return fallback || "";
      if (value === 0) return "晴朗";
      if (value === 1) return "大致晴朗";
      if (value === 2) return "局部多雲";
      if (value === 3) return "多雲";
      if (value === 45 || value === 48) return "有霧";
      if ([51, 53, 55].includes(value)) return "毛毛雨";
      if ([56, 57].includes(value)) return "凍毛毛雨";
      if ([61, 63, 65].includes(value)) return "下雨";
      if ([66, 67].includes(value)) return "凍雨";
      if ([71, 73, 75, 77].includes(value)) return "下雪";
      if ([80, 81, 82].includes(value)) return "陣雨";
      if ([85, 86].includes(value)) return "陣雪";
      if (value === 95) return "雷雨";
      if (value === 96 || value === 99) return "雷雨冰雹";
      return fallback || "";
    }

    function formatWeatherLocation(location) {
      const text = String(location || "").trim();
      if (!text || /^weather$/i.test(text)) return "天氣";
      if (/^current location$/i.test(text)) return "目前位置";
      return text
        .replace(/\bTaiwan\b/i, "台灣")
        .replace(/\bDistrict\b/i, "區")
        .replace(/,\s*/g, "，");
    }

    function formatSeconds(value) {
      if (!Number.isFinite(value)) return "--";
      const hours = Math.floor(value / 3600);
      const minutes = Math.floor((value % 3600) / 60);
      if (hours >= 24) {
        return `${Math.floor(hours / 24)}d ${hours % 24}h`;
      }
      return `${hours}h ${minutes}m`;
    }

    function setBar(element, value) {
      const percent = Number.isFinite(value) ? Math.max(0, Math.min(100, value)) : 0;
      element.style.width = `${percent}%`;
    }

    function averagePercent(values) {
      const valid = values.filter(Number.isFinite);
      if (!valid.length) return Number.NaN;
      return valid.reduce((sum, value) => sum + value, 0) / valid.length;
    }

    function setText(element, value) {
      element.textContent = value || "--";
    }

    function canUseProtectedConnection() {
      return deviceTrusted || deviceLocalRequest;
    }

    function setTrustState(message, good) {
      trustState.textContent = message || "";
      trustState.classList.toggle("good", Boolean(good));
      trustState.classList.toggle("warn", !good);
    }

    function setPairingStep(element, state) {
      element.classList.toggle("done", state === "done");
      element.classList.toggle("active", state === "active");
    }

    function updatePairingGuide(state) {
      const normalized = state || "install";
      setPairingStep(pairingStepInstall, normalized === "install" ? "active" : "done");
      setPairingStep(pairingStepPair, normalized === "pair" ? "active" : normalized === "paired" ? "done" : "");
      setPairingStep(pairingStepOpen, normalized === "scan" ? "active" : normalized === "paired" ? "done" : "");
      if (normalized === "paired") {
        qrCaption.textContent = "已有配對手機。iPhone 請用 HTTPS 開啟主畫面圖示。";
      } else if (normalized === "scan") {
        qrCaption.textContent = "iPhone：相機掃碼 → Safari。須已信任本機 HTTPS 憑證。";
      } else if (normalized === "pair") {
        qrCaption.textContent = "iPhone 先裝憑證，再按「開始配對手機」。";
      } else {
        qrCaption.textContent = "iPhone：憑證 → HTTPS 配對。Android：可下 APK。";
      }
    }

    function showInstallQr() {
      pairingQrActive = false;
      if (androidApkAvailable) {
        qrCode.src = `${androidApkQrUrl}?t=${Date.now()}`;
        updatePairingGuide("install");
        return true;
      }

      qrCode.src = `/qr.svg?t=${Date.now()}`;
      updatePairingGuide("pair");
      return false;
    }

    function appendDeviceToken(params) {
      if (deviceToken) {
        params.set("deviceToken", deviceToken);
      }
      return params;
    }

    function readDeviceField(device, pascalName, camelName) {
      return device?.[pascalName] ?? device?.[camelName] ?? "";
    }

    function formatDeviceTime(value) {
      if (!value) return "--";
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) return "--";
      return date.toLocaleString([], {
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
      });
    }

    function renderTrustedDevices(status) {
      const isLocal = Boolean(status?.LocalRequest ?? status?.localRequest);
      trustedDevicesPanel.hidden = !isLocal;
      if (!isLocal) {
        clearTrustedDevices.disabled = true;
        trustedDeviceList.textContent = "";
        return;
      }

      const devices = status?.Devices || status?.devices || [];
      clearTrustedDevices.disabled = !devices.length;
      trustedDeviceList.textContent = "";
      if (!devices.length) {
        const empty = document.createElement("span");
        empty.className = "trusted-devices-empty";
        empty.textContent = "尚未配對手機。";
        trustedDeviceList.append(empty);
        return;
      }

      for (const device of devices) {
        const id = readDeviceField(device, "DeviceId", "deviceId");
        const name = readDeviceField(device, "Name", "name") || "Phone";
        const lastSeen = readDeviceField(device, "LastSeenAt", "lastSeenAt");
        const remote = readDeviceField(device, "LastRemoteAddress", "lastRemoteAddress") || "local";
        const row = document.createElement("div");
        row.className = "trusted-device-row";
        row.innerHTML = `
          <div class="trusted-device-main">
            <strong></strong>
            <span></span>
          </div>
          <button type="button" data-device-revoke="">移除</button>
        `;
        row.querySelector("strong").textContent = name;
        row.querySelector("span").textContent = `${formatDeviceTime(lastSeen)} · ${remote}`;
        const button = row.querySelector("button");
        button.dataset.deviceRevoke = id;
        trustedDeviceList.append(row);
      }
    }

    function pairingPlatform() {
      if (isEinkClient()) return "BOOX / 電子紙";
      if (isIos()) return "iPhone / iPad";
      if (/Android/i.test(navigator.userAgent || "")) return "Android";
      return "瀏覽器";
    }

    function pairingDeviceName() {
      if (isEinkClient()) return "BOOX Go Color 7";
      if (isIos()) return "iPhone";
      if (/Android/i.test(navigator.userAgent || "")) return "Android 裝置";
      return navigator.platform || "新裝置";
    }

    async function requestApprovalPairing() {
      if (deviceTrusted || deviceLocalRequest || approvalPollTimer) return;
      const storageKey = `phoneMonitorPendingApproval:${location.host}`;
      let pending = null;
      try { pending = JSON.parse(localStorage.getItem(storageKey) || "null"); } catch { }

      if (!pending?.requestId || !pending?.requestSecret) {
        const response = await fetch("/api/devices/pairing/request", {
          method: "POST", cache: "no-store", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ name: pairingDeviceName(), platform: pairingPlatform() })
        });
        const result = await response.json();
        if (!response.ok) throw new Error(result.error || result.message || "無法提出配對申請。");
        pending = {
          requestId: result.RequestId || result.requestId,
          requestSecret: result.RequestSecret || result.requestSecret,
          verificationCode: result.VerificationCode || result.verificationCode
        };
        localStorage.setItem(storageKey, JSON.stringify(pending));
      }

      setTrustState(`已找到 Host，請在 PC 按允許（驗證碼 ${pending.verificationCode || "------"}）。`, false);
      const poll = async () => {
        try {
          const response = await fetch("/api/devices/pairing/poll", {
            method: "POST", cache: "no-store", headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ requestId: pending.requestId, requestSecret: pending.requestSecret })
          });
          const result = await response.json();
          const status = result.Status || result.status;
          if (response.ok && status === "approved") {
            persistDeviceCredentials(result.DeviceToken || result.deviceToken, result.DeviceId || result.deviceId);
            localStorage.removeItem(storageKey);
            clearInterval(approvalPollTimer);
            approvalPollTimer = null;
            await loadDeviceTrustStatus();
            showPairSuccessBanner(`裝置：${result.DeviceName || result.deviceName || pairingDeviceName()}`);
          } else if (status === "denied" || status === "expired") {
            localStorage.removeItem(storageKey);
            clearInterval(approvalPollTimer);
            approvalPollTimer = null;
            setTrustState(status === "denied" ? "PC 已拒絕這次配對。" : "配對申請已逾時，重新整理可再試。", false);
          }
        } catch { }
      };
      approvalPollTimer = setInterval(poll, 1500);
      poll();
    }

    function renderPendingApprovals(result) {
      const requests = result?.Requests || result?.requests || [];
      pendingPairingPanel.hidden = !requests.length;
      pendingPairingList.replaceChildren();
      for (const request of requests) {
        const row = document.createElement("div");
        row.className = "pending-pairing-row";
        const requestId = request.RequestId || request.requestId;
        row.innerHTML = `<div><strong></strong><span></span><b></b></div><button data-pair-approve>允許</button><button data-pair-deny>拒絕</button>`;
        row.querySelector("strong").textContent = request.Name || request.name || "新裝置";
        row.querySelector("span").textContent = `${request.Platform || request.platform || ""} · ${request.RemoteAddress || request.remoteAddress || ""}`;
        row.querySelector("b").textContent = `驗證碼 ${request.VerificationCode || request.verificationCode || "------"}`;
        row.querySelector("[data-pair-approve]").dataset.requestId = requestId;
        row.querySelector("[data-pair-deny]").dataset.requestId = requestId;
        pendingPairingList.append(row);
      }
    }

    async function loadPendingApprovals() {
      if (!deviceLocalRequest) return;
      try {
        renderPendingApprovals(await fetchJsonOrThrow("/api/devices/pairing/pending", { method: "POST" }));
      } catch { }
    }

    async function actOnPendingApproval(requestId, approve) {
      await fetchJsonOrThrow(`/api/devices/pairing/${approve ? "approve" : "deny"}`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ requestId })
      });
      await loadPendingApprovals();
      await loadDeviceTrustStatus();
    }

    async function revokeTrustedDevice(deviceId, button) {
      if (!deviceId) return;
      if (!window.confirm("要移除這支已配對手機嗎？")) return;

      const originalText = button?.textContent || "";
      if (button) {
        button.disabled = true;
        button.textContent = "…";
      }

      try {
        const result = await fetchJsonOrThrow("/api/devices/revoke", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ deviceId })
        });
        setTrustState(result.Message || result.message || "已移除手機。", true);
        await loadDeviceTrustStatus();
      } catch (error) {
        setTrustState(error.message || "移除手機失敗。", false);
      } finally {
        if (button) {
          button.disabled = false;
          button.textContent = originalText;
        }
      }
    }

    async function clearAllTrustedDevices(button) {
      if (!window.confirm("要清空所有已配對手機嗎？目前手機會需要重新掃 QR 配對。")) return;

      const originalText = button?.textContent || "";
      if (button) {
        button.disabled = true;
        button.textContent = "清除中";
      }

      try {
        const result = await fetchJsonOrThrow("/api/devices/clear", {
          method: "POST",
          headers: { "Content-Type": "application/json" }
        });
        persistDeviceCredentials("", "");
        setTrustState(result.Message || result.message || "已清空配對手機。", true);
        await loadDeviceTrustStatus();
      } catch (error) {
        setTrustState(error.message || "清空配對手機失敗。", false);
      } finally {
        if (button) {
          button.disabled = false;
          button.textContent = originalText;
        }
      }
    }

    function parsePairingCredentials(raw) {
      const text = String(raw || "").trim();
      if (!text) return null;

      try {
        const asUrl = text.includes("://")
          ? new URL(text)
          : new URL(text, location.origin);
        const hash = asUrl.hash ? new URLSearchParams(asUrl.hash.slice(1)) : null;
        const query = asUrl.searchParams;
        const pairingId = hash?.get("pairingId") || query.get("pairingId");
        const pairingSecret = hash?.get("pairingSecret") || query.get("pairingSecret");
        if (pairingId && pairingSecret) {
          return { pairingId, pairingSecret };
        }
      } catch {
      }

      // Accept raw "id secret" or query-only fragments.
      const params = new URLSearchParams(text.replace(/^#/, "").replace(/^\?/, ""));
      const pairingId = params.get("pairingId");
      const pairingSecret = params.get("pairingSecret");
      if (pairingId && pairingSecret) {
        return { pairingId, pairingSecret };
      }

      return null;
    }

    function showPairSuccessBanner(message) {
      const banner = document.getElementById("pairSuccessBanner");
      if (!banner) return;
      const homeHint = isIos() && !isStandaloneApp()
        ? "<div>接著在<strong>這個已配對頁面</strong>按 Safari 底部分享 → <strong>加入主畫面</strong>。不要另開新分頁再加。</div>"
        : "<div>可點上方「顯示器 / 資訊板 / 額度」使用。</div>";
      banner.innerHTML = `<strong>已配對成功</strong><div>${message || ""}</div>${homeHint}`;
      banner.classList.add("show");
    }

    async function completePairingWithCredentials(pairingId, pairingSecret) {
      if (!pairingId || !pairingSecret) {
        throw new Error("配對資訊不完整。");
      }

      setTrustState("正在完成手機配對...", false);
      // Do not require action-token for complete — unpaired phones only need the one-time secret.
      const response = await fetch("/api/devices/pairing/complete", {
        method: "POST",
        cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ pairingId, pairingSecret })
      });
      const text = await response.text();
      let result = {};
      try {
        result = text ? JSON.parse(text) : {};
      } catch {
        result = {};
      }

      if (!response.ok) {
        throw new Error(result.Message || result.message || result.error || `配對失敗 HTTP ${response.status}`);
      }

      const nextToken = result.DeviceToken || result.deviceToken || "";
      const nextId = result.DeviceId || result.deviceId || "";
      if (!nextToken) {
        throw new Error("配對回應沒有 device token。");
      }

      persistDeviceCredentials(nextToken, nextId);
      return result;
    }

    async function completeDevicePairingFromHash() {
      const hash = location.hash ? new URLSearchParams(location.hash.slice(1)) : null;
      const query = new URLSearchParams(location.search);
      const pairingId = hash?.get("pairingId") || query.get("pairingId");
      const pairingSecret = hash?.get("pairingSecret") || query.get("pairingSecret");
      if (!pairingId || !pairingSecret) return false;

      const result = await completePairingWithCredentials(pairingId, pairingSecret);

      // Drop one-time pairing secrets from the address bar.
      const cleanUrl = new URL(location.href);
      cleanUrl.searchParams.delete("pairingId");
      cleanUrl.searchParams.delete("pairingSecret");
      cleanUrl.hash = "";
      history.replaceState(null, "", `${cleanUrl.pathname}${cleanUrl.search}`);

      await loadDeviceTrustStatus();
      applyClientChrome();
      if (isEinkClient()) {
        fullscreen.textContent = "全螢幕面板";
      }
      const name = result.DeviceName || result.deviceName || "這支手機";
      setTrustState(`信任狀態：${name} 已配對。`, true);
      showPairSuccessBanner(`裝置：${name}`);
      return true;
    }

    async function completePairingFromPastedLink() {
      const input = document.getElementById("pairLinkInput");
      const parsed = parsePairingCredentials(input?.value || "");
      if (!parsed) {
        setTrustState("連結格式不對。請貼上包含 pairingId 與 pairingSecret 的完整網址。", false);
        return;
      }

      try {
        const result = await completePairingWithCredentials(parsed.pairingId, parsed.pairingSecret);
        if (input) input.value = "";
        await loadDeviceTrustStatus();
        applyClientChrome();
        const name = result.DeviceName || result.deviceName || "這支手機";
        setTrustState(`信任狀態：${name} 已配對。`, true);
        showPairSuccessBanner(`裝置：${name}`);
      } catch (error) {
        setTrustState(error.message || "配對失敗。", false);
      }
    }

    async function loadDeviceTrustStatus() {
      try {
        const result = await fetchJsonOrThrow("/api/devices/status");
        deviceHeaderName = result.DeviceHeader || result.deviceHeader || deviceHeaderName;
        deviceTrusted = Boolean(result.Trusted ?? result.trusted);
        deviceLocalRequest = Boolean(result.LocalRequest ?? result.localRequest);
        const currentDevice = result.CurrentDevice || result.currentDevice;
        pairPhone.hidden = !deviceLocalRequest;
        pairPhone.textContent = "開始配對手機";
        launchDeckWindow.hidden = !deviceLocalRequest;
        nativeAppLink.hidden = deviceLocalRequest;
        renderTrustedDevices(result);

        if (deviceLocalRequest) {
          setTrustState("信任狀態：本機控制。", true);
          if (!pairingQrActive) {
            updatePairingGuide(androidApkAvailable ? "install" : "pair");
          }
          loadPendingApprovals();
          if (!pendingApprovalTimer) pendingApprovalTimer = setInterval(loadPendingApprovals, 2000);
        } else if (deviceTrusted) {
          const name = currentDevice?.Name || currentDevice?.name || "已配對手機";
          setTrustState(`信任狀態：${name} 已配對。`, true);
          updatePairingGuide("paired");
        } else {
          setTrustState("已找到 Host，正在提出配對申請…", false);
          requestApprovalPairing().catch(error => setTrustState(error.message || "自動配對申請失敗。", false));
        }

        applyClientChrome();
        return result;
      } catch (error) {
        deviceTrusted = false;
        deviceLocalRequest = false;
        pairPhone.hidden = true;
        launchDeckWindow.hidden = true;
        renderTrustedDevices(null);
        setTrustState(error.message || "信任狀態無法取得。", false);
        applyClientChrome();
        return null;
      }
    }

    let lastPairingPayload = null;

    function showPairingQr(mode) {
      if (!lastPairingPayload) return;
      const useNative = mode === "android-app";
      const svg = useNative
        ? (lastPairingPayload.nativeQrSvg || lastPairingPayload.qrSvg)
        : (lastPairingPayload.qrSvg || lastPairingPayload.nativeQrSvg);
      if (!svg) return;
      pairingQrActive = true;
      qrCode.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
      qrCode.alt = useNative ? "Android App 配對 QR" : "Safari / 瀏覽器配對 QR";
      updatePairingGuide("scan");
      qrCaption.textContent = useNative
        ? "Android App 配對 QR（選用 VibeDeck 開啟）。"
        : "iPhone HTTPS 配對 QR（相機 → Safari）。須已信任憑證。";
      setTrustState(
        useNative ? "QR：Android App" : "QR：iPhone / 網頁（HTTPS）",
        true
      );
    }

    async function startPhonePairing() {
      const result = await fetchJsonOrThrow("/api/devices/pairing/start", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: "" })
      });
      const qrSvg = result.QrSvg || result.qrSvg;
      const nativeQrSvg = result.NativeQrSvg || result.nativeQrSvg;
      const pairingUrl = result.PairingUrl || result.pairingUrl;
      const nativePairingUrl = result.NativePairingUrl || result.nativePairingUrl;
      const expiresAt = result.ExpiresAt || result.expiresAt;
      lastPairingPayload = { qrSvg, nativeQrSvg, pairingUrl, nativePairingUrl, expiresAt };

      // Default: web HTTP QR — works for iPhone Safari and Android Chrome.
      // Android native App can switch to deep-link QR below.
      showPairingQr("web");

      if (pairingUrl) {
        httpLink.href = pairingUrl;
        httpLink.textContent = pairingUrl;
        httpLink.hidden = false;
        prettyLink.href = pairingUrl;
        prettyLink.textContent = "網頁配對連結（iPhone / Chrome）";
        prettyLink.hidden = false;
      }
      if (pairingUrl) {
        prettyLink.href = pairingUrl;
        prettyLink.textContent = "網頁配對連結";
        prettyLink.hidden = false;
        prettyLink.onclick = event => {
          event.preventDefault();
          showPairingQr("web");
          // Also copy helper: leave href for long-press open.
        };
      }
      if (nativePairingUrl) {
        if (copyNativePairingLink) {
          copyNativePairingLink.hidden = false;
          copyNativePairingLink.onclick = async () => {
            try {
              await navigator.clipboard.writeText(nativePairingUrl);
              setTrustState("Android 配對連結已複製。可用 adb shell am start 開啟。", true);
            } catch {
              window.prompt("請複製這個 Android 配對連結", nativePairingUrl);
            }
          };
        }
        nativeQrLink.href = "#android-app-pairing-qr";
        nativeQrLink.textContent = "顯示 Android App 配對 QR";
        nativeQrLink.hidden = false;
        nativeQrLink.onclick = event => {
          event.preventDefault();
          showPairingQr("android-app");
        };
      }
      const expiryText = expiresAt
        ? new Date(expiresAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
        : "";
      if (expiryText) {
        setTrustState(`配對 QR 已建立（到 ${expiryText}）。iPhone 須已裝 HTTPS 憑證。`, true);
      }
    }

    async function launchDeckOnVirtualDisplay() {
      const mode = activeMode === "quota" ? "quota" : "sideboard";
      const originalText = launchDeckWindow.textContent;
      launchDeckWindow.disabled = true;
      launchDeckWindow.textContent = "開啟中";

      try {
        const result = await fetchJsonOrThrow("/api/deck/launch", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ mode })
        });
        setAppState(result.Message || result.message || "Deck 視窗已開啟。", true);
      } catch (error) {
        setAppState(error.message || "Deck 視窗開啟失敗。", false);
      } finally {
        launchDeckWindow.disabled = false;
        launchDeckWindow.textContent = originalText;
      }
    }

    async function ensureActionToken() {
      if (actionToken) return actionToken;
      const response = await fetch("/api/session", { cache: "no-store" });
      const data = await response.json();
      actionToken = data.ActionToken || data.actionToken || "";
      actionHeaderName = data.ActionHeader || data.actionHeader || actionHeaderName;
      deviceHeaderName = data.DeviceHeader || data.deviceHeader || deviceHeaderName;
      return actionToken;
    }

    async function fetchJsonOrThrow(url, init = {}, retryOnTokenRefresh = true) {
      const method = String(init.method || "GET").toUpperCase();
      const headers = new Headers(init.headers || {});
      if (deviceToken) {
        headers.set(deviceHeaderName, deviceToken);
      }
      if (method !== "GET" && method !== "HEAD") {
        const token = await ensureActionToken();
        headers.set(actionHeaderName, token);
      }

      const response = await fetch(url, { cache: "no-store", ...init, headers });
      const text = await response.text();
      const data = text ? JSON.parse(text) : {};
      if (response.status === 403 && retryOnTokenRefresh && method !== "GET" && method !== "HEAD") {
        actionToken = "";
        return fetchJsonOrThrow(url, init, false);
      }

      if (!response.ok) {
        const error = new Error(data.error || data.Message || data.message || `HTTP ${response.status}`);
        error.status = response.status;
        throw error;
      }
      return data;
    }

    function isTrustRequiredError(error) {
      return error?.status === 403 &&
        /not paired|trust|token/i.test(error.message || "");
    }

    async function refreshSideboard() {
      if (activeMode !== "sideboard") return;

      try {
        const [stats, workPulse] = await Promise.all([
          fetchJsonOrThrow("/api/sideboard/stats"),
          fetchJsonOrThrow("/api/sideboard/work-pulse").catch(() => null)
        ]);

        sideError.textContent = "";
        renderSideboardStats(stats);
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

    function renderSideboardStats(stats) {
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
        disk.usagePercent
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
        weatherTemp
      ].filter(Boolean);
      setText(sideWeather, weatherParts.length > 1 ? weatherParts.join(" · ") : "天氣資料暫不可用");
      setText(sideWeatherSub, `體感 ${weatherFeels}`);
      setText(sideDiskIo, `磁碟 IO 讀 ${formatMbps(disk.readMBps)} / 寫 ${formatMbps(disk.writeMBps)} MB/s`);
      renderProcesses(stats.processes || []);
    }

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

    async function refreshQuotas(options = {}) {
      if (activeMode !== "quota") return;

      try {
        const endpoint = options.force ? "/api/quotas/refresh" : "/api/quotas";
        const init = options.force ? { method: "POST" } : undefined;
        renderQuotas(await fetchJsonOrThrow(endpoint, init));
      } catch (error) {
        quotaSummary.textContent = isTrustRequiredError(error)
          ? "請先配對手機，才能查看 AI 額度。"
          : error.message || "額度來源無法使用。";
        quotaUpdated.textContent = "--";
        if (quotaHelp) {
          quotaHelp.replaceChildren();
          quotaHelp.append(renderQuotaHelpBlock(
            "無法讀取額度",
            isTrustRequiredError(error)
              ? ["手機需先與 PC 完成配對，才能查看本機 AI 額度。"]
              : [error.message || "額度 API 失敗。", "請確認 Host 在跑，並在 PC 本機操作額度來源。"]
          ));
        }
        quotaGrid.replaceChildren();
      }
    }

    function renderQuotas(snapshot) {
      quotaSnapshotData = snapshot || {};
      renderQuotaContent();
    }

    function renderQuotaContent() {
      quotaGrid.replaceChildren();
      if (quotaHelp) quotaHelp.replaceChildren();
      const snapshot = quotaSnapshotData || {};
      const providers = snapshot?.Providers || snapshot?.providers || [];
      const tabs = buildQuotaTabs();
      if (!tabs.some(tab => tab.id === quotaActiveTab)) {
        quotaActiveTab = tabs[0]?.id || "agy";
      }
      renderQuotaTabs(tabs);

      const activeTab = tabs.find(tab => tab.id === quotaActiveTab);
      const hasUsable = providers.some(provider => {
        const family = getProviderFamily(provider);
        if (family !== quotaActiveTab) return false;
        const state = String(provider.State || provider.state || "").toLowerCase();
        return state === "ok" || family === "agy";
      });
      const agyAccounts = groupAgyAccounts(providers);
      const codexProviders = groupSingleProviderAccounts(providers, "codex");
      const codexUsable = codexProviders.some(provider => {
        const state = String(provider.State || provider.state || "").toLowerCase();
        return state === "ok";
      });

      if (quotaActiveTab === "agy") {
        quotaSummary.textContent = agyAccounts.length
          ? `AGY · ${agyAccounts.length} 個帳號`
          : "AGY · 尚未導入帳號";
      } else if (quotaActiveTab === "codex") {
        quotaSummary.textContent = codexUsable
          ? "Codex · 已讀到額度"
          : "Codex · 等待本機 session";
      } else {
        quotaSummary.textContent = hasUsable
          ? `${activeTab?.label || "AI"} 額度狀態`
          : "目前沒有可用的額度來源。";
      }
      quotaUpdated.textContent = snapshot?.GeneratedAt || snapshot?.generatedAt
        ? new Date(snapshot.GeneratedAt || snapshot.generatedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
        : "--";

      if (quotaHelp) {
        quotaHelp.append(renderQuotaHelpForTab(quotaActiveTab, {
          agyCount: agyAccounts.length,
          codexUsable,
          codexCount: codexProviders.length
        }));
      }

      if (quotaActiveTab === "agy") {
        if (agyAccounts.length) {
          const index = getQuotaAccountIndex("agy", agyAccounts.length);
          quotaGrid.append(renderAgyAccountPage(agyAccounts[index], agyAccounts, index, snapshot));
        } else {
          quotaGrid.append(renderAgySetupCard());
        }
      } else if (quotaActiveTab === "codex") {
        if (codexProviders.length) {
          const index = getQuotaAccountIndex("codex", codexProviders.length);
          const provider = codexProviders[index];
          if (provider) {
            quotaGrid.append(renderSingleQuotaPage(provider, snapshot, { index, total: codexProviders.length, tabId: "codex" }));
          }
        } else {
          quotaGrid.append(renderCodexSetupCard());
        }
      }

      if (!quotaGrid.children.length) {
        quotaGrid.append(renderQuotaEmptyCard("等待額度來源", ["切換上方分頁查看 AGY / Codex 的導入說明。"]));
      }
    }

    function buildQuotaTabs() {
      return [
        { id: "agy", label: "AGY" },
        { id: "codex", label: "Codex" }
      ];
    }

    function isEinkQuotaClient() {
      return document.body.classList.contains("eink-client");
    }

    function renderQuotaHelpBlock(title, steps) {
      const block = document.createElement("details");
      block.className = "quota-help-block";
      if (isEinkQuotaClient()) block.classList.add("quota-help-block-eink");
      // Collapsed by default — pipeline notes are dense and steal vertical space.
      block.open = false;
      const summary = document.createElement("summary");
      summary.className = "quota-help-summary";
      const label = document.createElement("span");
      label.className = "quota-help-summary-label";
      label.textContent = title;
      const hint = document.createElement("span");
      hint.className = "quota-help-summary-hint";
      hint.textContent = "pipeline";
      summary.append(label, hint);
      block.append(summary);
      const body = document.createElement("div");
      body.className = "quota-help-body";
      const list = document.createElement("ol");
      for (const step of steps) {
        const li = document.createElement("li");
        li.textContent = step;
        list.append(li);
      }
      body.append(list);
      block.append(body);
      return block;
    }

    function renderQuotaHelpForTab(tabId, state = {}) {
      const eink = isEinkQuotaClient();

      if (tabId === "codex") {
        if (state.codexUsable) {
          return renderQuotaHelpBlock("Codex pipeline", eink
            ? [
                "Source: local session JSONL → latest rate_limits payload.",
                "Refresh: new Codex activity on Host PC, then ↻ / POST /api/quotas/refresh."
              ]
            : [
                "Ingest path: Host tails %USERPROFILE%\\.codex\\sessions\\**\\*.jsonl for the newest event_msg.rate_limits snapshot.",
                "Identity only: auth*.json supplies account metadata (email / plan); Codex tokens are not stored by VibeDeck.",
                "Cache: normalized rows under %LOCALAPPDATA%\\PhoneMonitor\\quotas\\codex\\accounts\\.",
                "Re-sample: generate a newer rate_limits event on this PC, then ↻ (POST /api/quotas/refresh)."
              ]);
        }
        return renderQuotaHelpBlock(eink ? "Codex bind" : "Codex bind requirements", eink
          ? [
              "Co-locate Codex CLI + Host on the same Windows machine.",
              "Require at least one session JSONL containing rate_limits.",
              "Trigger rescan via ↻. No OAuth / token-import surface."
            ]
          : [
              "Runtime co-location: Codex CLI must run on the same Windows host process tree as PhoneMonitor.Host.",
              "Filesystem probe: %USERPROFILE%\\.codex\\sessions\\**\\*.jsonl — Host scans newest files for rate_limits.",
              "No remote bind: there is no Codex OAuth, paste-token, or cloud account import API.",
              "State source-needed: auth identity exists but no compatible rate_limits snapshot has been observed yet.",
              "Rescan: ↻ maps to POST /api/quotas/refresh."
            ]);
      }

      if (state.agyCount > 0) {
        return renderQuotaHelpBlock("AGY pipeline", eink
          ? [
              "Store: DPAPI-protected refresh tokens under LocalAppData\\PhoneMonitor\\quotas\\agy.",
              "Actions: + OAuth · ↻ token refresh + quota API · ⌫ drop local account store."
            ]
          : [
              "Credential store: %LOCALAPPDATA%\\PhoneMonitor\\quotas\\agy\\accounts\\ (refresh_token_protected, DPAPI CurrentUser).",
              "Quota fetch: Host exchanges refresh → access token, then calls Antigravity quota endpoints (not Claude Code local config).",
              "Controls: + → POST /api/quotas/agy/oauth/start · ↻ → /api/quotas/refresh · ⌫ → /api/quotas/agy/account/delete."
            ]);
      }

      return renderQuotaHelpBlock(eink ? "AGY bind" : "AGY bind requirements", eink
        ? [
            "Prerequisite: Google OAuth client on Host (env or secrets JSON).",
            "+ → loopback OAuth on PC browser (PKCE).",
            "↻ → refresh tokens + retrieveUserQuotaSummary."
          ]
        : [
              "Prerequisite: Google OAuth client on the Host — AGY_GOOGLE_CLIENT_ID / AGY_GOOGLE_CLIENT_SECRET, or %LOCALAPPDATA%\\PhoneMonitor\\secrets\\agy-google-oauth.json.",
              "Bind: + issues POST /api/quotas/agy/oauth/start (PKCE, loopback redirect to Host); complete consent in the PC browser.",
              "Persist: refresh tokens land in %LOCALAPPDATA%\\PhoneMonitor\\quotas\\agy\\accounts\\; quota cache under ...\\quotas\\agy\\cache\\.",
              "Hydrate: ↻ refreshes access tokens and pulls Antigravity remaining-quota windows (Claude / Gemini buckets).",
              "Missing client credentials: oauth/start fails closed — expected, not a device-pairing fault."
            ]);
    }

    function renderQuotaEmptyCard(title, lines) {
      const card = document.createElement("article");
      card.className = "quota-account-card offline quota-setup-card";
      const head = document.createElement("div");
      head.className = "quota-setup-title";
      head.textContent = title;
      card.append(head);
      const list = document.createElement("ul");
      list.className = "quota-setup-list";
      for (const line of lines) {
        const li = document.createElement("li");
        li.textContent = line;
        list.append(li);
      }
      card.append(list);
      return card;
    }

    function renderAgySetupCard() {
      const card = document.createElement("article");
      card.className = "quota-account-card quota-setup-card";
      card.dataset.statusKey = "agy:setup";
      const eink = isEinkQuotaClient();
      card.innerHTML = eink
        ? `
        <div class="quota-setup-title">AGY · unbound</div>
        <ul class="quota-setup-list">
          <li>Host OAuth client required (env / secrets JSON).</li>
          <li><b>+</b> → PKCE loopback on PC browser.</li>
          <li><b>↻</b> → token refresh + quota API pull.</li>
        </ul>
        <div class="quota-footer">
          <span class="quota-time">oauth/start</span>
          <span></span>
          <div class="quota-toolbox" aria-label="額度操作">
            <button type="button" title="登入 AGY" data-quota-action="agy-oauth">+</button>
            <button type="button" title="更新額度" data-quota-action="refresh">↻</button>
          </div>
        </div>
        <div class="quota-action-status" aria-live="polite"></div>
      `
        : `
        <div class="quota-setup-title">AGY · no bound account</div>
        <ul class="quota-setup-list">
          <li>No passive filesystem discovery for AGY — bind via OAuth only.</li>
          <li>Client credentials must exist on Host before <b>+</b> (see pipeline notes above).</li>
          <li>Successful consent writes DPAPI-protected refresh tokens under LocalAppData\\PhoneMonitor\\quotas\\agy.</li>
        </ul>
        <div class="quota-footer">
          <span class="quota-time">POST /api/quotas/agy/oauth/start</span>
          <span></span>
          <div class="quota-toolbox" aria-label="額度操作">
            <button type="button" title="登入 AGY" data-quota-action="agy-oauth">+</button>
            <button type="button" title="更新額度" data-quota-action="refresh">↻</button>
          </div>
        </div>
        <div class="quota-action-status" aria-live="polite"></div>
      `;
      applyQuotaCardStatus(card);
      wireQuotaToolbox(card);
      return card;
    }

    function renderCodexSetupCard() {
      const card = document.createElement("article");
      card.className = "quota-account-card quota-setup-card";
      card.dataset.statusKey = "codex:setup";
      const eink = isEinkQuotaClient();
      card.innerHTML = eink
        ? `
        <div class="quota-setup-title">Codex · no snapshot</div>
        <ul class="quota-setup-list">
          <li>Await local session JSONL with rate_limits.</li>
          <li>Rescan only — no OAuth / import API.</li>
          <li>Co-locate Codex CLI with Host on Windows.</li>
        </ul>
        <div class="quota-footer">
          <span class="quota-time">session scan</span>
          <span></span>
          <div class="quota-toolbox" aria-label="額度操作">
            <button type="button" title="更新額度" data-quota-action="refresh">↻</button>
          </div>
        </div>
        <div class="quota-action-status" aria-live="polite"></div>
      `
        : `
        <div class="quota-setup-title">Codex · no rate_limits snapshot</div>
        <ul class="quota-setup-list">
          <li>Passive scrape only: Host does not expose Codex OAuth or token import.</li>
          <li>Probe path: %USERPROFILE%\\.codex\\sessions\\**\\*.jsonl (event_msg.rate_limits).</li>
          <li>Host and Codex must share the same Windows machine / user profile.</li>
        </ul>
        <div class="quota-footer">
          <span class="quota-time">POST /api/quotas/refresh</span>
          <span></span>
          <div class="quota-toolbox" aria-label="額度操作">
            <button type="button" title="更新額度" data-quota-action="refresh">↻</button>
          </div>
        </div>
        <div class="quota-action-status" aria-live="polite"></div>
      `;
      applyQuotaCardStatus(card);
      wireQuotaToolbox(card);
      return card;
    }

    function renderQuotaTabs(tabs) {
      quotaTabs.replaceChildren();
      for (const tab of tabs) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = `quota-tab${tab.id === quotaActiveTab ? " active" : ""}`;
        button.setAttribute("role", "tab");
        button.setAttribute("aria-selected", tab.id === quotaActiveTab ? "true" : "false");
        button.textContent = tab.label;
        button.addEventListener("click", () => {
          quotaActiveTab = tab.id;
          localStorage.setItem("phoneMonitorQuotaTab", quotaActiveTab);
          renderQuotaContent();
        });
        quotaTabs.append(button);
      }
    }

    function groupAgyAccounts(providers) {
      const accounts = new Map();
      for (const provider of providers.filter(item => getProviderFamily(item) === "agy")) {
        const accountId = provider.AccountId || provider.accountId || provider.Source || provider.source || "agy";
        if (!accounts.has(accountId)) {
          accounts.set(accountId, {
            id: accountId,
            email: provider.AccountEmail || provider.accountEmail || extractQuotaEmail([provider]) || "Antigravity Account",
            tier: provider.AccountTier || provider.accountTier || extractQuotaTier([provider]) || "PRO",
            providers: []
          });
        }
        const account = accounts.get(accountId);
        account.email = account.email || provider.AccountEmail || provider.accountEmail;
        account.tier = account.tier || provider.AccountTier || provider.accountTier;
        account.providers.push(provider);
      }

      return Array.from(accounts.values()).sort((left, right) => String(left.email).localeCompare(String(right.email)));
    }

    function renderAgyAccountPage(account, accounts, index, snapshot) {
      const page = document.createElement("section");
      page.className = "quota-account-page";
      page.append(renderAgyAccountCard(account, snapshot, { index, total: accounts.length, tabId: "agy" }));
      return page;
    }

    function renderQuotaSwitcher(index, total, tabId) {
      if (total < 2) return `<span class="quota-account-switcher"></span>`;
      const dots = Array.from({ length: total }, (_, itemIndex) =>
        `<span class="quota-dot${itemIndex === index ? " active" : ""}"></span>`).join("");
      return `
        <div class="quota-account-switcher" data-tab-id="${tabId}">
        <button class="quota-page-button" type="button" aria-label="上一個帳號">‹</button>
          <span>${index + 1} / ${total}</span>
          <span class="quota-dots">${dots}</span>
          <button class="quota-page-button" type="button" aria-label="下一個帳號">›</button>
        </div>
      `;
    }

    function wireQuotaSwitcher(card) {
      const switcher = card.querySelector(".quota-account-switcher");
      if (!switcher) return;
      const buttons = switcher.querySelectorAll("button");
      if (buttons.length < 2) return;
      buttons[0].addEventListener("click", () => changeQuotaAccount(switcher.dataset.tabId, -1));
      buttons[1].addEventListener("click", () => changeQuotaAccount(switcher.dataset.tabId, 1));
    }

    function wireQuotaToolbox(card) {
      const refreshButton = card.querySelector('[data-quota-action="refresh"]');
      if (refreshButton) refreshButton.addEventListener("click", async () => {
        await runQuotaButton(refreshButton, () => refreshQuotas({ force: true }), {
          pending: "正在更新額度...",
          success: "額度已更新。"
        });
      });

      const cliButton = card.querySelector('[data-quota-action="agy-cli"]');
      if (cliButton) cliButton.addEventListener("click", async () => {
        await runQuotaButton(cliButton, async () => {
          return await postQuotaAccountAction("/api/quotas/agy/cli/open", card);
        }, {
          pending: "正在開啟 AGY CLI...",
          success: "AGY CLI 已開啟。"
        });
      });

      const oauthButton = card.querySelector('[data-quota-action="agy-oauth"]');
      if (oauthButton) oauthButton.addEventListener("click", async () => {
        await runQuotaButton(oauthButton, async () => {
          const result = await fetchJsonOrThrow("/api/quotas/agy/oauth/start", { method: "POST" });
          const authUrl = result.AuthUrl || result.authUrl;
          const opened = result.Opened ?? result.opened;
          if (!opened && authUrl && isLocalHost()) {
            window.open(authUrl, "_blank", "noopener");
          }
          startQuotaOAuthPolling();
          return {
            message: opened
              ? "AGY 登入頁已在 PC 開啟。"
              : "AGY 登入已準備好。"
          };
        }, {
          pending: "正在啟動 AGY 登入..."
        });
      });

      const deleteButton = card.querySelector('[data-quota-action="agy-delete"]');
      if (deleteButton) deleteButton.addEventListener("click", async () => {
        const email = card.dataset.accountEmail || "this AGY account";
        if (!window.confirm(`要從 VibeDeck 移除 ${email} 嗎？`)) return;
        await runQuotaButton(deleteButton, async () => {
          const result = await postQuotaAccountAction("/api/quotas/agy/account/delete", card);
          await refreshQuotas({ force: true });
          return result;
        }, {
          pending: "正在刪除 AGY 帳號...",
          success: "AGY 帳號已刪除。"
        });
      });

      const codexDeleteButton = card.querySelector('[data-quota-action="codex-delete"]');
      if (codexDeleteButton) codexDeleteButton.addEventListener("click", async () => {
        const label = card.dataset.accountEmail || "this Codex profile";
        if (!window.confirm(`要從 VibeDeck 移除 ${label} 的額度快取嗎？`)) return;
        await runQuotaButton(codexDeleteButton, async () => {
          const result = await postQuotaAccountAction("/api/quotas/codex/account/delete", card);
          await refreshQuotas({ force: true });
          return result;
        }, {
          pending: "正在刪除 Codex Profile...",
          success: "Codex Profile 已刪除。"
        });
      });
    }

    function postQuotaAccountAction(url, card) {
      return fetchJsonOrThrow(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          accountId: card.dataset.accountId || "",
          email: card.dataset.accountEmail || ""
        })
      });
    }

    function isLocalHost() {
      return location.hostname === "127.0.0.1" ||
        location.hostname === "localhost" ||
        location.hostname === "::1";
    }

    function startQuotaOAuthPolling() {
      if (quotaOAuthPollTimer) {
        clearInterval(quotaOAuthPollTimer);
      }

      let attempts = 0;
      quotaOAuthPollTimer = setInterval(async () => {
        attempts += 1;
        if (activeMode !== "quota" || attempts > 40) {
          clearInterval(quotaOAuthPollTimer);
          quotaOAuthPollTimer = null;
          return;
        }

        await refreshQuotas({ force: true });
      }, 3000);
    }

    function getQuotaActionMessage(result, fallback) {
      return result?.Message || result?.message || fallback;
    }

    function applyQuotaCardStatus(card) {
      if (!card) return;
      const status = card.querySelector(".quota-action-status");
      if (!status) return;
      const state = card.dataset.statusKey ? quotaActionStatusByKey.get(card.dataset.statusKey) : null;
      status.textContent = state?.message || "";
      status.className = `quota-action-status${state?.level ? ` ${state.level}` : ""}`;
    }

    function setQuotaCardStatus(card, message, level = "") {
      if (!card) return;
      const statusKey = card.dataset.statusKey || "";
      const normalized = {
        message: message || "",
        level: level || ""
      };

      if (statusKey) {
        if (normalized.message) {
          quotaActionStatusByKey.set(statusKey, normalized);
        } else {
          quotaActionStatusByKey.delete(statusKey);
        }

        document.querySelectorAll(".quota-account-card").forEach(item => {
          if (item.dataset.statusKey === statusKey) applyQuotaCardStatus(item);
        });
      } else {
        const status = card.querySelector(".quota-action-status");
        if (status) {
          status.textContent = normalized.message;
          status.className = `quota-action-status${normalized.level ? ` ${normalized.level}` : ""}`;
        }
      }
    }

    function setQuotaCardActionsDisabled(card, disabled) {
      if (!card) return;
      card.querySelectorAll(".quota-toolbox button").forEach(item => {
        if (disabled) {
          item.dataset.actionWasDisabled = item.disabled ? "1" : "0";
          item.disabled = true;
        } else if (item.dataset.actionWasDisabled !== undefined) {
          item.disabled = item.dataset.actionWasDisabled === "1";
          delete item.dataset.actionWasDisabled;
        }
      });
    }

    async function runQuotaButton(button, action, options = {}) {
      if (!button) return null;
      const card = button.closest(".quota-account-card");
      const originalText = button.textContent;
      setQuotaCardStatus(card, options.pending || "處理中...", "working");
      setQuotaCardActionsDisabled(card, true);
      button.textContent = "…";
      try {
        const result = await action();
        setQuotaCardStatus(card, getQuotaActionMessage(result, options.success || "完成。"), "success");
        return result;
      } catch (error) {
        setQuotaCardStatus(card, error.message || options.error || "額度操作失敗。", "error");
        return null;
      } finally {
        button.textContent = originalText;
        setQuotaCardActionsDisabled(card, false);
      }
    }

    function renderAgyAccountCard(account, snapshot, pageInfo) {
      const providers = account.providers || [];
      const card = document.createElement("article");
      card.className = "quota-account-card";
      const email = account.email || extractQuotaEmail(providers) || "Antigravity Account";
      const tier = account.tier || extractQuotaTier(providers) || "PRO";
      card.dataset.accountId = account.id || "";
      card.dataset.accountEmail = email;
      card.dataset.statusKey = `agy:${account.id || email}`;
      const updated = snapshot?.GeneratedAt || snapshot?.generatedAt;
      const updatedText = updated
        ? new Date(updated).toLocaleString([], { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" })
        : "--";
      const claude = providers.find(provider => providerContains(provider, "claude")) || {};
      const gemini = providers.find(provider => providerContains(provider, "gemini")) || {};

      card.innerHTML = `
        <div class="quota-account-head">
          <span class="quota-check"></span>
          <span class="quota-account-email"></span>
          ${renderQuotaSwitcher(pageInfo?.index || 0, pageInfo?.total || 1, pageInfo?.tabId || "agy")}
          <span class="quota-pill"></span>
        </div>
        <div class="quota-pair">
          ${renderQuotaProvider("Claude", claude)}
          ${renderQuotaProvider("Gemini", gemini)}
        </div>
        <div class="quota-credit-row">可用 AI 點數：<span>--</span></div>
        <div class="quota-footer">
          <span class="quota-time"></span>
          <span></span>
          <div class="quota-toolbox" aria-label="額度操作">
            <button type="button" title="用這個帳號開啟 AGY CLI" data-quota-action="agy-cli">▶</button>
            <button type="button" title="登入 AGY" data-quota-action="agy-oauth">+</button>
            <button type="button" title="更新額度" data-quota-action="refresh">↻</button>
            <button type="button" title="刪除 AGY 帳號" data-quota-action="agy-delete">⌫</button>
          </div>
        </div>
        <div class="quota-action-status" aria-live="polite"></div>
      `;
      card.querySelector(".quota-account-email").textContent = email;
      card.querySelector(".quota-pill").textContent = normalizeTierLabel(tier);
      card.querySelector(".quota-time").textContent = updatedText;
      applyQuotaCardStatus(card);
      wireQuotaSwitcher(card);
      wireQuotaToolbox(card);
      return card;
    }

    function renderSingleQuotaPage(provider, snapshot, pageInfo) {
      const card = document.createElement("article");
      card.className = "quota-account-card";
      const label = provider.AccountEmail || provider.accountEmail || provider.Label || provider.label || provider.Id || provider.id || "AI";
      const state = provider.State || provider.state || "unknown";
      const family = getProviderFamily(provider);
      const accountId = provider.AccountId || provider.accountId || provider.Id || provider.id || "";
      const isActive = Boolean(provider.IsActive ?? provider.isActive);
      card.dataset.statusKey = `${getProviderFamily(provider)}:${label}`;
      card.dataset.accountId = accountId;
      card.dataset.accountEmail = provider.AccountEmail || provider.accountEmail || "";
      const updated = provider.ObservedAt || provider.observedAt || snapshot?.GeneratedAt || snapshot?.generatedAt;
      const updatedText = updated
        ? new Date(updated).toLocaleString([], { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" })
        : "--";
      card.innerHTML = `
        <div class="quota-account-head">
          <span class="quota-check"></span>
          <span class="quota-account-email"></span>
          ${isActive ? '<span class="quota-active-badge">目前使用中</span>' : ''}
          ${renderQuotaSwitcher(pageInfo?.index || 0, pageInfo?.total || 1, pageInfo?.tabId || getProviderFamily(provider))}
          <span class="quota-pill"></span>
        </div>
        <div class="quota-pair single">
          ${renderQuotaProvider(provider.Label || provider.label || label, provider)}
        </div>
        <div class="quota-footer">
          <span class="quota-time"></span>
          <span></span>
          <div class="quota-toolbox" aria-label="額度操作">
            <button type="button" title="更新額度" data-quota-action="refresh">↻</button>
            ${family === "codex" ? '<button type="button" title="刪除這個 Codex Profile 額度快取" data-quota-action="codex-delete">⌫</button>' : ''}
          </div>
        </div>
        <div class="quota-action-status" aria-live="polite"></div>
      `;
      card.querySelector(".quota-account-email").textContent = label;
      card.querySelector(".quota-pill").textContent = state;
      card.querySelector(".quota-time").textContent = updatedText;
      applyQuotaCardStatus(card);
      wireQuotaSwitcher(card);
      wireQuotaToolbox(card);
      return card;
    }

    function groupSingleProviderAccounts(providers, tabId) {
      return providers
        .filter(item => getProviderFamily(item) === tabId)
        .sort((left, right) => {
          const leftActive = Boolean(left.IsActive ?? left.isActive);
          const rightActive = Boolean(right.IsActive ?? right.isActive);
          if (leftActive !== rightActive) return leftActive ? -1 : 1;
          const leftLabel = left.AccountEmail || left.accountEmail || left.Label || left.label || left.Id || left.id || "";
          const rightLabel = right.AccountEmail || right.accountEmail || right.Label || right.label || right.Id || right.id || "";
          return String(leftLabel).localeCompare(String(rightLabel));
        });
    }

    function getProviderFamily(provider) {
      const family = provider.Family || provider.family || "";
      if (family) return family;

      const id = String(provider.Id || provider.id || "").toLowerCase();
      if (id.startsWith("agy")) return "agy";
      if (id.startsWith("codex")) return "codex";
      if (id.startsWith("claude")) return "claude-code";
      return id || "unknown";
    }

    function providerContains(provider, text) {
      const value = [
        provider.Id || provider.id,
        provider.Label || provider.label
      ].join(" ").toLowerCase();
      return value.includes(text);
    }

    function getQuotaAccountIndex(tabId, total) {
      const current = quotaAccountIndexByTab[tabId] || 0;
      const normalized = total > 0 ? ((current % total) + total) % total : 0;
      quotaAccountIndexByTab[tabId] = normalized;
      return normalized;
    }

    function changeQuotaAccount(tabId, delta) {
      const providers = quotaSnapshotData?.Providers || quotaSnapshotData?.providers || [];
      const total = tabId === "agy"
        ? groupAgyAccounts(providers).length
        : groupSingleProviderAccounts(providers, tabId).length;
      if (total < 2) return;
      quotaAccountIndexByTab[tabId] = getQuotaAccountIndex(tabId, total) + delta;
      renderQuotaContent();
    }

    function renderQuotaProvider(title, provider) {
      const primary = provider.Primary || provider.primary || {};
      const secondary = provider.Secondary || provider.secondary || {};
      return `
        <section class="quota-provider">
          <strong class="quota-provider-title">${title}</strong>
          ${renderQuotaWindow("5h", primary)}
          ${renderQuotaWindow("週額度", secondary)}
        </section>
      `;
    }

    function renderLocalQuotaCard(provider) {
      const card = document.createElement("article");
      card.className = "quota-local-card";
      const primary = provider.Primary || provider.primary || {};
      const secondary = provider.Secondary || provider.secondary || {};
      const label = provider.Label || provider.label || provider.Id || provider.id || "AI";
      const state = provider.State || provider.state || "unknown";
      const primaryText = summarizeQuotaWindow(primary);
      const secondaryText = summarizeQuotaWindow(secondary);
      card.innerHTML = `
        <strong></strong><b></b>
        <span></span><span></span>
      `;
      card.querySelector("strong").textContent = label;
      card.querySelector("b").textContent = state;
      const spans = card.querySelectorAll("span");
      spans[0].textContent = `5h ${primaryText}`;
      spans[1].textContent = `週額度 ${secondaryText}`;
      return card;
    }

    function renderQuotaWindow(label, windowData) {
      const used = windowData.UsedPercent ?? windowData.usedPercent;
      const providedRemaining = windowData.RemainingPercent ?? windowData.remainingPercent;
      const remaining = Number.isFinite(providedRemaining)
        ? Math.max(0, Math.min(100, providedRemaining))
        : Number.isFinite(used)
          ? Math.max(0, Math.min(100, 100 - used))
          : Number.NaN;
      const reset = windowData.ResetsAt || windowData.resetsAt;
      const resetText = reset ? `重置 ${new Date(reset).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}` : "重置 --";
      const value = Number.isFinite(remaining) ? `${Math.round(remaining)}%` : "--";
      const bar = Number.isFinite(remaining) ? remaining : 0;
      return `
        <div class="quota-window">
          <span>${label}</span>
          <b>${value}</b>
          <div class="quota-bar"><i style="width:${bar}%"></i></div>
          <small>${resetText}</small>
        </div>
      `;
    }

    function summarizeQuotaWindow(windowData) {
      const used = windowData.UsedPercent ?? windowData.usedPercent;
      const providedRemaining = windowData.RemainingPercent ?? windowData.remainingPercent;
      const remaining = Number.isFinite(providedRemaining)
        ? providedRemaining
        : Number.isFinite(used)
          ? 100 - used
          : Number.NaN;
      return Number.isFinite(remaining) ? `${Math.round(remaining)}%` : "--";
    }

    function extractQuotaEmail(providers) {
      for (const provider of providers) {
        const detail = provider.Detail || provider.detail || "";
        const match = detail.match(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/i);
        if (match) return match[0];
      }
      return null;
    }

    function extractQuotaTier(providers) {
      for (const provider of providers) {
        const detail = provider.Detail || provider.detail || "";
        const parts = detail.split("·").map(part => part.trim()).filter(Boolean);
        const tier = parts.find(part => !part.includes("@"));
        if (tier) return tier;
      }
      return null;
    }

    function normalizeTierLabel(tier) {
      const value = String(tier || "").toUpperCase();
      if (value.includes("PRO")) return "PRO";
      if (value.includes("ULTRA")) return "ULTRA";
      if (value.includes("FREE")) return "FREE";
      return value || "PRO";
    }

    function setStatus(text, online) {
      statusText.textContent = text;
      dot.classList.toggle("online", online);
    }

    function setWakeState(text, good) {
      wakeState.textContent = text;
      wakeState.classList.toggle("good", good);
      wakeState.classList.toggle("warn", !good);
    }

    function setAppState(text, good) {
      appState.textContent = text;
      appState.classList.toggle("good", good);
      appState.classList.toggle("warn", !good);
    }

    function setApkState(text, good) {
      apkState.textContent = text;
      apkState.classList.toggle("good", good);
      apkState.classList.toggle("warn", !good);
    }

    function setStreamCapabilityState(text, good) {
      streamCapabilityState.textContent = text;
      streamCapabilityState.classList.toggle("good", good);
      streamCapabilityState.classList.toggle("warn", !good);
    }

    async function loadStreamCapabilities() {
      try {
        const capabilities = await fetchJsonOrThrow("/api/stream/capabilities");
        const h264Supported = Boolean(capabilities?.h264?.supported ?? capabilities?.H264?.Supported);
        const webrtcSupported = Boolean(capabilities?.webrtc?.supported ?? capabilities?.Webrtc?.Supported);
        const h264Metrics = capabilities?.h264?.metrics || capabilities?.H264?.Metrics || null;
        const metricsActive = Boolean(h264Metrics?.Active ?? h264Metrics?.active);
        const recentFps = Number(h264Metrics?.RecentQueuedFps ?? h264Metrics?.recentQueuedFps);
        const recentMbps = Number(h264Metrics?.RecentMbps ?? h264Metrics?.recentMbps);
        const skipped = Number(h264Metrics?.RecentSkippedFps ?? h264Metrics?.recentSkippedFps);
        const metricText = metricsActive && Number.isFinite(recentFps) && Number.isFinite(recentMbps)
          ? `Host H.264 ${recentFps.toFixed(0)}fps ${recentMbps.toFixed(1)}Mbps idle ${Number.isFinite(skipped) ? skipped.toFixed(0) : "0"}fps`
          : "";
        setStreamCapabilityState(
          webrtcSupported
            ? (metricText ? `串流：WebRTC H.264 · ${metricText}` : "串流：iPhone WebRTC H.264 可用，JPEG fallback 已保留。")
            : h264Supported
              ? (metricText ? `串流：${metricText}` : "串流：JPEG 可用，原生 H.264 可用。")
            : "串流：JPEG 可用；H.264 Host 編碼器尚未接上。",
          webrtcSupported || h264Supported);
      } catch {
        setStreamCapabilityState("串流：無法讀取能力狀態。", false);
      }
    }

    function updateInstallState() {
      // No fake install button: iOS must use Safari Share → Add to Home Screen.
      // Android Chrome may still fire beforeinstallprompt; we only surface status text.
      if (isStandaloneApp()) {
        setAppState("App：已在主畫面。", true);
        return;
      }

      if (isIos()) {
        setAppState(
          location.protocol === "https:"
            ? "App：Safari 分享 → 加入主畫面（不要用網頁假按鈕）。"
            : "App：先改 HTTPS，再分享 → 加入主畫面。",
          location.protocol === "https:"
        );
        return;
      }

      if (installPromptEvent) {
        setAppState("App：瀏覽器可安裝（用瀏覽器選單）。", true);
        return;
      }

      if (!window.isSecureContext && location.hostname !== "localhost" && location.hostname !== "127.0.0.1") {
        setAppState("App：安裝提示需要 HTTPS。", false);
        return;
      }

      setAppState("App：可用瀏覽器選單加入主畫面。", false);
    }

    function registerPhoneAppShell() {
      if (!("serviceWorker" in navigator)) {
        updateInstallState();
        return;
      }

      const register = () => {
        navigator.serviceWorker.register("/service-worker.js?v=23")
          .then(registration => {
            serviceWorkerRegistration = registration;
            registration.update().catch(() => {});
            registration.addEventListener("updatefound", () => {
              const worker = registration.installing;
              if (!worker) return;

              worker.addEventListener("statechange", () => {
                if (worker.state === "installed" && navigator.serviceWorker.controller) {
                  setAppState("App：更新已準備好，重新開啟即可套用。", true);
                }
              });
            });
            updateInstallState();
          })
          .catch(() => setAppState("App：離線殼註冊失敗。", false));
      };

      if (document.readyState === "complete") {
        register();
      } else {
        window.addEventListener("load", register, { once: true });
      }
    }

    function getKeepAwakeVideo() {
      return document.getElementById("keepAwakeVideo");
    }

    function getKeepAwakeButton() {
      return document.getElementById("keepAwake");
    }

    function updateKeepAwakeButton() {
      const button = getKeepAwakeButton();
      if (!button) return;
      button.textContent = keepAwakeDesired ? "長亮 ON" : "長亮 OFF";
      button.classList.toggle("active", keepAwakeDesired && (Boolean(wakeLock) || keepAwakeVideoPlaying));
    }

    function updateWakeCapability() {
      // One status line only — no HTTP/video/WakeLock lecture for the user.
      if (!keepAwakeDesired) {
        setWakeState("長亮：關", false);
      } else if (wakeLock || keepAwakeVideoPlaying) {
        setWakeState("長亮：開", true);
      } else if (isIos() && location.protocol !== "https:") {
        setWakeState("長亮：需 HTTPS", false);
      } else {
        setWakeState("長亮：點按鈕開啟", false);
      }
      updateKeepAwakeButton();
    }

    async function startKeepAwakeVideo() {
      const video = getKeepAwakeVideo();
      if (!video) return false;
      try {
        video.muted = true;
        video.defaultMuted = true;
        video.playsInline = true;
        video.setAttribute("playsinline", "");
        video.setAttribute("webkit-playsinline", "");
        video.loop = true;
        if (video.paused) {
          await video.play();
        }
        keepAwakeVideoPlaying = !video.paused;
        return keepAwakeVideoPlaying;
      } catch {
        keepAwakeVideoPlaying = false;
        return false;
      }
    }

    function stopKeepAwakeVideo() {
      const video = getKeepAwakeVideo();
      keepAwakeVideoPlaying = false;
      if (!video) return;
      try {
        video.pause();
        video.currentTime = 0;
      } catch {
      }
    }

    function resetStreamStats() {
      streamStats = {
        frames: 0,
        bytes: 0,
        lastFrames: 0,
        lastBytes: 0,
        lastTime: performance.now()
      };
    }

    function recordFrame(byteLength) {
      if (!streamStats) return;
      streamStats.frames += 1;
      streamStats.bytes += byteLength || 0;
      const now = performance.now();
      const elapsed = now - streamStats.lastTime;
      if (elapsed < 1000) return;

      const frameDelta = streamStats.frames - streamStats.lastFrames;
      const byteDelta = streamStats.bytes - streamStats.lastBytes;
      const fps = frameDelta * 1000 / elapsed;
      const mbps = byteDelta * 8 / elapsed / 1000;
      const protocolLabel = jpegFallbackReason ? `${jpegFallbackReason} · ` : "";
      setStatus(`${protocolLabel}jpeg ${fps.toFixed(0)}fps ${mbps.toFixed(1)}Mbps`, true);
      streamStats.lastFrames = streamStats.frames;
      streamStats.lastBytes = streamStats.bytes;
      streamStats.lastTime = now;
    }

    function getActiveStreamElement() {
      return rtcActive ? rtcScreen : screen;
    }

    function getMediaWidth(element) {
      return element.videoWidth || element.naturalWidth || element.clientWidth || 0;
    }

    function getMediaHeight(element) {
      return element.videoHeight || element.naturalHeight || element.clientHeight || 0;
    }

    function setDisplayAspectRatio(value) {
      screen.style.aspectRatio = value;
      rtcScreen.style.aspectRatio = value;
    }

    function applyRotation() {
      document.body.classList.remove("rotate-90", "rotate-180", "rotate-270");
      const resolvedRotation = resolveRotation();
      if (resolvedRotation !== "0") {
        document.body.classList.add(`rotate-${resolvedRotation}`);
      }
      localStorage.setItem("phoneMonitorRotation", rotation.value);
    }

    function applyOrientation() {
      const value = ["auto", "portrait", "landscape"].includes(orientation.value)
        ? orientation.value
        : "auto";
      orientation.value = value;
      localStorage.setItem("phoneMonitorOrientation", value);
      applyForcedLandscape();
      updateViewportSize();
      if (window.screen?.orientation?.lock && value !== "auto") {
        window.screen.orientation.lock(value).catch(() => {});
      }
    }

    function resolveRotation() {
      if (rotation.value !== "auto") return rotation.value;

      const viewportWidth = window.visualViewport ? window.visualViewport.width : window.innerWidth;
      const viewportHeight = window.visualViewport ? window.visualViewport.height : window.innerHeight;
      const viewportPortrait = viewportHeight > viewportWidth;
      const activeMedia = getActiveStreamElement();
      const streamPortrait = getMediaHeight(activeMedia) > getMediaWidth(activeMedia);

      return viewportPortrait !== streamPortrait ? "90" : "0";
    }

    async function requestWakeLock() {
      return ensureKeepAwake();
    }

    async function ensureKeepAwake() {
      if (!keepAwakeDesired) {
        updateWakeCapability();
        return false;
      }

      if (document.visibilityState === "hidden") {
        updateWakeCapability();
        return false;
      }

      let locked = false;

      // 1) Official API — needs secure context; works better on iOS 18.4+ home screen.
      if ("wakeLock" in navigator && window.isSecureContext) {
        try {
          if (!wakeLock) {
            wakeLock = await navigator.wakeLock.request("screen");
            wakeLock.addEventListener("release", () => {
              wakeLock = null;
              if (keepAwakeDesired && document.visibilityState === "visible") {
                // Fall back to silent video if the OS drops the lock.
                startKeepAwakeVideo().finally(updateWakeCapability);
              } else {
                updateWakeCapability();
              }
            });
          }
          locked = Boolean(wakeLock);
        } catch {
          wakeLock = null;
        }
      }

      // 2) iOS / mobile fallback: silent looping video (works on HTTP with a user gesture).
      if (!locked && (isIos() || isMobileClient() || !window.isSecureContext)) {
        locked = await startKeepAwakeVideo();
      }

      updateWakeCapability();
      return locked;
    }

    async function setKeepAwakeDesired(enabled) {
      keepAwakeDesired = Boolean(enabled);
      localStorage.setItem("phoneMonitorKeepAwake", keepAwakeDesired ? "1" : "0");
      if (!keepAwakeDesired) {
        try {
          await wakeLock?.release();
        } catch {
        }
        wakeLock = null;
        stopKeepAwakeVideo();
        updateWakeCapability();
        return;
      }

      await ensureKeepAwake();
    }

    function startKeepAwakeWatch() {
      if (keepAwakeWatchTimer) return;
      keepAwakeWatchTimer = setInterval(() => {
        if (!keepAwakeDesired || document.visibilityState !== "visible") return;
        if (wakeLock) return;
        const video = getKeepAwakeVideo();
        if (video && !video.paused) {
          keepAwakeVideoPlaying = true;
          return;
        }
        ensureKeepAwake();
      }, 15000);
    }

    async function enterLandscapeViewer() {
      updateViewportSize();
      const isDisplayMode = activeMode === "display";
      document.body.classList.toggle("viewer-fullscreen", isDisplayMode);
      document.body.classList.toggle("dashboard-viewer", !isDisplayMode);
      notifyNativeViewerMode(true);
      window.scrollTo(0, 1);
      setTimeout(() => {
        updateViewportSize();
        applyRotation();
      }, 250);

      // Safari iOS ignores standard Fullscreen API for arbitrary elements; CSS viewer is enough.
      if (isDisplayMode && !isIos() && document.fullscreenEnabled && document.documentElement.requestFullscreen) {
        try {
          await document.documentElement.requestFullscreen({ navigationUI: "hide" });
        } catch {
        }
      }

      if (isDisplayMode && orientation.value === "landscape" && window.screen && window.screen.orientation && window.screen.orientation.lock) {
        try {
          await window.screen.orientation.lock("landscape");
        } catch {
        }
      }
      await ensureKeepAwake();
      applyRotation();
      if (isDisplayMode) {
        connectVideo();
      }
    }

    function toggleDisplayViewerFromScreen() {
      if (activeMode !== "display") return;
      if (document.body.classList.contains("viewer-fullscreen")) {
        exitLandscapeViewer();
      } else {
        enterLandscapeViewer();
      }
    }

    async function exitLandscapeViewer() {
      document.body.classList.remove("viewer-fullscreen");
      document.body.classList.remove("dashboard-viewer");
      notifyNativeViewerMode(false);
      if (document.fullscreenElement && document.exitFullscreen) {
        try {
          await document.exitFullscreen();
        } catch {
        }
      }
      applyRotation();
    }

    async function loadPhoneDisplay() {
      let displays;
      try {
        displays = await fetchJsonOrThrow("/api/displays");
      } catch (error) {
        selectedDisplayName = "";
        setDisplayAspectRatio("16 / 9");
        if (isTrustRequiredError(error)) {
          setStatus("請先配對手機", false);
          driverState.textContent = "請先配對手機，才能觀看虛擬螢幕。";
          return;
        }

        setStatus("顯示器無法使用", false);
        driverState.textContent = error.message || "無法取得 VibeDeck 顯示器狀態。";
        return;
      }

      const phoneDisplay = displays.find(display => display.IsPhoneMonitor);

      if (!phoneDisplay) {
        selectedDisplayName = "";
        setDisplayAspectRatio("16 / 9");
        setStatus("找不到虛擬螢幕", false);
        driverState.textContent = "找不到 VibeDeck 虛擬螢幕。";
        return;
      }

      selectedDisplayName = phoneDisplay.DeviceName;
      setDisplayAspectRatio(`${phoneDisplay.Width} / ${phoneDisplay.Height}`);
      driverState.textContent = `VibeDeck 顯示器：${phoneDisplay.DeviceName} (${phoneDisplay.Width}x${phoneDisplay.Height})`;
    }

    function jpegStreamUrl() {
      const params = appendDeviceToken(new URLSearchParams({
        deviceName: selectedDisplayName,
        fps: streamFps.value,
        quality: streamQuality.value
      }));
      return `${wsBase}/ws/display?${params.toString()}`;
    }

    function inputSocketUrl() {
      const params = appendDeviceToken(new URLSearchParams());
      const query = params.toString();
      return `${wsBase}/ws/input${query ? `?${query}` : ""}`;
    }

    function closeJpegStream() {
      if (videoSocket) videoSocket.close();
      videoSocket = null;
      clearJpegFrameQueue();
    }

    function closeRtcStream(invalidate = true) {
      if (invalidate) rtcConnectGeneration += 1;
      const peer = rtcPeer;
      rtcPeer = null;
      rtcActive = false;
      if (peer) {
        try {
          peer.close();
        } catch {
        }
      }
      if (rtcDisconnectTimer) {
        clearTimeout(rtcDisconnectTimer);
        rtcDisconnectTimer = null;
      }
      if (rtcStatsTimer) {
        clearInterval(rtcStatsTimer);
        rtcStatsTimer = null;
      }
      rtcScreen.srcObject = null;
      rtcScreen.hidden = true;
      screen.hidden = false;
    }

    function clearJpegFrameQueue() {
      pendingJpegFrame = null;
      if (activeJpegObjectUrl) {
        URL.revokeObjectURL(activeJpegObjectUrl);
        activeJpegObjectUrl = null;
      }
      jpegFrameDecoding = false;
    }

    function presentPendingJpegFrame() {
      if (jpegFrameDecoding || !pendingJpegFrame) return;

      const frame = pendingJpegFrame;
      pendingJpegFrame = null;
      jpegFrameDecoding = true;
      const url = URL.createObjectURL(frame);
      activeJpegObjectUrl = url;

      if (typeof screen.decode === "function") {
        screen.src = url;
        screen.decode().then(
          () => finishJpegFrameDecode(url),
          () => finishJpegFrameDecode(url)
        );
        return;
      }

      // Fallback for older WebKit: the URL token prevents a late event from a
      // previous frame from completing the currently decoding frame.
      const complete = () => {
        screen.removeEventListener("load", complete);
        screen.removeEventListener("error", complete);
        finishJpegFrameDecode(url);
      };
      screen.addEventListener("load", complete, { once: true });
      screen.addEventListener("error", complete, { once: true });
      screen.src = url;
    }

    function finishJpegFrameDecode(url) {
      if (!jpegFrameDecoding || url !== activeJpegObjectUrl) return;

      if (activeJpegObjectUrl) {
        // Revoking after load is safe: WebKit has fully consumed the Blob.
        URL.revokeObjectURL(activeJpegObjectUrl);
        activeJpegObjectUrl = null;
      }
      jpegFrameDecoding = false;

      // Keep only the newest frame while Safari is decoding.  This is the
      // important part for responsiveness: stale frames are deliberately
      // discarded instead of being rendered late.
      requestAnimationFrame(presentPendingJpegFrame);
    }

    async function connectVideo() {
      const generation = ++rtcConnectGeneration;
      jpegFallbackReason = "";
      closeJpegStream();
      closeRtcStream(false);

      if (!canUseProtectedConnection()) {
        setStatus("請先配對手機", false);
        return;
      }

      if (!selectedDisplayName) {
        await loadPhoneDisplay();
      }

      if (generation !== rtcConnectGeneration || !selectedDisplayName) return;

      // iOS Safari can hardware-decode H.264 in WebRTC, but it cannot consume
      // the Host's raw Annex-B WebSocket directly.  Keep JPEG as a reliable
      // fallback for unsupported browsers, insecure contexts, or failed ICE.
      if (prefersWebRtcDisplay() && !isNativeShell() && !window.RTCPeerConnection) {
        jpegFallbackReason = "WebRTC API 不可用";
      } else if (prefersWebRtcDisplay() && !isNativeShell() && window.RTCPeerConnection) {
        try {
          const connected = await connectRtcVideo(generation);
          if (connected && generation === rtcConnectGeneration) return;
        } catch (error) {
          console.warn("WebRTC negotiation failed; using JPEG fallback", error);
          if (generation === rtcConnectGeneration) {
            setStatus(`WebRTC 無法連線，切回 JPEG：${error.message || "未知錯誤"}`, false);
          }
          jpegFallbackReason = `WebRTC fallback：${error.message || "未知錯誤"}`;
          closeRtcStream(false);
        }
      }

      if (generation !== rtcConnectGeneration) return;
      connectJpegVideo();
    }

    function waitForIceGatheringComplete(peer, timeoutMs = 1800) {
      if (peer.iceGatheringState === "complete") return Promise.resolve();
      return new Promise(resolve => {
        let finished = false;
        const finish = () => {
          if (finished) return;
          finished = true;
          clearTimeout(timer);
          peer.removeEventListener("icegatheringstatechange", onStateChange);
          resolve();
        };
        const onStateChange = () => {
          if (peer.iceGatheringState === "complete") finish();
        };
        const timer = setTimeout(finish, timeoutMs);
        peer.addEventListener("icegatheringstatechange", onStateChange);
      });
    }

    async function connectRtcVideo(generation) {
      if (generation !== rtcConnectGeneration) return false;
      if (!window.isSecureContext && !isLoopbackHost()) {
        throw new Error("WebRTC 需要 HTTPS");
      }

      const peer = new RTCPeerConnection({ iceServers: [] });
      rtcPeer = peer;
      rtcActive = true;
      screen.hidden = true;
      rtcScreen.hidden = false;
      rtcScreen.onloadedmetadata = () => {
        applyRotation();
        rtcScreen.play().catch(() => {});
        setStatus("WebRTC H.264 已連線", true);
      };
      peer.ontrack = event => {
        if (generation !== rtcConnectGeneration || rtcPeer !== peer) return;
        const stream = event.streams?.[0] || new MediaStream([event.track]);
        rtcScreen.srcObject = stream;
        rtcScreen.play().catch(() => {});
        const receiver = event.receiver;
        try {
          if ("jitterBufferTarget" in receiver) receiver.jitterBufferTarget = 0;
          if ("playoutDelayHint" in receiver) receiver.playoutDelayHint = 0;
        } catch { }
        startRtcStats(peer, generation);
      };
      peer.onconnectionstatechange = () => {
        if (generation !== rtcConnectGeneration || rtcPeer !== peer) return;
        if (peer.connectionState === "disconnected") {
          // Safari can report a short disconnected state while the Wi-Fi path
          // is being re-selected.  Rebuilding the peer immediately causes a
          // visible JPEG fallback and makes the next RTC attempt less stable.
          if (rtcDisconnectTimer) clearTimeout(rtcDisconnectTimer);
          rtcDisconnectTimer = setTimeout(() => {
            rtcDisconnectTimer = null;
            if (generation !== rtcConnectGeneration || rtcPeer !== peer || peer.connectionState !== "disconnected") return;
            closeRtcStream(false);
            setStatus("WebRTC 中斷（ICE disconnected），切回 JPEG", false);
            connectJpegVideo("WebRTC ICE disconnected");
          }, 3000);
          return;
        }
        if (["failed", "closed"].includes(peer.connectionState)) {
          closeRtcStream(false);
          setStatus(`WebRTC 中斷（${peer.connectionState}），切回 JPEG`, false);
          connectJpegVideo(`WebRTC ${peer.connectionState}`);
        }
      };
      peer.oniceconnectionstatechange = () => {
        if (generation !== rtcConnectGeneration || rtcPeer !== peer) return;
        if (peer.iceConnectionState === "failed") {
          setStatus("WebRTC ICE failed，切回 JPEG", false);
        }
      };

      peer.addTransceiver("video", { direction: "recvonly" });
      const offer = await peer.createOffer();
      if (generation !== rtcConnectGeneration || rtcPeer !== peer) {
        try {
          peer.close();
        } catch {
        }
        return false;
      }
      await peer.setLocalDescription(offer);
      await waitForIceGatheringComplete(peer);
      if (generation !== rtcConnectGeneration || rtcPeer !== peer) return false;

      const answer = await fetchJsonOrThrow("/api/stream/webrtc/offer", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          sdp: peer.localDescription?.sdp || offer.sdp,
          deviceName: selectedDisplayName,
          fps: Number(streamFps.value),
          quality: Number(streamQuality.value)
        })
      });

      // A socket close, page visibility change, or a second reconnect can
      // finish while the signalling request is in flight.  Safari reports
      // this race as "The object is in an invalid state" when an answer is
      // applied to the already-closed peer.  Never touch a stale peer.
      if (generation !== rtcConnectGeneration || rtcPeer !== peer || peer.signalingState === "closed") {
        try {
          peer.close();
        } catch {
        }
        return false;
      }

      await peer.setRemoteDescription({
        type: answer.Type || answer.type || "answer",
        sdp: answer.Sdp || answer.sdp || ""
      });
      return true;
    }

    function startRtcStats(peer, generation) {
      if (rtcStatsTimer) clearInterval(rtcStatsTimer);
      let previous = null;
      rtcStatsTimer = setInterval(async () => {
        if (generation !== rtcConnectGeneration || rtcPeer !== peer || peer.connectionState === "closed") return;
        try {
          const reports = await peer.getStats();
          let inbound = null;
          reports.forEach(report => {
            if (report.type === "inbound-rtp" && (report.kind === "video" || report.mediaType === "video")) inbound = report;
          });
          if (!inbound) return;
          const now = Number(inbound.timestamp || performance.now());
          if (previous) {
            const seconds = Math.max(.001, (now - previous.time) / 1000);
            const fps = Math.max(0, (Number(inbound.framesDecoded || 0) - previous.frames) / seconds);
            const mbps = Math.max(0, (Number(inbound.bytesReceived || 0) - previous.bytes) * 8 / seconds / 1e6);
            const dropped = Math.max(0, Number(inbound.framesDropped || 0) - previous.dropped);
            const jitterMs = Math.max(0, Number(inbound.jitter || 0) * 1000);
            const emitted = Number(inbound.jitterBufferEmittedCount || 0);
            const bufferMs = emitted > 0 ? Number(inbound.jitterBufferDelay || 0) / emitted * 1000 : 0;
            const decoded = Number(inbound.framesDecoded || 0);
            const decodeMs = decoded > 0 ? Number(inbound.totalDecodeTime || 0) / decoded * 1000 : 0;
            setStatus(`H.264 ${fps.toFixed(0)}fps ${mbps.toFixed(1)}Mbps · jitter ${jitterMs.toFixed(0)}ms · buffer ${bufferMs.toFixed(0)}ms · decode ${decodeMs.toFixed(1)}ms${dropped ? ` · drop ${dropped}` : ""}`, fps >= 20 && bufferMs < 250);
          }
          previous = {
            time: now,
            frames: Number(inbound.framesDecoded || 0),
            bytes: Number(inbound.bytesReceived || 0),
            dropped: Number(inbound.framesDropped || 0)
          };
        } catch { }
      }, 1000);
    }

    function connectJpegVideo(fallbackReason = "") {
      jpegFallbackReason = fallbackReason || jpegFallbackReason;
      screen.hidden = false;
      applyRotation();
      const socket = new WebSocket(jpegStreamUrl());
      videoSocket = socket;
      socket.binaryType = "blob";
      resetStreamStats();

      socket.onopen = () => setStatus(jpegFallbackReason ? `${jpegFallbackReason} · JPEG` : "影像已連線", true);
      socket.onclose = () => {
        if (videoSocket !== socket) return;
        setStatus("重新連線中", false);
        setTimeout(connectVideo, 1000);
      };
      socket.onerror = () => setStatus("影像連線錯誤", false);
      socket.onmessage = event => {
        // A latest-frame-wins queue bounds both memory and decoder work on
        // iPhone.  At 30fps the old one-second revoke delay retained roughly
        // 30 full JPEGs and let WebKit render them long after they were useful.
        pendingJpegFrame = event.data;
        presentPendingJpegFrame();
        recordFrame(event.data.size);
        if (rotation.value === "auto") {
          requestAnimationFrame(applyRotation);
        }
      };
    }

    function applyStreamPresetFields() {
      const presets = {
        battery: { fps: 30, quality: 48 },
        balanced: { fps: 45, quality: 56 },
        smooth: { fps: 60, quality: 60 },
        sharp: { fps: 60, quality: 68 }
      };
      const preset = presets[streamPreset.value];
      if (preset) {
        streamFps.value = preset.fps;
        streamQuality.value = preset.quality;
      }
      localStorage.setItem("phoneMonitorStreamPreset", streamPreset.value);
    }

    function applyStreamSettings() {
      localStorage.setItem("phoneMonitorStreamFps", streamFps.value);
      localStorage.setItem("phoneMonitorStreamQuality", streamQuality.value);
      connectVideo();
    }

    async function loadDisplayStatus() {
      try {
        const status = await fetchJsonOrThrow("/api/display/status");
        driverState.textContent = `${driverState.textContent} 虛擬螢幕：${status.State}.`;
      } catch (error) {
        if (!isTrustRequiredError(error)) {
          driverState.textContent = `${driverState.textContent} 無法取得虛擬螢幕狀態。`;
        }
      }
    }

    async function loadConnectInfo() {
      const response = await fetch("/api/connect", { cache: "no-store" });
      const info = await response.json();
      const nativeUrl = info.NativeAppUrl || info.nativeAppUrl || info.AndroidAppUrl || info.androidAppUrl || info.IosAppUrl || info.iosAppUrl;
      const nativeCertUrl = info.NativeAppCertificateUrl || info.nativeAppCertificateUrl || info.AndroidAppCertificateUrl || info.androidAppCertificateUrl || "";
      const androidRelease = info.AndroidRelease || info.androidRelease || {};
      const apkAvailable = Boolean(androidRelease.Available ?? androidRelease.available);
      const apkInstallPageUrl = androidRelease.InstallPageUrl || androidRelease.installPageUrl || "";
      const apkDownloadUrl = androidRelease.DownloadUrl || androidRelease.downloadUrl || "";
      const apkQrUrl = androidRelease.QrUrl || androidRelease.qrUrl || "/qr/apk.svg";
      const apkShaUrl = androidRelease.Sha256Url || androidRelease.sha256Url || "/download/vibedeck-android.apk.sha256";
      const apkVersionName = androidRelease.VersionName || androidRelease.versionName || "";
      const apkVersionCode = androidRelease.VersionCode ?? androidRelease.versionCode ?? "";
      const apkSizeBytes = Number(androidRelease.SizeBytes ?? androidRelease.sizeBytes ?? 0);
      const httpsAvailable = Boolean(info.HttpsAvailable ?? info.httpsAvailable);
      const rootCertificateUrl = info.RootCertificateUrl || info.rootCertificateUrl || "";
      const httpsSetupHint = info.HttpsSetupHint || info.httpsSetupHint || "";
      androidApkAvailable = apkAvailable;
      androidApkQrUrl = apkQrUrl;
      if (!pairingQrActive) {
        showInstallQr();
      }
      const httpsUrl = info.HttpsUrl || info.httpsUrl || "";
      const httpUrl = info.HttpUrl || info.httpUrl || "";
      // PC console: HTTPS is the canonical phone URL; HTTP is bootstrap only (cert / Android).
      prettyLink.href = httpsAvailable ? httpsUrl : (info.LocalNameHttpUrl || httpUrl);
      prettyLink.textContent = httpsAvailable
        ? `iPhone 請用：${httpsUrl}`
        : `本機：${info.LocalNameHttpUrl || httpUrl}`;
      httpsLink.href = httpsAvailable ? httpsUrl : "#";
      httpsLink.textContent = httpsAvailable
        ? `HTTPS（iPhone 專用）：${httpsUrl}`
        : `HTTPS 未就緒：${httpsSetupHint || "PC 執行 scripts\\setup-https.ps1"}`;
      httpsCertLink.hidden = !httpsAvailable || !rootCertificateUrl;
      httpsCertLink.href = rootCertificateUrl || "#";
      httpsCertLink.textContent = "iPhone 步驟1：安裝 HTTPS 憑證";
      httpLink.href = httpUrl || "#";
      httpLink.textContent = `HTTP（僅 Android / 下憑證）：${httpUrl}`;
      const connectTitleHint = document.getElementById("connectTitleHint");
      if (connectTitleHint) {
        connectTitleHint.textContent = "打開 VibeDeck App，它會自動找到這台 PC。第一次只需在下方允許一次。";
      }
      const gateCert = document.getElementById("iosGateCertLink");
      if (gateCert && rootCertificateUrl) {
        gateCert.href = rootCertificateUrl;
      }
      const gateHttps = document.getElementById("iosGateHttpsUrl");
      if (gateHttps && httpsUrl) {
        gateHttps.href = httpsUrl;
        gateHttps.textContent = httpsUrl;
      }
      const openHttps = document.getElementById("iosGateOpenHttps");
      if (openHttps) {
        const target = buildHttpsUrlFromCurrent() || (httpsUrl ? new URL("index.html" + (location.search || ""), httpsUrl).toString() : "");
        openHttps.onclick = () => {
          if (target) location.replace(target);
        };
      }
      androidApkLink.hidden = !apkAvailable || !apkDownloadUrl;
      androidApkLink.href = apkInstallPageUrl || apkDownloadUrl || "#";
      androidApkLink.textContent = apkVersionName
        ? `下載 Android APK v${apkVersionName}`
        : "下載 Android APK";
      androidApkQrLink.hidden = !apkAvailable;
      androidApkQrLink.href = apkQrUrl;
      androidApkQrLink.textContent = "Android APK QR";
      androidApkShaLink.hidden = !apkAvailable;
      androidApkShaLink.href = apkShaUrl;
      androidApkShaLink.textContent = "Android APK SHA256";
      setApkState(
        apkAvailable
          ? `Android APK：${apkVersionName ? `v${apkVersionName}` : "可下載"}${apkVersionCode ? ` (${apkVersionCode})` : ""}${apkSizeBytes ? `，${formatFileSize(apkSizeBytes)}` : ""}。`
          : "Android APK：尚未建立，PC 執行 scripts\\build-android-release.ps1。",
        apkAvailable);
      nativeAppLink.href = nativeUrl || "#";
      nativeAppLink.textContent = nativeUrl ? "開啟手機 App" : "手機 App 連結無法使用";
      nativeCertLink.href = nativeCertUrl || "#";
      nativeCertLink.textContent = nativeCertUrl ? "Android HTTPS 信任" : "Android HTTPS 信任無法使用";
      updateWakeCapability();
    }

    async function loadModePresets() {
      const response = await fetch("/api/display/modes");
      const presets = await response.json();
      modePreset.textContent = "";
      for (const preset of presets) {
        const option = document.createElement("option");
        option.value = JSON.stringify(preset);
        option.textContent = preset.Label;
        modePreset.append(option);
      }
      modePreset.value = localStorage.getItem("phoneMonitorModePreset") || modePreset.options[0]?.value || "";
      if (isIphone()) {
        const iphonePreset = Array.from(modePreset.options)
          .find(option => JSON.parse(option.value).Id === IPHONE_XS_EXACT_PRESET);
        if (iphonePreset) {
          modePreset.value = iphonePreset.value;
        }
      }
      applyPresetFields();

      // The iPhone XS test device is 812×375 in landscape.  Apply this once
      // after pairing so the virtual display, WebRTC video and browser canvas
      // share an aspect ratio instead of relying on visible crop/stretch.
      const aspectKey = `phoneMonitorIosAspectApplied:${IPHONE_XS_ASPECT_VERSION}`;
      if (isIphone() && deviceTrusted && localStorage.getItem(aspectKey) !== "1") {
        try {
          await applyDisplayMode();
          localStorage.setItem(aspectKey, "1");
        } catch (error) {
          setStatus(error.message || "iPhone 副螢幕比例套用失敗", false);
        }
      }
    }

    function applyPresetFields() {
      if (!modePreset.value) return;
      const preset = JSON.parse(modePreset.value);
      modeWidth.value = preset.Width;
      modeHeight.value = preset.Height;
      modeRefresh.value = preset.RefreshRate;
      localStorage.setItem("phoneMonitorModePreset", modePreset.value);
    }

    async function applyDisplayMode() {
      const result = await fetchJsonOrThrow("/api/display/mode", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          Width: Number(modeWidth.value),
          Height: Number(modeHeight.value),
          RefreshRate: Number(modeRefresh.value)
        })
      });
      driverState.textContent = result.Message;
      selectedDisplayName = "";
      setTimeout(async () => {
        await loadPhoneDisplay();
        connectVideo();
      }, 1200);
    }

    function connectInput() {
      if (!canUseProtectedConnection()) {
        return;
      }

      inputSocket = new WebSocket(inputSocketUrl());
      inputSocket.onclose = () => setTimeout(connectInput, 1000);
    }

    function clamp01(value) {
      return Math.max(0, Math.min(1, value));
    }

    function getScreenContentBox() {
      const media = getActiveStreamElement();
      const rect = media.getBoundingClientRect();
      const naturalWidth = getMediaWidth(media) || rect.width;
      const naturalHeight = getMediaHeight(media) || rect.height;
      const fit = getComputedStyle(media).objectFit || "contain";

      if (fit === "cover" && naturalWidth && naturalHeight && rect.width && rect.height) {
        const scale = Math.max(rect.width / naturalWidth, rect.height / naturalHeight);
        const width = naturalWidth * scale;
        const height = naturalHeight * scale;
        return {
          left: rect.left + ((rect.width - width) / 2),
          top: rect.top + ((rect.height - height) / 2),
          width,
          height
        };
      }

      if (fit !== "contain" || !naturalWidth || !naturalHeight || !rect.width || !rect.height) {
        return {
          left: rect.left,
          top: rect.top,
          width: rect.width,
          height: rect.height
        };
      }

      const elementAspect = rect.width / rect.height;
      const contentAspect = naturalWidth / naturalHeight;

      if (contentAspect > elementAspect) {
        const height = rect.width / contentAspect;
        return {
          left: rect.left,
          top: rect.top + ((rect.height - height) / 2),
          width: rect.width,
          height
        };
      }

      const width = rect.height * contentAspect;
      return {
        left: rect.left + ((rect.width - width) / 2),
        top: rect.top,
        width,
        height: rect.height
      };
    }

    function mapPointerToDisplay(event) {
      const box = getScreenContentBox();
      if (!box.width || !box.height) return null;

      const rawX = (event.clientX - box.left) / box.width;
      const rawY = (event.clientY - box.top) / box.height;
      if (rawX < 0 || rawX > 1 || rawY < 0 || rawY > 1) return null;

      const resolvedRotation = resolveRotation();
      let x = rawX;
      let y = rawY;

      if (resolvedRotation === "90") {
        x = rawY;
        y = 1 - rawX;
      } else if (resolvedRotation === "180") {
        x = 1 - rawX;
        y = 1 - rawY;
      } else if (resolvedRotation === "270") {
        x = 1 - rawY;
        y = rawX;
      }

      return {
        x: clamp01(x),
        y: clamp01(y)
      };
    }

    function buildPointerPayload(event) {
      const point = mapPointerToDisplay(event);
      if (!point) return null;
      return {
        deviceName: selectedDisplayName,
        x: point.x,
        y: point.y,
        buttons: event.buttons || 0
      };
    }

    function sendPointerMessage(type, payload, buttonsOverride) {
      if (!inputSocket || inputSocket.readyState !== WebSocket.OPEN) return;
      inputSocket.send(JSON.stringify({
        type,
        deviceName: payload.deviceName,
        x: payload.x,
        y: payload.y,
        buttons: buttonsOverride ?? payload.buttons ?? 0
      }));
    }

    function sendPointer(type, event, buttonsOverride) {
      const payload = buildPointerPayload(event);
      if (!payload) return false;
      event.preventDefault();
      sendPointerMessage(type, payload, buttonsOverride);
      return true;
    }

    function clearTouchInputState() {
      if (touchInputState?.timer) {
        clearTimeout(touchInputState.timer);
      }
      touchInputState = null;
    }

    function isTouchPointer(event) {
      return event.pointerType === "touch";
    }

    function beginTouchInput(event) {
      const payload = buildPointerPayload(event);
      if (!payload) return;

      clearTouchInputState();
      touchInputState = {
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        lastPayload: payload,
        dragStarted: false,
        longPressTriggered: false,
        timer: setTimeout(() => {
          if (!touchInputState || touchInputState.pointerId !== event.pointerId || touchInputState.dragStarted) {
            return;
          }

          touchInputState.longPressTriggered = true;
          sendPointerMessage("rightclick", touchInputState.lastPayload, 2);
        }, touchLongPressMs)
      };
      event.preventDefault();
    }

    function updateTouchInput(event) {
      if (!touchInputState || touchInputState.pointerId !== event.pointerId) return;
      const payload = buildPointerPayload(event);
      if (!payload) return;

      touchInputState.lastPayload = payload;
      const moved = Math.hypot(event.clientX - touchInputState.startX, event.clientY - touchInputState.startY) >= touchDragThresholdPx;

      if (touchInputState.longPressTriggered) {
        event.preventDefault();
        return;
      }

      if (!moved && !touchInputState.dragStarted) {
        return;
      }

      if (touchInputState.timer) {
        clearTimeout(touchInputState.timer);
        touchInputState.timer = null;
      }

      if (!touchInputState.dragStarted) {
        touchInputState.dragStarted = true;
        sendPointerMessage("pointerdown", payload, 1);
      }

      sendPointerMessage("pointermove", payload, 1);
      event.preventDefault();
    }

    function endTouchInput(event, cancelled) {
      if (!touchInputState || touchInputState.pointerId !== event.pointerId) return;

      const payload = buildPointerPayload(event) || touchInputState.lastPayload;
      if (touchInputState.timer) {
        clearTimeout(touchInputState.timer);
        touchInputState.timer = null;
      }

      if (touchInputState.longPressTriggered) {
        clearTouchInputState();
        event.preventDefault();
        return;
      }

      if (touchInputState.dragStarted) {
        if (payload) {
          sendPointerMessage(cancelled ? "pointercancel" : "pointerup", payload, 0);
        }
        clearTouchInputState();
        event.preventDefault();
        return;
      }

      if (payload) {
        sendPointerMessage("pointerdown", payload, 1);
        sendPointerMessage("pointerup", payload, 0);
      }
      clearTouchInputState();
      event.preventDefault();
    }

    function wireDisplayPointerTarget(target) {
      target.addEventListener("dragstart", event => event.preventDefault());
      target.addEventListener("contextmenu", event => event.preventDefault());
      target.addEventListener("pointerdown", event => {
        target.setPointerCapture(event.pointerId);
        if (isTouchPointer(event)) {
          beginTouchInput(event);
          return;
        }
        sendPointer("pointerdown", event);
      });
      target.addEventListener("pointermove", event => {
        if (isTouchPointer(event)) {
          updateTouchInput(event);
          return;
        }
        sendPointer("pointermove", event);
      });
      target.addEventListener("pointerup", event => {
        if (isTouchPointer(event)) {
          endTouchInput(event, false);
          return;
        }
        sendPointer("pointerup", event);
      });
      target.addEventListener("pointercancel", event => {
        if (isTouchPointer(event)) {
          endTouchInput(event, true);
          return;
        }
        sendPointer("pointercancel", event);
      });
      target.addEventListener("dblclick", event => {
        // Mobile clients use the toggle handler below; avoid firing two
        // handlers that would enter and immediately exit fullscreen.
        if (!isMobileClient()) enterLandscapeViewer(event);
      });
    }

    wireDisplayPointerTarget(screen);
    wireDisplayPointerTarget(rtcScreen);
    quotaGrid.addEventListener("pointerdown", event => {
      quotaSwipeStartX = event.clientX;
    });
    quotaGrid.addEventListener("pointerup", event => {
      if (quotaSwipeStartX == null) return;
      const delta = event.clientX - quotaSwipeStartX;
      quotaSwipeStartX = null;
      if (Math.abs(delta) > 48) {
        changeQuotaAccount(quotaActiveTab, delta < 0 ? 1 : -1);
      }
    });
    quotaGrid.addEventListener("pointercancel", () => {
      quotaSwipeStartX = null;
    });
    rotation.addEventListener("change", applyRotation);
    orientation.addEventListener("change", applyOrientation);
    streamPreset.addEventListener("change", () => {
      applyStreamPresetFields();
      applyStreamSettings();
    });
    applyStream.addEventListener("click", applyStreamSettings);
    modePreset.addEventListener("change", applyPresetFields);
    applyMode.addEventListener("click", applyDisplayMode);
    pairPhone.addEventListener("click", () => {
      startPhonePairing().catch(error => setTrustState(error.message || "配對失敗。", false));
    });
    launchDeckWindow.addEventListener("click", () => {
      launchDeckOnVirtualDisplay();
    });
    refreshTrustedDevices.addEventListener("click", () => {
      loadDeviceTrustStatus();
    });
    clearTrustedDevices.addEventListener("click", () => {
      clearAllTrustedDevices(clearTrustedDevices);
    });
    trustedDeviceList.addEventListener("click", event => {
      const button = event.target.closest("[data-device-revoke]");
      if (!button) return;
      revokeTrustedDevice(button.dataset.deviceRevoke, button);
    });
    pendingPairingList.addEventListener("click", event => {
      const approve = event.target.closest("[data-pair-approve]");
      const deny = event.target.closest("[data-pair-deny]");
      const button = approve || deny;
      if (!button) return;
      button.disabled = true;
      actOnPendingApproval(button.dataset.requestId, Boolean(approve)).catch(error => setTrustState(error.message || "配對操作失敗。", false));
    });
    refresh.addEventListener("click", async () => {
      selectedDisplayName = "";
      pairingQrActive = false;
      await loadConnectInfo();
      await loadStreamCapabilities();
      await loadDeviceTrustStatus();
      await loadPhoneDisplay();
      connectVideo();
      refreshSideboard();
      refreshQuotas();
    });
    displayMode.addEventListener("click", () => setMode("display"));
    sideboardMode.addEventListener("click", () => setMode("sideboard"));
    quotaMode.addEventListener("click", () => setMode("quota"));
    for (const button of document.querySelectorAll("[data-side-skin]")) {
      button.addEventListener("click", () => setSideSkin(button.dataset.sideSkin));
    }
    const pairLinkSubmit = document.getElementById("pairLinkSubmit");
    if (pairLinkSubmit) {
      pairLinkSubmit.addEventListener("click", () => {
        completePairingFromPastedLink();
      });
    }
    const keepAwakeButton = document.getElementById("keepAwake");
    if (keepAwakeButton) {
      keepAwakeButton.addEventListener("click", async () => {
        await setKeepAwakeDesired(!keepAwakeDesired);
      });
    }
    fullscreen.addEventListener("click", enterLandscapeViewer);
    exitViewer.addEventListener("click", exitLandscapeViewer);
    window.PhoneMonitorExitViewer = exitLandscapeViewer;
    document.addEventListener("fullscreenchange", () => {
      if (!document.fullscreenElement && !isIos()) {
        document.body.classList.remove("viewer-fullscreen");
      }
      notifyNativeViewerMode(document.body.classList.contains("viewer-fullscreen") || document.body.classList.contains("dashboard-viewer"));
      applyRotation();
    });
    document.addEventListener("visibilitychange", async () => {
      if (document.visibilityState === "visible") {
        scheduleDashboardRefresh("sideboard", true);
        scheduleDashboardRefresh("quota", true);
        if (keepAwakeDesired) {
          await ensureKeepAwake();
        }
        if (activeMode === "display" && canUseProtectedConnection()) {
          connectVideo();
        }
        return;
      }

      // Background: drop wake helpers + JPEG to save battery/heat.
      try {
        await wakeLock?.release();
      } catch {
      }
      wakeLock = null;
      stopKeepAwakeVideo();
      if (isMobileClient()) {
        closeJpegStream();
        closeRtcStream();
      }
    });

    // Any user gesture can (re)arm iOS keep-awake.
    document.addEventListener("pointerdown", () => {
      if (keepAwakeDesired && (isIos() || isMobileClient())) {
        ensureKeepAwake();
      }
    }, { passive: true });

    // Single taps drive PC mouse; double-tap toggles iPhone fullscreen viewer.
    screen.addEventListener("dblclick", event => {
      if (!isIos() && !isMobileClient()) return;
      event.preventDefault();
      if (touchInputState && (touchInputState.dragging || touchInputState.longPressed)) return;
      toggleDisplayViewerFromScreen();
    });
    rtcScreen.addEventListener("dblclick", event => {
      if (!isIos() && !isMobileClient()) return;
      event.preventDefault();
      toggleDisplayViewerFromScreen();
    });
    window.addEventListener("beforeinstallprompt", event => {
      event.preventDefault();
      installPromptEvent = event;
      updateInstallState();
    });
    window.addEventListener("appinstalled", () => {
      installPromptEvent = null;
      updateInstallState();
    });
    document.addEventListener("keydown", event => {
      if (activeMode !== "quota") return;
      if (event.key === "ArrowLeft") {
        changeQuotaAccount(quotaActiveTab, -1);
      } else if (event.key === "ArrowRight") {
        changeQuotaAccount(quotaActiveTab, 1);
      }
    });
    window.addEventListener("resize", () => {
      updateViewportSize();
      applyRotation();
    });
    window.addEventListener("orientationchange", () => {
      setTimeout(() => {
        updateViewportSize();
        applyRotation();
      }, 120);
    });
    if (window.visualViewport) {
      window.visualViewport.addEventListener("resize", () => {
        updateViewportSize();
        applyRotation();
      });
    }
    async function boot() {
      const deckWindow = isDeckWindow();
      document.body.classList.toggle("deck-window", deckWindow);

      // iPhone: one path only — HTTPS. Block HTTP UI entirely (except loopback).
      if (!deckWindow && enforceIosHttpsPath()) {
        try {
          await loadConnectInfo();
        } catch {
        }
        return;
      }

      applyClientChrome();
      rotation.value = localStorage.getItem("phoneMonitorRotation") || "auto";
      orientation.value = localStorage.getItem("phoneMonitorOrientation") || "auto";
      if (isEinkClient()) {
        fullscreen.textContent = "全螢幕面板";
      }
      streamPreset.value = localStorage.getItem("phoneMonitorStreamPreset") || defaultStreamPreset();
      applyStreamPresetFields();
      streamFps.value = localStorage.getItem("phoneMonitorStreamFps") || streamFps.value;
      streamQuality.value = localStorage.getItem("phoneMonitorStreamQuality") || streamQuality.value;
      updateViewportSize();
      applyRotation();
      applyOrientation();
      setSideSkin(localStorage.getItem("phoneMonitorSideSkin") || "command");
      if (deckWindow) {
        document.title = "VibeDeck Deck";
        setMode(getInitialMode() === "quota" ? "quota" : "sideboard");
        connectDashboardEvents();
        sideboardTimer = setInterval(() => scheduleDashboardRefresh("sideboard"), 60000);
        quotaTimer = setInterval(() => scheduleDashboardRefresh("quota"), 120000);
        return;
      }

      describeClient();
      registerPhoneAppShell();
      // Re-hydrate token from cookie/session if localStorage was empty (iOS Home Screen cases).
      const again = loadStoredDeviceCredentials();
      if (!deviceToken && again.token) {
        persistDeviceCredentials(again.token, again.id);
      }
      notifyNativeDeviceTrust();
      applyClientChrome();
      await completeDevicePairingFromHash().catch(error => {
        setTrustState(error.message || "配對失敗。", false);
        showPairSuccessBanner(`配對失敗：${error.message || "未知錯誤"}。可把 PC 配對連結貼到下方重試。`);
        const banner = document.getElementById("pairSuccessBanner");
        if (banner) {
          banner.style.borderColor = "rgba(255, 120, 120, .5)";
          banner.style.background = "rgba(48, 16, 16, .94)";
        }
      });
      await loadDeviceTrustStatus();
      applyClientChrome();
      if (deviceTrusted && isIos() && !isStandaloneApp()) {
        showPairSuccessBanner("已配對（HTTPS）。同一頁分享 → 加入主畫面，再點「長亮 ON」。");
      }
      setMode(getInitialMode());
      if (shouldStartInViewer() || (isIos() && deviceTrusted && getInitialMode() === "display" && isStandaloneApp())) {
        setTimeout(() => enterLandscapeViewer(), 350);
      }
      connectDashboardEvents();
      sideboardTimer = setInterval(() => scheduleDashboardRefresh("sideboard"), 60000);
      quotaTimer = setInterval(() => scheduleDashboardRefresh("quota"), 120000);
      await loadModePresets().catch(error => {
        setStatus(error.message || "解析度預設讀取失敗", false);
      });
      loadConnectInfo();
      loadStreamCapabilities();
      if (!isEinkClient()) {
        loadPhoneDisplay().then(loadDisplayStatus).finally(connectVideo);
        connectInput();
      }
      if (isIos()) {
        fullscreen.textContent = "全螢幕副螢幕";
      }
      updateKeepAwakeButton();
      updateWakeCapability();
      startKeepAwakeWatch();
      // BOOX browsers may be served over LAN HTTP and still need the silent
      // video fallback. Native Android shells use their own OS WakeLock.
      if (keepAwakeDesired && (isEinkClient() || (location.protocol === "https:" && (isIos() || isMobileClient() || deviceTrusted)))) {
        ensureKeepAwake();
        setTimeout(() => ensureKeepAwake(), 800);
      }
    }

    boot();
