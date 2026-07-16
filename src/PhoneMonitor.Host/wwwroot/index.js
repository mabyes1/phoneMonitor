import {
  averagePercent,
  describeWeatherCode,
  formatFileSize,
  formatGb,
  formatMbps,
  formatPercent,
  formatSeconds,
  formatTemperature,
  formatWeatherLocation,
} from "./modules/formatters.js?v=47";
import { createDisplayInputController } from "./modules/display-input.js?v=47";
import { createCustomCardsController } from "./modules/custom-cards.js?v=52";
import { createActivityFeedController } from "./modules/activity-feed.js?v=53";
import { createDashboardLayoutController } from "./modules/dashboard-layout.js?v=55";
import { createQuotaController } from "./modules/quota-controller.js?v=49";
import { createQuotaMiniCardController } from "./modules/quota-mini-card.js?v=53";
import { createSideboardController } from "./modules/sideboard.js?v=49";
import { createMobileOverviewController } from "./modules/mobile-overview.js?v=1";
import { createStreamController } from "./modules/stream-controller.js?v=47";
import { tuneVideoReceiver } from "./modules/stream-tuning.js?v=47";
import {
  escapeHtml,
  extractQuotaEmail,
  extractQuotaTier,
  normalizeTierLabel,
  renderQuotaWindow,
  summarizeQuotaWindow,
} from "./modules/quota-formatters.js?v=48";

    const screen = document.getElementById("screen");
    const statusText = document.getElementById("status");
    const dot = document.getElementById("dot");
    const rotation = document.getElementById("rotation");
    const orientation = document.getElementById("orientation");
    let einkPhysicalOrientation = "unknown";
    let einkOrientationCandidate = "";
    let einkOrientationCandidateSince = 0;
    const displayMode = document.getElementById("displayMode");
    const sideboardMode = document.getElementById("sideboardMode");
    const quotaMode = document.getElementById("quotaMode");
    const refresh = document.getElementById("refresh");
    const fullscreen = document.getElementById("fullscreen");
    const displaySettingsToggle = document.getElementById("displaySettingsToggle");
    const displayEmptyState = document.getElementById("displayEmptyState");
    const displayEmptyTitle = document.getElementById("displayEmptyTitle");
    const displayEmptyMessage = document.getElementById("displayEmptyMessage");
    const installVirtualDisplay = document.getElementById("installVirtualDisplay");
    const displayInstallDetail = document.getElementById("displayInstallDetail");
    const openSideboardFromEmpty = document.getElementById("openSideboardFromEmpty");
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
    const pairPhone = document.getElementById("pairPhone");
    const phonePairRequest = document.getElementById("phonePairRequest");
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
    const appState = document.getElementById("appState");
    const deviceState = document.getElementById("deviceState");
    const displayView = document.getElementById("displayView");
    const sideboardView = document.getElementById("sideboardView");
    const quotaView = document.getElementById("quotaView");
    const sideboardShell = document.getElementById("sideboardShell");
    const systemSideboardPage = document.getElementById("systemSideboardPage");
    const customSideboardPage = document.getElementById("customSideboardPage");
    const sideboardPageTabs = document.getElementById("sideboardPageTabs");
    const customCardsGrid = document.getElementById("customCardsGrid");
    const customCardsStatus = document.getElementById("customCardsStatus");
    const customRefreshCards = document.getElementById("customRefreshCards");
    const customSettingsButton = document.getElementById("customSettingsButton");
    const customCardSettingsPanel = document.getElementById("customCardSettingsPanel");
    const customSettingsClose = document.getElementById("customSettingsClose");
    const customCardSettingsForm = document.getElementById("customCardSettingsForm");
    const customSettingsCard = document.getElementById("customSettingsCard");
    const customSettingsMaxItems = document.getElementById("customSettingsMaxItems");
    const customSettingsStreamEnabled = document.getElementById("customSettingsStreamEnabled");
    const customSettingsStreamDelay = document.getElementById("customSettingsStreamDelay");
    const customSettingsHint = document.getElementById("customSettingsHint");
    const customSettingsSave = document.getElementById("customSettingsSave");
    const customSettingsClear = document.getElementById("customSettingsClear");
    const customManageButton = document.getElementById("customManageButton");
    const customSourcesManager = document.getElementById("customSourcesManager");
    const customSourceList = document.getElementById("customSourceList");
    const customSourceForm = document.getElementById("customSourceForm");
    const customSourceFormTitle = document.getElementById("customSourceFormTitle");
    const customSourceKey = document.getElementById("customSourceKey");
    const customSourceDisplayName = document.getElementById("customSourceDisplayName");
    const customCardType = document.getElementById("customCardType");
    const customCardTitle = document.getElementById("customCardTitle");
    const customCardPosition = document.getElementById("customCardPosition");
    const customStaleAfter = document.getElementById("customStaleAfter");
    const customDefaultTtl = document.getElementById("customDefaultTtl");
    const customMaxItems = document.getElementById("customMaxItems");
    const customSourceFormSubmit = document.getElementById("customSourceFormSubmit");
    const customSourceCancel = document.getElementById("customSourceCancel");
    const customCredentialPanel = document.getElementById("customCredentialPanel");
    const customCredentialText = document.getElementById("customCredentialText");
    const customCredentialCopy = document.getElementById("customCredentialCopy");
    const customCredentialClose = document.getElementById("customCredentialClose");
    const customAddSource = document.getElementById("customAddSource");
    const customManagerAdd = document.getElementById("customManagerAdd");
    const customManagerClose = document.getElementById("customManagerClose");
    const windowsNotificationControl = document.getElementById("windowsNotificationControl");
    const windowsNotificationStatus = document.getElementById("windowsNotificationStatus");
    const windowsNotificationMessage = document.getElementById("windowsNotificationMessage");
    const windowsNotificationEnable = document.getElementById("windowsNotificationEnable");
    const windowsNotificationDisable = document.getElementById("windowsNotificationDisable");
    const rtcScreen = document.getElementById("rtcScreen");
    const sideHeadline = document.getElementById("sideHeadline");
    const sideSummary = document.getElementById("sideSummary");
    const sideError = document.getElementById("sideError");
    const sideLoad = document.getElementById("sideLoad");
    const sideLoadNormal = document.getElementById("sideLoadNormal");
    const sideLoadStatus = document.getElementById("sideLoadStatus");
    const sideLoadStatusReason = document.getElementById("sideLoadStatusReason");
    const sideLoadAlert = document.getElementById("sideLoadAlert");
    const sideLoadAlertTitle = document.getElementById("sideLoadAlertTitle");
    const sideLoadAlertReason = document.getElementById("sideLoadAlertReason");
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
    const activityFeedCard = document.getElementById("activityFeedCard");
    const activityFeedList = document.getElementById("activityFeedList");
    const activityFeedFilters = [...document.querySelectorAll("[data-activity-filter]")];
    const quotaMiniSource = document.getElementById("quotaMiniSource");
    const quotaMiniValue = document.getElementById("quotaMiniValue");
    const quotaMiniBar = document.getElementById("quotaMiniBar");
    const quotaMiniReset = document.getElementById("quotaMiniReset");
    const quotaMiniState = document.getElementById("quotaMiniState");
    const quotaSummary = document.getElementById("quotaSummary");
    const quotaUpdated = document.getElementById("quotaUpdated");
    const quotaTabs = document.getElementById("quotaTabs");
    const quotaHelp = document.getElementById("quotaHelp");
    const quotaGrid = document.getElementById("quotaGrid");
    const hostAuthGate = document.getElementById("hostAuthGate");
    const hostAuthForm = document.getElementById("hostAuthForm");
    const hostAuthPassword = document.getElementById("hostAuthPassword");
    const hostAuthSubmit = document.getElementById("hostAuthSubmit");
    const hostAuthError = document.getElementById("hostAuthError");
    const wsBase = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}`;
    let selectedDisplayName = "";
    let lastUrl = null;
    let inputSocket = null;
    let streamController = null;
    let wakeLock = null;
    let keepAwakeDesired = localStorage.getItem("phoneMonitorKeepAwake") !== "0";
    let keepAwakeVideoPlaying = false;
    let keepAwakeWatchTimer = null;
    let streamStats = null;
    let activeMode = "display";
    let dashboardConnectionState = "connecting";
    let sideboardTimer = null;
    let quotaTimer = null;
    let customCardsTimer = null;
    let activityNotificationsTimer = null;
    let dashboardConnectionTimer = null;
    let displayInstallTimer = null;
    let deviceStatusTimer = null;
    let deviceStatusInterval = 0;
    let dashboardEvents = null;
    const dashboardRefreshState = {
      sideboard: { last: 0, timer: null, dirty: false },
      quota: { last: 0, timer: null, dirty: false },
      customCards: { last: 0, timer: null, dirty: false }
    };
    let customCardsController = null;
    let quotaMiniController = null;
    let actionToken = "";
    let actionHeaderName = "X-PhoneMonitor-Action-Token";
    let hostAuthEnabled = false;
    let hostAuthenticated = false;
    let hostAuthRequired = false;
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
    }

    const storedDevice = loadStoredDeviceCredentials();
    let deviceToken = storedDevice.token;
    let deviceId = storedDevice.id;
    let deviceHeaderName = "X-PhoneMonitor-Device-Token";
    let deviceTrusted = false;
    let deviceLocalRequest = false;
    let pairingQrActive = false;
    let quotaSnapshotData = null;
    // Keep the initial quota view consistent across phone and E Ink clients.
    // Users can still switch tabs; the versioned key prevents an old device
    // preference from making one client open AGY while another opens Codex.
    const QUOTA_TAB_STORAGE_KEY = "phoneMonitorQuotaTab.v2";
    let quotaActiveTab = localStorage.getItem(QUOTA_TAB_STORAGE_KEY) || "codex";
    const quotaAccountIndexByTab = {};
    const quotaActionStatusByKey = new Map();
    let quotaSwipeStartX = null;
    let installPromptEvent = null;
    let approvalPollTimer = null;
    let pendingApprovalTimer = null;
    const touchLongPressMs = 460;
    const touchDragThresholdPx = 12;

    function isMobileClient() {
      return isIos() || /Android|Mobile|webOS|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent || "");
    }

    const EINK_PREF_KEY = "phoneMonitorEink";

    function readEinkQuery() {
      const requested = new URLSearchParams(location.search).get("eink");
      if (requested === "1" || requested === "true") return true;
      if (requested === "0" || requested === "false") return false;
      return null;
    }

    function readEinkCookie() {
      try {
        const match = document.cookie.match(/(?:^|;\s*)phoneMonitorEink=([01])/);
        return match ? match[1] : null;
      } catch {
        return null;
      }
    }

    function writeEinkPreference(value) {
      const flag = value ? "1" : "0";
      try {
        localStorage.setItem(EINK_PREF_KEY, flag);
      } catch {
        // ignore
      }
      try {
        // Cookie backup: some BOOX "Add to Home Screen" WebAPK paths keep cookies
        // more reliably than a fresh localStorage partition on first launch.
        document.cookie = `${EINK_PREF_KEY}=${flag}; Path=/; Max-Age=31536000; SameSite=Lax`;
      } catch {
        // ignore
      }
    }

    function looksLikeBooxScreen() {
      // Product path is browser/PWA. NeoBrowser on Go Color often sends a generic
      // Android Chrome UA with no BOOX/ONYX token, so fall back to known panel size.
      try {
        const widths = [
          screen.width || 0,
          screen.height || 0,
          window.innerWidth || 0,
          window.innerHeight || 0,
          document.documentElement?.clientWidth || 0,
          document.documentElement?.clientHeight || 0
        ];
        const w = Math.max(...widths);
        const h = Math.min(...widths.filter(v => v > 0));
        // BOOX Go Color 7: 1680 × 1264 (allow CSS-pixel / density variance)
        if (w >= 1180 && w <= 1900 && h >= 980 && h <= 1500 && (w / Math.max(h, 1)) >= 1.15) {
          return true;
        }
      } catch {
        // ignore
      }
      return false;
    }

    function detectEinkHardware() {
      const ua = navigator.userAgent || "";
      if (/VibeDeck-EInk|BOOX|ONYX|Onyx|eInk|E-Ink|E Ink/i.test(ua)) return true;
      // Generic Android Chrome on a known BOOX panel still wants the paper layout.
      if (/Android/i.test(ua) && looksLikeBooxScreen()) return true;
      return false;
    }

    function isEinkClient() {
      const query = readEinkQuery();
      if (query !== null) return query;

      try {
        const stored = localStorage.getItem(EINK_PREF_KEY);
        if (stored === "1") return true;
        if (stored === "0") return false;
      } catch {
        // ignore
      }

      const cookie = readEinkCookie();
      if (cookie === "1") return true;
      if (cookie === "0") return false;

      return detectEinkHardware();
    }

    function ensureEinkPreferenceSticky() {
      // Once we know this device wants e-ink (hardware or explicit query), persist
      // so PWA home-screen launches (which drop ?eink=1) still open paper mode.
      const query = readEinkQuery();
      if (query === false) {
        writeEinkPreference(false);
        return;
      }
      if (query === true || detectEinkHardware()) {
        try {
          if (localStorage.getItem(EINK_PREF_KEY) !== "0" && readEinkCookie() !== "0") {
            writeEinkPreference(true);
          }
        } catch {
          writeEinkPreference(true);
        }
      }
    }

    function setEinkClient(enabled, { persist = true, syncUrl = true } = {}) {
      if (persist) {
        writeEinkPreference(enabled);
      }

      if (syncUrl) {
        try {
          const url = new URL(location.href);
          if (enabled) url.searchParams.set("eink", "1");
          else url.searchParams.delete("eink");
          history.replaceState({}, "", url.pathname + url.search + url.hash);
        } catch {
          // ignore
        }
      }

      applyClientChrome();
      if (enabled) {
        // E-ink product default is sideboard, not display stream.
        try {
          if (typeof setMode === "function") setMode("sideboard");
        } catch {
          // view switch may not be ready during early boot
        }
      }
      updateEinkToggle();
    }

    function updateEinkToggle() {
      const button = document.getElementById("einkModeToggle");
      if (!button) return;
      const on = isEinkClient();
      button.classList.toggle("active", on);
      button.setAttribute("aria-pressed", on ? "true" : "false");
      button.textContent = on ? "電子書 ON" : "電子書";
      button.title = on
        ? "目前是電子紙版面（資訊板優先、高對比）。再按可切回一般手機版。"
        : "切換成電子紙版面（BOOX / 電子書）。也可在網址加 ?eink=1。";
    }

    function dashboardMinInterval(topic) {
      if (topic === "sideboard") return isEinkClient() ? 8000 : 1000;
      if (topic === "customCards") return isEinkClient() ? 8000 : 250;
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
        else if (topic === "customCards") await customCardsController?.refresh();
        else if (activeMode === "sideboard") await quotaMiniController?.refresh();
        else await refreshQuotas();
        if (state.dirty) scheduleDashboardRefresh(topic);
      }, delay);
    }

    function connectDashboardEvents() {
      if (dashboardEvents || typeof EventSource === "undefined") return;
      dashboardEvents = new EventSource("/api/dashboard/events");
      dashboardEvents.onopen = () => {
        const topic = activeMode === "quota" ? "quota" : "sideboard";
        scheduleDashboardRefresh(topic, true);
      };
      dashboardEvents.addEventListener("sideboard", () => scheduleDashboardRefresh("sideboard"));
      dashboardEvents.addEventListener("quota", () => scheduleDashboardRefresh("quota"));
      dashboardEvents.addEventListener("custom-card", () => scheduleDashboardRefresh("customCards"));
      dashboardEvents.addEventListener("sync", () => {
        scheduleDashboardRefresh("sideboard");
        scheduleDashboardRefresh("quota");
        scheduleDashboardRefresh("customCards");
      });
      dashboardEvents.onerror = () => {
        // EventSource reconnects automatically. Low-frequency fallback timers remain active.
      };
    }

    function defaultStreamPreset() {
      // iPhone Safari JPEG is heavier; prefer battery/balanced over 60fps.
      if (isIos()) return "battery";
      return isMobileClient() ? "balanced" : "smooth";
    }

    function applyClientChrome() {
      const localConsole = Boolean(deviceLocalRequest);
      const phoneClient = !localConsole && isMobileClient();
      const ios = isIos();
      const eink = isEinkClient();

      document.body.classList.toggle("eink-client", eink);
      document.body.classList.toggle("phone-client", phoneClient);
      document.body.classList.toggle("ios-client", ios && !localConsole);
      document.body.classList.toggle("device-trusted", Boolean(deviceTrusted) && !localConsole);
      document.body.classList.toggle("pc-console", localConsole);
      document.body.classList.toggle("standalone-app", isStandaloneApp());
      customCardsController?.syncAccess?.();
      applyForcedLandscape();
      updateIosHomeTip();
      updateEinkToggle();
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
        (isMobileClient() || document.body.classList.contains("phone-client"));
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

    function getRawViewportSize() {
      const viewport = window.visualViewport;
      return {
        width: viewport ? viewport.width : window.innerWidth,
        height: viewport ? viewport.height : window.innerHeight,
      };
    }

    function syncEinkSensorOrientationClasses(width, height) {
      const forcePortrait = isEinkClient() &&
        orientation?.value === "auto" &&
        einkPhysicalOrientation.startsWith("portrait-") &&
        width > height;
      const primary = forcePortrait && einkPhysicalOrientation === "portrait-primary";
      const secondary = forcePortrait && einkPhysicalOrientation === "portrait-secondary";
      for (const root of [document.documentElement, document.body]) {
        root.classList.toggle("eink-sensor-portrait", forcePortrait);
        root.classList.toggle("eink-sensor-portrait-primary", primary);
        root.classList.toggle("eink-sensor-portrait-secondary", secondary);
      }
      return forcePortrait;
    }

    function setEinkPhysicalOrientation(value) {
      if (einkPhysicalOrientation === value) return;
      einkPhysicalOrientation = value;
      document.documentElement.dataset.einkPhysicalOrientation = value;
      updateViewportSize();
    }

    function handleEinkDeviceMotion(event) {
      if (!isEinkClient() || orientation?.value !== "auto") return;
      const gravity = event.accelerationIncludingGravity;
      const x = Number(gravity?.x);
      const y = Number(gravity?.y);
      if (!Number.isFinite(x) || !Number.isFinite(y) || Math.max(Math.abs(x), Math.abs(y)) < 3) return;

      let candidate = "";
      if (Math.abs(y) > Math.abs(x) * 1.2) {
        candidate = y >= 0 ? "portrait-primary" : "portrait-secondary";
      } else if (Math.abs(x) > Math.abs(y) * 1.2) {
        candidate = x >= 0 ? "landscape-primary" : "landscape-secondary";
      }
      if (!candidate) return;

      const now = performance.now();
      if (candidate !== einkOrientationCandidate) {
        einkOrientationCandidate = candidate;
        einkOrientationCandidateSince = now;
        return;
      }
      // E-ink readers report noisy gravity while being moved. Wait until the
      // device has settled before changing the whole dashboard canvas.
      if (now - einkOrientationCandidateSince >= 400) setEinkPhysicalOrientation(candidate);
    }

    function isIos() {
      return /iPad|iPhone|iPod/.test(navigator.userAgent) ||
        (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
    }

    function isIphone() {
      return /iPhone|iPod/.test(navigator.userAgent || "");
    }

    function prefersWebRtcDisplay() {
      return isMobileClient() || new URLSearchParams(location.search).get("webrtc") === "1";
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
      const rawViewport = getRawViewportSize();
      let width = rawViewport.width;
      let height = rawViewport.height;
      const forceLandscape = shouldForceLandscape() && height >= width;
      const forceEinkPortrait = syncEinkSensorOrientationClasses(width, height);

      // When CSS rotates portrait → landscape, layout metrics are swapped.
      if (forceLandscape) {
        const swapped = width;
        width = height;
        height = swapped;
      }

      // A stale installed BOOX PWA can keep Chrome's Activity locked to
      // sensor-landscape. CSS rotates that canvas, so expose portrait metrics
      // to the dashboard layout while the fallback is active.
      if (forceEinkPortrait) {
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
      if (!isIos() || isLoopbackHost()) {
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
      // A leftover "paired" toast must never sit on top of the display stream.
      if (!isSideboard && !isQuota) hidePairSuccessBanner();
      document.body.classList.toggle("mode-display", !isSideboard && !isQuota);
      document.body.classList.toggle("mode-sideboard", isSideboard);
      document.body.classList.toggle("mode-quota", isQuota);
      if (isSideboard || isQuota) {
        document.body.classList.remove("display-settings-open");
        displaySettingsToggle?.setAttribute("aria-expanded", "false");
        if (displaySettingsToggle) displaySettingsToggle.textContent = "顯示設定";
      }
      displayView.classList.toggle("active", !isSideboard && !isQuota);
      sideboardView.classList.toggle("active", isSideboard);
      quotaView.classList.toggle("active", isQuota);
      displayMode.classList.toggle("active", !isSideboard && !isQuota);
      sideboardMode.classList.toggle("active", isSideboard);
      quotaMode.classList.toggle("active", isQuota);
      document.querySelectorAll("[data-dashboard-mode]").forEach(button => {
        const active = button.dataset.dashboardMode === mode;
        button.classList.toggle("active", active);
        button.setAttribute("aria-pressed", active ? "true" : "false");
      });
      localStorage.setItem("phoneMonitorViewMode", mode);

      if (isSideboard) {
        scheduleDashboardRefresh("sideboard", true);
        scheduleDashboardRefresh("customCards", true);
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

    function setBar(element, value) {
      const percent = Number.isFinite(value) ? Math.max(0, Math.min(100, value)) : 0;
      element.style.width = `${percent}%`;
    }

    function setText(element, value) {
      element.textContent = value == null || value === "" ? "--" : String(value);
    }

    function updateHostAuthGate(message = "") {
      if (!hostAuthGate) return;
      const visible = hostAuthRequired && !hostAuthenticated;
      hostAuthGate.hidden = !visible;
      if (hostAuthError && message) hostAuthError.textContent = message;
      if (visible) {
        document.body.classList.add("host-auth-required");
        setTimeout(() => hostAuthPassword?.focus(), 0);
      } else {
        document.body.classList.remove("host-auth-required");
      }
    }

    function parseJsonResponse(text, label) {
      const raw = (text || "").replace(/^\uFEFF/, "").trim();
      if (!raw) return {};
      try {
        return JSON.parse(raw);
      } catch (error) {
        const preview = raw.slice(0, 80).replace(/\s+/g, " ");
        const looksHtml = /^<!doctype|^<html/i.test(raw);
        throw new Error(
          looksHtml
            ? `${label || "API"} 回了 HTML 而不是 JSON（多半是離線頁或連不到 Host）。請確認 PC Host 有開、iPhone 用 https://<PC-IP>:5443。`
            : `${label || "API"} JSON 解析失敗：${preview || error.message}`
        );
      }
    }

    async function loadHostAuthStatus() {
      const response = await fetch("/api/auth/status", { cache: "no-store" });
      const result = parseJsonResponse(await response.text(), "/api/auth/status");
      hostAuthEnabled = Boolean(result.enabled ?? result.Enabled);
      hostAuthenticated = Boolean(result.authenticated ?? result.Authenticated);
      hostAuthRequired = Boolean(result.required ?? result.Required);
      updateHostAuthGate(
        Boolean(result.httpsRequired ?? result.HttpsRequired)
          ? "遠端登入必須改用 HTTPS。"
          : ""
      );
      return result;
    }

    async function loginToHost(password) {
      hostAuthSubmit.disabled = true;
      hostAuthSubmit.textContent = "登入中…";
      hostAuthError.textContent = "";
      try {
        const response = await fetch("/api/auth/login", {
          method: "POST",
          cache: "no-store",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ password })
        });
        const result = parseJsonResponse(await response.text(), "/api/auth/login");
        if (!response.ok || !result.success) {
          throw new Error(result.error || result.message || `登入失敗 HTTP ${response.status}`);
        }

        actionToken = "";
        await loadHostAuthStatus();
        location.reload();
      } catch (error) {
        hostAuthError.textContent = error.message || "登入失敗。";
        updateHostAuthGate();
      } finally {
        hostAuthSubmit.disabled = false;
        hostAuthSubmit.textContent = "登入";
      }
    }

    function canUseProtectedConnection() {
      return deviceTrusted || deviceLocalRequest || hostAuthenticated;
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
        qrCaption.textContent = "手機相機掃碼 → 開啟 HTTPS Host → 按「配對申請」。";
      } else if (normalized === "pair") {
        qrCaption.textContent = "手機先掃描 QR Code 開啟 HTTPS Host，再按「配對申請」。";
      } else {
        qrCaption.textContent = "手機瀏覽器：需要時先安裝 HTTPS 憑證，再進行配對。";
      }
    }

    function showInstallQr() {
      pairingQrActive = false;
      setPairingUiActive(false);
      if (qrCode) {
        if (qrCode.dataset.blobUrl) {
          URL.revokeObjectURL(qrCode.dataset.blobUrl);
          delete qrCode.dataset.blobUrl;
        }
        qrCode.src = `/qr.svg?t=${Date.now()}`;
      }
      updatePairingGuide("pair");
      return true;
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

    function syncDeviceStatusPolling() {
      const nextInterval = deviceLocalRequest ? 3000 : deviceTrusted ? 10000 : 0;
      if (nextInterval === deviceStatusInterval) return;
      if (deviceStatusTimer) clearInterval(deviceStatusTimer);
      deviceStatusTimer = null;
      deviceStatusInterval = nextInterval;
      if (nextInterval > 0) {
        deviceStatusTimer = setInterval(() => loadDeviceTrustStatus(), nextInterval);
      }
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
        empty.textContent = "尚未配對裝置。";
        trustedDeviceList.append(empty);
        return;
      }

      for (const device of devices) {
        const id = readDeviceField(device, "DeviceId", "deviceId");
        const name = readDeviceField(device, "Name", "name") || "Phone";
        const lastSeen = readDeviceField(device, "LastSeenAt", "lastSeenAt");
        const remote = readDeviceField(device, "LastRemoteAddress", "lastRemoteAddress") || "local";
        const connected = Boolean(device?.Connected ?? device?.connected);
        const row = document.createElement("div");
        row.className = `trusted-device-row${connected ? " is-connected" : ""}`;
        row.innerHTML = `
          <div class="trusted-device-main">
            <div class="trusted-device-name-row"><strong></strong><b class="trusted-device-status"></b></div>
            <span></span>
          </div>
          <button type="button" data-device-revoke="">移除</button>
        `;
        row.querySelector("strong").textContent = name;
        row.querySelector(".trusted-device-status").textContent = connected ? "連線中" : "未連線";
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

    async function requestApprovalPairing(options = {}) {
      if (deviceTrusted || deviceLocalRequest || hostAuthenticated || approvalPollTimer) return;
      const allowCreate = options.allowCreate === true;
      const storageKey = `phoneMonitorPendingApproval:${location.host}`;
      let pending = null;
      try { pending = JSON.parse(localStorage.getItem(storageKey) || "null"); } catch { }

      if (!pending?.requestId || !pending?.requestSecret) {
        if (!allowCreate) {
          setTrustState("請按「配對申請」，再回 PC 按允許。", false);
          updatePairingGuide("pair");
          return false;
        }
        const response = await fetch("/api/devices/pairing/request", {
          method: "POST", cache: "no-store", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ name: pairingDeviceName(), platform: pairingPlatform() })
        });
        const result = parseJsonResponse(await response.text(), "/api/devices/pairing/request");
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
          const result = parseJsonResponse(await response.text(), "/api/devices/pairing/poll");
          const status = result.Status || result.status;
          if (response.ok && status === "approved") {
            persistDeviceCredentials(result.DeviceToken || result.deviceToken, result.DeviceId || result.deviceId);
            localStorage.removeItem(storageKey);
            clearInterval(approvalPollTimer);
            approvalPollTimer = null;
            const trust = await loadDeviceTrustStatus();
            const trusted = Boolean(trust?.Trusted ?? trust?.trusted);
            if (!trusted) {
              setTrustState("配對 token 已取得，正在重新載入驗證。", true);
            }
            showPairSuccessBanner(`裝置：${result.DeviceName || result.deviceName || pairingDeviceName()}`);
            // Reload from the Host after approval so every controller starts
            // from the newly trusted state. The cache-busting query also
            // escapes stale installed-PWA module caches.
            setTimeout(() => {
              const url = new URL(location.href);
              url.searchParams.set("paired", Date.now().toString());
              location.replace(url.toString());
            }, 250);
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
      return true;
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
      if (!window.confirm("要移除這個已配對裝置嗎？")) return;

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
      if (!window.confirm("要清空所有已配對裝置嗎？目前裝置會需要重新配對。")) return;

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
        setTrustState(result.Message || result.message || "已清空配對裝置。", true);
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

    function showPairSuccessBanner(message) {
      const banner = document.getElementById("pairSuccessBanner");
      if (!banner) return;
      const title = document.createElement("strong");
      title.textContent = "已配對成功";
      const detail = document.createElement("div");
      detail.textContent = message || "";
      const homeHint = document.createElement("div");
      if (isIos() && !isStandaloneApp()) {
        homeHint.append("接著在");
        const pairedPage = document.createElement("strong");
        pairedPage.textContent = "這個已配對頁面";
        const addToHome = document.createElement("strong");
        addToHome.textContent = "加入主畫面";
        homeHint.append(pairedPage, "按 Safari 底部分享 → ", addToHome, "。不要另開新分頁再加。");
      } else {
        homeHint.textContent = "可點上方「顯示器 / 資訊板 / 額度」使用。";
      }
      banner.replaceChildren(title, detail, homeHint);
      banner.classList.add("show");
      // The toast is confirmation, not a permanent panel. On phones it used to
      // stay sticky at z-40 and cover the secondary-screen view forever.
      clearTimeout(showPairSuccessBanner._timer);
      showPairSuccessBanner._timer = setTimeout(hidePairSuccessBanner, 6000);
    }

    function hidePairSuccessBanner() {
      const banner = document.getElementById("pairSuccessBanner");
      if (banner) banner.classList.remove("show");
    }

    function setPairControlsVisible(localConsole) {
      if (pairPhone) {
        pairPhone.hidden = !localConsole;
        if (localConsole) pairPhone.removeAttribute("hidden");
        else pairPhone.setAttribute("hidden", "");
        pairPhone.textContent = "配對新手機";
        pairPhone.disabled = false;
      }
      if (launchDeckWindow) {
        launchDeckWindow.hidden = !localConsole;
        if (localConsole) launchDeckWindow.removeAttribute("hidden");
        else launchDeckWindow.setAttribute("hidden", "");
      }
    }

    async function loadDeviceTrustStatus() {
      try {
        const result = await fetchJsonOrThrow("/api/devices/status");
        deviceHeaderName = result.DeviceHeader || result.deviceHeader || deviceHeaderName;
        deviceTrusted = Boolean(result.Trusted ?? result.trusted);

        // localStorage can outlive a re-pair (especially for an iOS PWA), while
        // the cookie is refreshed by the approval response. If the two stores
        // disagree, the stale localStorage value used to make every request
        // look unpaired forever. Retry once with the cookie-backed credential.
        const cookieToken = readCookie(DEVICE_COOKIE);
        if (!deviceTrusted && deviceToken && cookieToken && cookieToken !== deviceToken) {
          persistDeviceCredentials(cookieToken, deviceId);
          return loadDeviceTrustStatus();
        }
        deviceLocalRequest = Boolean(result.LocalRequest ?? result.localRequest) || isLoopbackHost();
        const currentDevice = result.CurrentDevice || result.currentDevice;
        setPairControlsVisible(deviceLocalRequest);
        renderTrustedDevices(result);

        if (deviceLocalRequest) {
          setTrustState("信任狀態：本機控制。", true);
          if (!pairingQrActive) updatePairingGuide("pair");
          loadPendingApprovals();
          if (!pendingApprovalTimer) pendingApprovalTimer = setInterval(loadPendingApprovals, 2000);
        } else if (hostAuthenticated) {
          setTrustState("遠端 Host 登入成功。", true);
          updatePairingGuide("paired");
        } else if (deviceTrusted) {
          const name = currentDevice?.Name || currentDevice?.name || "已配對手機";
          setTrustState(`信任狀態：${name} 已配對。`, true);
          updatePairingGuide("paired");
        } else if (hostAuthRequired && !hostAuthenticated) {
          setTrustState("請先登入 Host。", false);
          updatePairingGuide("install");
        } else {
          const storageKey = `phoneMonitorPendingApproval:${location.host}`;
          let hasPendingRequest = false;
          try {
            const pending = JSON.parse(localStorage.getItem(storageKey) || "null");
            hasPendingRequest = Boolean(pending?.requestId && pending?.requestSecret);
          } catch { }
          if (hasPendingRequest) {
            requestApprovalPairing().catch(error => setTrustState(error.message || "配對申請失敗。", false));
          } else {
            setTrustState("請按「配對申請」，再回 PC 按允許。", false);
            updatePairingGuide("pair");
          }
        }

        applyClientChrome();
        syncDeviceStatusPolling();
        return result;
      } catch (error) {
        // A transient /api/devices/status failure must not drop a phone that is
        // already paired: forcing deviceTrusted=false here flashed the pairing
        // rescue panel back over the live secondary-screen view on iOS blips.
        // Keep the last known trust; only a successful response changes it.
        deviceLocalRequest = deviceLocalRequest || isLoopbackHost();
        setPairControlsVisible(deviceLocalRequest);
        if (!deviceTrusted) renderTrustedDevices(null);
        setTrustState(error.message || "信任狀態暫時無法取得，沿用上次狀態。", deviceTrusted);
        applyClientChrome();
        syncDeviceStatusPolling();
        return null;
      }
    }

    function setPairingUiActive(active) {
      document.body.classList.toggle("pairing-active", Boolean(active));
      // PC 本機預設藏 QR 區；配對時強制打開進階連線區顯示連結
      const links = document.querySelector(".connect-links");
      if (links && active) {
        links.open = true;
        links.hidden = false;
        links.removeAttribute("hidden");
      }
    }

    async function startPhonePairing() {
      if (pairPhone) {
        pairPhone.disabled = true;
        pairPhone.textContent = "正在準備…";
      }
      setPairingUiActive(true);
      try {
        showInstallQr();
        setPairingUiActive(true);
        setTrustState("請用手機掃描 QR Code 開啟 HTTPS Host，再按「配對申請」。", true);
        setStatus("手機 QR Code 已顯示", true);
        updatePairingGuide("scan");
      } finally {
        if (pairPhone) {
          pairPhone.disabled = false;
          pairPhone.textContent = "配對新手機";
        }
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
        setAppState(result.Message || result.message || "資訊板已開到副螢幕。", true);
      } catch (error) {
        setAppState(error.message || "無法把資訊板開到副螢幕。", false);
      } finally {
        launchDeckWindow.disabled = false;
        launchDeckWindow.textContent = originalText;
      }
    }

    async function ensureActionToken() {
      if (actionToken) return actionToken;
      const response = await fetch("/api/session", { cache: "no-store" });
      const data = parseJsonResponse(await response.text(), "/api/session");
      if (!response.ok) {
        throw new Error(data.error || data.message || `取得 session 失敗 HTTP ${response.status}`);
      }
      actionToken = data.ActionToken || data.actionToken || "";
      actionHeaderName = data.ActionHeader || data.actionHeader || actionHeaderName;
      deviceHeaderName = data.DeviceHeader || data.deviceHeader || deviceHeaderName;
      if (!actionToken) throw new Error("session 沒有 action token。");
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

      let response;
      try {
        response = await fetch(url, { cache: "no-store", ...init, headers });
      } catch (error) {
        throw new Error(`連線失敗：${url}（${error.message || "network error"}）`);
      }
      const text = await response.text();
      const data = parseJsonResponse(text, url);
      if (response.status === 401) {
        await loadHostAuthStatus().catch(() => {});
      }
      if (response.status === 403 && retryOnTokenRefresh && method !== "GET" && method !== "HEAD") {
        actionToken = "";
        return fetchJsonOrThrow(url, init, false);
      }

      if (!response.ok) {
        const errorPayload = data.error && typeof data.error === "object" ? data.error : null;
        const error = new Error(
          errorPayload?.message ||
          data.Message ||
          data.message ||
          (typeof data.error === "string" ? data.error : null) ||
          `HTTP ${response.status}`
        );
        error.code = errorPayload?.code || "";
        error.status = response.status;
        throw error;
      }
      return data;
    }

    function isTrustRequiredError(error) {
      return (error?.status === 401 || error?.status === 403) &&
        /not paired|trust|token|login/i.test(error.message || "");
    }

    const activityFeedController = createActivityFeedController({
      elements: { card: activityFeedCard, list: activityFeedList, filters: activityFeedFilters },
    });
    // Keep the merged activity card independent from custom-card rendering.
    // This guarantees the feed receives the Windows payload even when the
    // custom-card page is hidden or its stream renderer is busy.
    const refreshActivityNotifications = async () => {
      try {
        const snapshot = await fetchJsonOrThrow("/api/custom-cards");
        const card = (snapshot?.cards || []).find(item => item.sourceKey === "windows-notifications") || null;
        activityFeedController.setWindowsNotifications(card);
      } catch (error) {
        if (!isTrustRequiredError(error)) console.debug("activity notifications refresh failed", error);
      }
    };
    quotaMiniController = createQuotaMiniCardController({
      elements: {
        select: quotaMiniSource,
        value: quotaMiniValue,
        bar: quotaMiniBar,
        reset: quotaMiniReset,
        state: quotaMiniState,
      },
      fetchJsonOrThrow,
    });

    const sideboardController = createSideboardController({
      elements: {
        sideHeadline,
        sideSummary,
        sideError,
        sideLoad,
        sideLoadNormal,
        sideLoadStatus,
        sideLoadStatusReason,
        sideLoadAlert,
        sideLoadAlertTitle,
        sideLoadAlertReason,
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
      },
      fetchJsonOrThrow,
      isTrustRequiredError,
      getActiveMode: () => activeMode,
      setText,
      setBar,
      formatters: {
        averagePercent,
        describeWeatherCode,
        formatGb,
        formatMbps,
        formatPercent,
        formatSeconds,
        formatTemperature,
        formatWeatherLocation,
      },
      onConnectionChange: state => setDashboardConnectionState(state),
      onWorkPulse: workPulse => activityFeedController.setWorkPulse(workPulse),
    });
    createMobileOverviewController();
    const refreshSideboard = () => Promise.all([
      sideboardController.refresh(),
      quotaMiniController.refresh(),
    ]);

    try {
      customCardsController = createCustomCardsController({
        elements: {
          customSideboardPage,
          systemSideboardPage,
          customPageTabs: sideboardPageTabs,
          customCardsGrid,
          customCardsStatus,
          customRefresh: customRefreshCards,
          customSettingsButton,
          customCardSettingsPanel,
          customSettingsClose,
          customCardSettingsForm,
          customSettingsCard,
          customSettingsMaxItems,
          customSettingsStreamEnabled,
          customSettingsStreamDelay,
          customSettingsHint,
          customSettingsSave,
          customSettingsClear,
          customManageButton,
          customSourcesManager,
          customSourceList,
          customSourceForm,
          customSourceFormTitle,
          customSourceKey,
          customSourceDisplayName,
          customCardType,
          customCardTitle,
          customCardPosition,
          customStaleAfter,
          customDefaultTtl,
          customMaxItems,
          customSourceFormSubmit,
          customSourceCancel,
          customCredentialPanel,
          customCredentialText,
          customCredentialCopy,
          customCredentialClose,
          customAddSource,
          customManagerAdd,
          customManagerClose,
          windowsNotificationControl,
          windowsNotificationStatus,
          windowsNotificationMessage,
          windowsNotificationEnable,
          windowsNotificationDisable,
        },
        fetchJsonOrThrow,
        getActiveMode: () => activeMode,
        isLocalConsole: () => Boolean(deviceLocalRequest),
        isTrustRequiredError,
        isEinkClient,
        onWindowsNotifications: card => activityFeedController.setWindowsNotifications(card),
      });
    } catch (error) {
      // 自訂卡片模組掛了也不能拖垮 iPhone 顯示器 / 配對主流程
      console.error("custom cards controller failed", error);
      customCardsController = null;
    }

    let dashboardLayoutController = null;
    try {
      const openDashboardConfig = action => {
        customCardsController?.setPage("custom", false);
        action?.();
      };
      const closeDashboardConfig = () => {
        customCardsController?.setSettingsPanelVisible(false);
        customCardsController?.setManagerVisible(false);
        customCardsController?.setPage("system", false);
      };
      dashboardLayoutController = createDashboardLayoutController({
        fetchJsonOrThrow,
        isEinkClient,
        openCardSettings: () => openDashboardConfig(() => customCardsController?.setSettingsPanelVisible(true)),
        openSourceManager: () => openDashboardConfig(() => customCardsController?.setManagerVisible(true)),
        openSourceForm: () => openDashboardConfig(() => customCardsController?.showForm()),
        closeConfig: closeDashboardConfig,
      });
    } catch (error) {
      console.error("dashboard layout controller failed", error);
    }

    const quotaController = createQuotaController({
      elements: { quotaSummary, quotaUpdated, quotaHelp, quotaGrid },
      getActiveMode: () => activeMode,
      fetchJsonOrThrow,
      isTrustRequiredError,
      renderSnapshot: renderQuotas,
      renderErrorHelp: (error, requiresTrust) => renderQuotaHelpBlock(
        "無法讀取額度",
        requiresTrust
          ? ["手機需先與 PC 完成配對，才能查看本機 AI 額度。"]
          : [error.message || "額度 API 失敗。", "請確認 Host 在跑，並在 PC 本機操作額度來源。"]
      ),
      onConnectionChange: state => setDashboardConnectionState(state),
    });
    const refreshQuotas = options => quotaController.refresh(options);

    function renderQuotas(snapshot) {
      quotaSnapshotData = snapshot || {};
      quotaMiniController?.renderSnapshot(snapshot);
      renderQuotaContent();
    }

    function quotaDataFingerprint(snapshot) {
      const providers = snapshot?.Providers || snapshot?.providers || [];
      return JSON.stringify(providers.map(provider => ({
        id: provider.Id || provider.id || "",
        family: provider.Family || provider.family || "",
        accountId: provider.AccountId || provider.accountId || "",
        state: provider.State || provider.state || "",
        observedAt: provider.ObservedAt || provider.observedAt || "",
        primary: provider.Primary || provider.primary || null,
        secondary: provider.Secondary || provider.secondary || null
      })).sort((left, right) => `${left.family}:${left.accountId}:${left.id}`.localeCompare(`${right.family}:${right.accountId}:${right.id}`)));
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
          ? `Codex · ${codexProviders.length} 個帳號 · 已讀到額度`
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
      hint.textContent = "說明";
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
          return renderQuotaHelpBlock("Codex 資料來源", eink
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
        return renderQuotaHelpBlock("AGY 資料來源", eink
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
            <button type="button" title="登入 AGY" data-quota-action="agy-oauth">＋ 登入</button>
            <button type="button" title="更新額度" data-quota-action="refresh">↻ 更新</button>
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
            <button type="button" title="登入 AGY" data-quota-action="agy-oauth">＋ 登入</button>
            <button type="button" title="更新額度" data-quota-action="refresh">↻ 更新</button>
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
            <button type="button" title="更新額度" data-quota-action="refresh">↻ 更新</button>
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
            <button type="button" title="更新額度" data-quota-action="refresh">↻ 更新</button>
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
          localStorage.setItem(QUOTA_TAB_STORAGE_KEY, quotaActiveTab);
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
        await runQuotaButton(refreshButton, async () => {
          const previous = quotaDataFingerprint(quotaSnapshotData);
          const snapshot = await refreshQuotas({ force: true, throwOnError: true });
          const unchanged = previous && previous === quotaDataFingerprint(snapshot);
          return {
            Message: unchanged
              ? "已重新讀取，來源沒有新額度資料。"
              : "額度已更新。"
          };
        }, {
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
          quotaController.startOAuthPolling();
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

    function getCurrentQuotaStatusCard(previousCard) {
      const statusKey = previousCard?.dataset?.statusKey;
      if (statusKey) {
        const matchingCard = Array.from(document.querySelectorAll(".quota-account-card"))
          .find(item => item.dataset.statusKey === statusKey);
        if (matchingCard) return matchingCard;
      }
      return document.querySelector(".quota-account-card") || previousCard;
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
        const statusCard = getCurrentQuotaStatusCard(card);
        setQuotaCardStatus(statusCard, getQuotaActionMessage(result, options.success || "完成。"), "success");
        return result;
      } catch (error) {
        setQuotaCardStatus(getCurrentQuotaStatusCard(card), error.message || options.error || "額度操作失敗。", "error");
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
            <button type="button" title="用這個帳號開啟 AGY CLI" data-quota-action="agy-cli">▶ 開啟</button>
            <button type="button" title="登入 AGY" data-quota-action="agy-oauth">＋ 登入</button>
            <button type="button" title="更新額度" data-quota-action="refresh">↻ 更新</button>
            <button type="button" title="刪除 AGY 帳號" data-quota-action="agy-delete">⌫ 刪除</button>
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
            <button type="button" title="更新額度" data-quota-action="refresh">↻ 更新</button>
            ${family === "codex" ? '<button type="button" title="刪除這個 Codex Profile 額度快取" data-quota-action="codex-delete">⌫ 刪除</button>' : ''}
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
          const leftObserved = Date.parse(left.ObservedAt || left.observedAt || "") || 0;
          const rightObserved = Date.parse(right.ObservedAt || right.observedAt || "") || 0;
          if (leftObserved !== rightObserved) return rightObserved - leftObserved;
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
          <strong class="quota-provider-title">${escapeHtml(title)}</strong>
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

    function setStatus(text, online) {
      statusText.textContent = text;
      dot.classList.toggle("online", online);
    }

    function setDashboardConnectionState(state) {
      dashboardConnectionState = state === "online" ? "online" : "connecting";
      document.querySelectorAll("[data-eink-connection-state]").forEach(element => {
        const online = dashboardConnectionState === "online";
        element.textContent = online ? "連線中" : "正在連線";
        element.classList.toggle("online", online);
        element.classList.toggle("connecting", !online);
        element.classList.toggle("offline", !online);
      });
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
              ? (metricText ? `串流：${metricText}` : "串流：JPEG 可用，瀏覽器 H.264 可用。")
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

    function recordFrame(byteLength, fallbackReason = "") {
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
      const protocolLabel = fallbackReason ? `${fallbackReason} · ` : "";
      setStatus(`${protocolLabel}jpeg ${fps.toFixed(0)}fps ${mbps.toFixed(1)}Mbps`, true);
      streamStats.lastFrames = streamStats.frames;
      streamStats.lastBytes = streamStats.bytes;
      streamStats.lastTime = now;
    }

    function getActiveStreamElement() {
      return streamController?.getActiveStreamElement() || screen;
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
      if (window.screen?.orientation) {
        if (value !== "auto") {
          window.screen.orientation.lock?.(value).catch(() => {});
        } else {
          window.screen.orientation.unlock?.();
          if (document.fullscreenElement && isEinkClient()) {
            window.screen.orientation.lock?.("any").catch(() => {});
          }
        }
      }
    }

    function resolveRotation() {
      if (rotation.value !== "auto") return rotation.value;

      // Auto rotation: only rotate when in fullscreen viewer mode so the desktop
      // fills the screen landscape. In the non-fullscreen thumbnail preview, keep
      // it upright — portrait streams show portrait, landscape shows landscape.
      if (!document.body.classList.contains("viewer-fullscreen")) return "0";

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
      // E-ink / sideboard also need the immersive body class for 100dvh layout.
      if (!isDisplayMode) {
        document.body.classList.add("viewer-immersive");
      } else {
        document.body.classList.remove("viewer-immersive");
      }
      window.scrollTo(0, 1);
      setTimeout(() => {
        updateViewportSize();
        applyRotation();
      }, 250);

      // Safari iOS ignores Fullscreen API; CSS immersive classes are enough there.
      // Android / BOOX: request real fullscreen for display AND sideboard/quota panels.
      if (!isIos()) {
        const root = document.documentElement;
        const candidates = [
          () => root.requestFullscreen && root.requestFullscreen({ navigationUI: "hide" }),
          () => root.webkitRequestFullscreen && root.webkitRequestFullscreen(),
          () => root.webkitRequestFullScreen && root.webkitRequestFullScreen(),
          () => document.body.requestFullscreen && document.body.requestFullscreen({ navigationUI: "hide" })
        ];
        for (const tryEnter of candidates) {
          try {
            const result = tryEnter();
            if (result && typeof result.then === "function") await result;
            if (document.fullscreenElement || document.webkitFullscreenElement) break;
          } catch {
            // try next vendor path
          }
        }
      }

      if (isDisplayMode && orientation.value === "landscape" && window.screen && window.screen.orientation && window.screen.orientation.lock) {
        try {
          await window.screen.orientation.lock("landscape");
        } catch {
        }
      }
      if (!isDisplayMode && isEinkClient() && orientation.value === "auto" && window.screen?.orientation?.lock) {
        try {
          await window.screen.orientation.lock("any");
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
      document.body.classList.remove("viewer-immersive");
      try {
        if (document.exitFullscreen && (document.fullscreenElement || document.webkitFullscreenElement)) {
          await document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
          document.webkitExitFullscreen();
        }
      } catch {
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
          setDisplayAvailability(false, "尚未完成配對", "完成配對後才能觀看虛擬螢幕。資訊板可直接使用。");
          return;
        }

        setStatus("顯示器無法使用", false);
        driverState.textContent = error.message || "無法取得 VibeDeck 顯示器狀態。";
        setDisplayAvailability(false, "暫時連不上顯示器", "請確認 Host 仍在執行，或先改用資訊板。");
        return;
      }

      const phoneDisplay = displays.find(display => display.IsPhoneMonitor);

      if (!phoneDisplay) {
        selectedDisplayName = "";
        setDisplayAspectRatio("16 / 9");
        setStatus("尚未建立虛擬螢幕", false);
        driverState.textContent = "尚未建立 VibeDeck 虛擬螢幕。";
        setDisplayAvailability(false, "這台電腦還沒有虛擬螢幕", "建立後，Windows 才能把手機當成真正的延伸桌面。");
        await loadDisplayInstallStatus();
        return;
      }

      selectedDisplayName = phoneDisplay.DeviceName;
      setDisplayAspectRatio(`${phoneDisplay.Width} / ${phoneDisplay.Height}`);
      driverState.textContent = `VibeDeck 顯示器：${phoneDisplay.DeviceName} (${phoneDisplay.Width}x${phoneDisplay.Height})`;
      setDisplayAvailability(true);
    }

    function readInstallField(status, name, fallback = null) {
      return status?.[name] ?? status?.[name.charAt(0).toLowerCase() + name.slice(1)] ?? fallback;
    }

    function renderDisplayInstallStatus(status) {
      const state = readInstallField(status, "State", "ready");
      const message = readInstallField(status, "Message", "");
      const canInstall = Boolean(readInstallField(status, "CanInstall", false));

      if (displayInstallDetail) {
        displayInstallDetail.textContent = deviceLocalRequest
          ? message
          : "請到這台 PC 的 VibeDeck 頁面建立虛擬螢幕；手機不能遠端觸發管理員安裝。";
      }

      if (!installVirtualDisplay) return state;
      installVirtualDisplay.hidden = !deviceLocalRequest || state === "installed" || state === "finishing" || state === "console-required";
      installVirtualDisplay.disabled = !canInstall || state === "installing";
      installVirtualDisplay.textContent = state === "installing"
        ? "正在建立…"
        : state === "failed"
          ? "再試一次"
          : state === "repair-ready"
            ? "修復虛擬螢幕"
          : state === "restart-required"
            ? "需要重新開機"
            : "建立虛擬螢幕";

      if (state === "installing") {
        setDisplayAvailability(false, "正在建立虛擬螢幕", "請在 Windows 管理員確認視窗按「是」，完成前不要關閉 VibeDeck。");
      } else if (state === "finishing" || state === "installed") {
        setDisplayAvailability(false, "正在完成虛擬螢幕", "驅動已安裝，正在等待 Windows 顯示新的延伸桌面。");
      } else if (state === "restart-required") {
        setDisplayAvailability(false, "需要重新開機", "Windows 必須重新開機才能完成虛擬螢幕安裝。");
      } else if (state === "console-required") {
        setDisplayAvailability(false, "請在本機 Windows 桌面啟動", "遠端桌面會把實體與虛擬顯示器換成 RDP 畫面，因此 VibeDeck 無法在這個工作階段接收延伸桌面。");
      } else if (state === "failed") {
        setDisplayAvailability(false, "虛擬螢幕沒有建立成功", "可以再試一次；原本的螢幕與資訊板不受影響。");
      }

      return state;
    }

    async function loadDisplayInstallStatus() {
      try {
        const status = await fetchJsonOrThrow("/api/display/install/status");
        return { status, state: renderDisplayInstallStatus(status) };
      } catch (error) {
        if (displayInstallDetail) displayInstallDetail.textContent = error.message || "無法取得安裝狀態。";
        return { status: null, state: "error" };
      }
    }

    function scheduleDisplayInstallPoll() {
      clearTimeout(displayInstallTimer);
      displayInstallTimer = setTimeout(async () => {
        const { state } = await loadDisplayInstallStatus();
        if (state === "installing" || state === "finishing" || state === "installed") {
          await loadPhoneDisplay();
          if (!selectedDisplayName) scheduleDisplayInstallPoll();
          else connectVideo();
        }
      }, 1500);
    }

    async function installDisplayFromWeb() {
      if (!deviceLocalRequest || !installVirtualDisplay) return;
      installVirtualDisplay.disabled = true;
      installVirtualDisplay.textContent = "等待 Windows 確認…";
      if (displayInstallDetail) displayInstallDetail.textContent = "請在這台 PC 跳出的管理員確認視窗按「是」。";

      try {
        const status = await fetchJsonOrThrow("/api/display/install", { method: "POST" });
        renderDisplayInstallStatus(status);
        scheduleDisplayInstallPoll();
      } catch (error) {
        renderDisplayInstallStatus({
          State: "failed",
          CanInstall: true,
          Message: error.message || "虛擬螢幕安裝失敗。"
        });
      }
    }

    function setDisplayAvailability(available, title = "", message = "") {
      document.body.classList.toggle("display-unavailable", !available);
      if (displayEmptyState) displayEmptyState.hidden = Boolean(available);
      if (displayEmptyTitle && title) displayEmptyTitle.textContent = title;
      if (displayEmptyMessage && message) displayEmptyMessage.textContent = message;
    }

    function inputSocketUrl() {
      const params = appendDeviceToken(new URLSearchParams());
      const query = params.toString();
      return `${wsBase}/ws/input${query ? `?${query}` : ""}`;
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
      const info = parseJsonResponse(await response.text(), "/api/connect");
      if (!response.ok) {
        throw new Error(info.error || info.message || `連線資訊讀取失敗 HTTP ${response.status}`);
      }
      const httpsAvailable = Boolean(info.HttpsAvailable ?? info.httpsAvailable);
      const rootCertificateUrl = info.RootCertificateUrl || info.rootCertificateUrl || "";
      const httpsSetupHint = info.HttpsSetupHint || info.httpsSetupHint || "";
      if (!pairingQrActive) {
        showInstallQr();
      }
      const httpsUrl = info.HttpsUrl || info.httpsUrl || "";
      const httpUrl = info.HttpUrl || info.httpUrl || "";
      // HTTPS is the canonical phone URL; HTTP remains a local bootstrap fallback.
      prettyLink.href = httpsAvailable ? httpsUrl : (info.LocalNameHttpUrl || httpUrl);
      prettyLink.textContent = httpsAvailable
        ? `手機請用：${httpsUrl}`
        : `本機：${info.LocalNameHttpUrl || httpUrl}`;
      httpsLink.href = httpsAvailable ? httpsUrl : "#";
      httpsLink.textContent = httpsAvailable
        ? `HTTPS（推薦）：${httpsUrl}`
        : `HTTPS 未就緒：${httpsSetupHint || "PC 執行 scripts\\setup-https.ps1"}`;
      httpsCertLink.hidden = !httpsAvailable || !rootCertificateUrl;
      httpsCertLink.href = rootCertificateUrl || "#";
      httpsCertLink.textContent = "安裝 HTTPS 憑證";
      httpLink.href = httpUrl || "#";
      httpLink.textContent = `HTTP（本機備援）：${httpUrl}`;
      const connectTitleHint = document.getElementById("connectTitleHint");
      if (connectTitleHint) {
        connectTitleHint.textContent = "用手機瀏覽器開啟 HTTPS 網址；第一次只需在下方允許一次。";
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
      updateWakeCapability();
    }

    async function loadModePresets() {
      const response = await fetch("/api/display/modes");
      const presets = parseJsonResponse(await response.text(), "/api/display/modes");
      if (!response.ok || !Array.isArray(presets)) {
        throw new Error(presets.error || presets.message || `解析度預設讀取失敗 HTTP ${response.status}`);
      }
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

    function connectVideo() {
      return streamController?.connect();
    }

    function closeJpegStream() {
      return streamController?.closeJpegStream();
    }

    function closeRtcStream(invalidate = true) {
      return streamController?.closeRtcStream(invalidate);
    }

    try {
      streamController = createStreamController({
        elements: { screen, rtcScreen },
        getWsBase: () => wsBase,
        appendDeviceToken,
        getSelectedDisplayName: () => selectedDisplayName,
        getStreamSettings: () => ({
          fps: Number(streamFps.value),
          quality: Number(streamQuality.value),
          rotationIsAuto: rotation.value === "auto",
        }),
        canUseProtectedConnection,
        loadPhoneDisplay,
        prefersWebRtcDisplay,
        isLoopbackHost,
        setStatus,
        applyRotation,
        resetJpegStats: resetStreamStats,
        recordJpegFrame: recordFrame,
        fetchJsonOrThrow,
        tuneVideoReceiver,
      });
    } catch (error) {
      console.error("stream controller failed", error);
      streamController = null;
    }

    let displayInputController = null;
    try {
      displayInputController = createDisplayInputController({
        targets: [screen, rtcScreen],
        getInputSocket: () => inputSocket,
        getDeviceName: () => selectedDisplayName,
        getActiveStreamElement,
        getMediaWidth,
        getMediaHeight,
        resolveRotation,
        isMobileClient,
        enterLandscapeViewer,
        touchLongPressMs,
        touchDragThresholdPx,
      });
      displayInputController.wireAll();
    } catch (error) {
      console.error("display input controller failed", error);
    }
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
    pairPhone?.addEventListener("click", () => {
      startPhonePairing();
    });
    // 流程圖第 2 步也可點，避免按鈕被藏時完全沒入口
    document.getElementById("pairingStepPair")?.addEventListener("click", () => {
      if (pairPhone?.hidden && !isLoopbackHost() && !deviceLocalRequest) return;
      setPairControlsVisible(true);
      startPhonePairing();
    });
    phonePairRequest?.addEventListener("click", async () => {
      phonePairRequest.disabled = true;
      phonePairRequest.textContent = "申請中…";
      try {
        await requestApprovalPairing({ allowCreate: true });
      } catch (error) {
        setTrustState(error.message || "配對申請失敗。", false);
      } finally {
        phonePairRequest.disabled = false;
        phonePairRequest.textContent = "開始配對";
      }
    });
    displaySettingsToggle?.addEventListener("click", () => {
      const open = !document.body.classList.contains("display-settings-open");
      document.body.classList.toggle("display-settings-open", open);
      displaySettingsToggle.setAttribute("aria-expanded", open ? "true" : "false");
      displaySettingsToggle.textContent = open ? "收起設定" : "顯示設定";
    });
    installVirtualDisplay?.addEventListener("click", installDisplayFromWeb);
    openSideboardFromEmpty?.addEventListener("click", () => setMode("sideboard"));
    hostAuthForm?.addEventListener("submit", event => {
      event.preventDefault();
      loginToHost(hostAuthPassword?.value || "");
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
      customCardsController?.refreshAll();
      refreshQuotas();
    });
    displayMode.addEventListener("click", () => setMode("display"));
    sideboardMode.addEventListener("click", () => setMode("sideboard"));
    const einkModeToggle = document.getElementById("einkModeToggle");
    if (einkModeToggle) {
      einkModeToggle.addEventListener("click", () => {
        setEinkClient(!isEinkClient());
      });
    }
    quotaMode.addEventListener("click", () => setMode("quota"));
    document.querySelectorAll("[data-dashboard-mode]").forEach(button => {
      button.addEventListener("click", () => setMode(button.dataset.dashboardMode));
    });
    for (const button of document.querySelectorAll("[data-side-skin]")) {
      button.addEventListener("click", () => setSideSkin(button.dataset.sideSkin));
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
    function onFullscreenChromeChange() {
      const inFs = Boolean(document.fullscreenElement || document.webkitFullscreenElement);
      if (!inFs && !isIos()) {
        // Keep CSS immersive for e-ink panel until user taps exit; only drop display
        // stream chrome when the system fullscreen shell closes.
        if (activeMode === "display") {
          document.body.classList.remove("viewer-fullscreen");
        }
      }
      updateViewportSize();
      applyRotation();
    }
    document.addEventListener("fullscreenchange", onFullscreenChromeChange);
    document.addEventListener("webkitfullscreenchange", onFullscreenChromeChange);
    document.addEventListener("visibilitychange", async () => {
      if (document.visibilityState === "visible") {
        scheduleDashboardRefresh("sideboard", true);
        scheduleDashboardRefresh("quota", true);
        scheduleDashboardRefresh("customCards", true);
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
      if (displayInputController.isTouchGestureActive()) return;
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
    window.addEventListener("offline", () => setDashboardConnectionState("connecting"));
    window.addEventListener("online", () => {
      setDashboardConnectionState("connecting");
      scheduleDashboardRefresh(activeMode === "quota" ? "quota" : "sideboard", true);
    });
    window.addEventListener("devicemotion", handleEinkDeviceMotion, { passive: true });
    if (window.visualViewport) {
      window.visualViewport.addEventListener("resize", () => {
        updateViewportSize();
        applyRotation();
      });
    }
    async function boot() {
      await (window.phoneMonitorServiceWorkerCleanup || Promise.resolve());
      ensureEinkPreferenceSticky();
      // If hardware/cookie says e-ink but URL has no flag, stamp ?eink=1 so a later
      // "Add to Home Screen" is more likely to capture paper mode.
      if (isEinkClient() && readEinkQuery() === null) {
        try {
          const url = new URL(location.href);
          url.searchParams.set("eink", "1");
          history.replaceState({}, "", url.pathname + url.search + url.hash);
        } catch {
          // ignore
        }
      }
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

      try {
        await loadHostAuthStatus();
      } catch {
        hostAuthEnabled = false;
        hostAuthenticated = false;
        hostAuthRequired = false;
      }
      if (hostAuthRequired && !hostAuthenticated) {
        setStatus("等待遠端登入", false);
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
        customCardsTimer = setInterval(() => scheduleDashboardRefresh("customCards"), 5000);
        activityNotificationsTimer = setInterval(refreshActivityNotifications, 5000);
        return;
      }

      describeClient();
      updateInstallState();
      // Re-hydrate token from cookie/session if localStorage was empty (iOS Home Screen cases).
      const again = loadStoredDeviceCredentials();
      if (!deviceToken && again.token) {
        persistDeviceCredentials(again.token, again.id);
      }
      applyClientChrome();
      await loadDeviceTrustStatus();
      applyClientChrome();
      setMode(getInitialMode());
      if (shouldStartInViewer() || (isIos() && deviceTrusted && getInitialMode() === "display" && isStandaloneApp())) {
        setTimeout(() => enterLandscapeViewer(), 350);
      }
      connectDashboardEvents();
      sideboardTimer = setInterval(() => scheduleDashboardRefresh("sideboard"), 60000);
      quotaTimer = setInterval(() => scheduleDashboardRefresh("quota"), 120000);
      customCardsTimer = setInterval(() => scheduleDashboardRefresh("customCards"), 5000);
      activityNotificationsTimer = setInterval(refreshActivityNotifications, 5000);
      dashboardConnectionTimer = setInterval(() => {
        scheduleDashboardRefresh(activeMode === "quota" ? "quota" : "sideboard", true);
      }, 15000);
      if (isEinkClient()) {
        // Electronic readers are dashboards, not remote displays. Do not run
        // display discovery or let its errors overwrite the connection state.
        setDisplayAvailability(true);
        driverState.textContent = "";
        setStatus("正在連線", false);
        setDashboardConnectionState("connecting");
      } else {
        await loadModePresets().catch(error => {
          setStatus(error.message || "解析度預設讀取失敗", false);
        });
      }
      loadConnectInfo().catch(error => {
        setTrustState(error.message || "連線資訊讀取失敗。", false);
        setStatus(error.message || "連線資訊讀取失敗", false);
      });
      if (!isEinkClient()) {
        loadStreamCapabilities();
        loadPhoneDisplay().then(loadDisplayStatus).finally(connectVideo);
        connectInput();
      }
      if (isIos()) {
        fullscreen.textContent = "全螢幕";
      }
      updateKeepAwakeButton();
      updateWakeCapability();
      startKeepAwakeWatch();
      // BOOX browsers may be served over LAN HTTP and still need the silent
      // video fallback.
      if (keepAwakeDesired && (isEinkClient() || (location.protocol === "https:" && (isIos() || isMobileClient() || deviceTrusted)))) {
        ensureKeepAwake();
        setTimeout(() => ensureKeepAwake(), 800);
      }
    }

    boot();
