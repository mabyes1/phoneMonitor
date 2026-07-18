const devices = {
  "boox-go-color-7": {
    name: "BOOX Go Color 7",
    portrait: [794, 1054],
    landscape: [1054, 794],
    dpr: 1.594,
    physical: "1264 × 1680",
    defaultOrientation: "landscape",
    sourceKey: "booxSource",
  },
  "galaxy-s23": {
    name: "Samsung Galaxy S23",
    portrait: [360, 780],
    landscape: [780, 360],
    dpr: 3,
    physical: "1080 × 2340",
    defaultOrientation: "portrait",
    sourceKey: "webSource",
  },
  "iphone-xs": {
    name: "iPhone XS",
    portrait: [375, 812],
    landscape: [812, 375],
    dpr: 3,
    physical: "1125 × 2436",
    defaultOrientation: "portrait",
    sourceKey: "appleSource",
  },
};

let labMessages = {};

const deviceSelect = document.getElementById("deviceSelect");
const orientationSelect = document.getElementById("orientationSelect");
const modeSelect = document.getElementById("modeSelect");
const languageSelect = document.getElementById("languageSelect");
const scaleSelect = document.getElementById("scaleSelect");
const reloadPreview = document.getElementById("reloadPreview");
const labStage = document.getElementById("labStage");
const deviceScaler = document.getElementById("deviceScaler");
const deviceFrame = document.getElementById("deviceFrame");
const deviceScreen = document.getElementById("deviceScreen");
const previewFrame = document.getElementById("previewFrame");
const deviceName = document.getElementById("deviceName");
const deviceMetrics = document.getElementById("deviceMetrics");
const layoutStatus = document.getElementById("layoutStatus");
const layoutDetail = document.getElementById("layoutDetail");
const deviceSource = document.getElementById("deviceSource");

let loadTimer = 0;
let statusTimer = 0;

function text(key, values = {}) {
  return String(labMessages[key] || key)
    .replace(/\{(\w+)\}/g, (_, name) => values[name] ?? `{${name}}`);
}

async function loadLanguage() {
  const response = await fetch(`/locales/${encodeURIComponent(languageSelect.value)}.json`, { cache: "no-store" });
  if (!response.ok) throw new Error(`Locale failed to load (${response.status})`);
  const catalog = await response.json();
  labMessages = catalog.deviceLab || {};
  applyLanguage();
}

function applyLanguage() {
  document.documentElement.lang = languageSelect.value;
  document.querySelectorAll("[data-i18n]").forEach(element => {
    element.textContent = text(element.dataset.i18n);
  });
  updateDeviceLabels();
  setChecking();
}

function currentDevice() {
  return devices[deviceSelect.value] || devices["boox-go-color-7"];
}

function currentViewport() {
  return currentDevice()[orientationSelect.value] || currentDevice().portrait;
}

function updateDeviceLabels() {
  const device = currentDevice();
  const [width, height] = currentViewport();
  deviceName.textContent = device.name;
  deviceMetrics.textContent = `${width} × ${height} CSS px · DPR ${device.dpr} · ${device.physical} px`;
  deviceSource.textContent = text(device.sourceKey);
}

function previewUrl() {
  const parameters = new URLSearchParams({
    mode: modeSelect.value,
    lang: languageSelect.value,
    devicePreview: deviceSelect.value,
    eink: deviceSelect.value === "boox-go-color-7" ? "1" : "0",
    source: "device-lab",
    preview: String(Date.now()),
  });
  if (deviceSelect.value === "boox-go-color-7" && ["sideboard", "quota"].includes(modeSelect.value)) {
    parameters.set("viewer", "1");
  }
  return `/index.html?${parameters.toString()}`;
}

function setChecking() {
  layoutStatus.className = "status-badge checking";
  layoutStatus.textContent = text("checking");
  layoutDetail.textContent = text("loading");
}

function applyScale() {
  const frameWidth = deviceFrame.offsetWidth;
  const frameHeight = deviceFrame.offsetHeight;
  if (!frameWidth || !frameHeight) return;

  const requested = scaleSelect.value === "auto" ? null : Number(scaleSelect.value);
  const availableWidth = Math.max(100, labStage.clientWidth - 52);
  const availableHeight = Math.max(100, labStage.clientHeight - 52);
  const scale = requested || Math.min(1, availableWidth / frameWidth, availableHeight / frameHeight);
  deviceFrame.style.transform = `scale(${scale})`;
  deviceScaler.style.width = `${Math.ceil(frameWidth * scale)}px`;
  deviceScaler.style.height = `${Math.ceil(frameHeight * scale)}px`;
}

function configureFrame({ reload = true } = {}) {
  const device = currentDevice();
  const [width, height] = currentViewport();
  if (deviceSelect.value === "boox-go-color-7" && modeSelect.value === "display") {
    modeSelect.value = "sideboard";
  }
  modeSelect.querySelector('option[value="display"]').disabled = deviceSelect.value === "boox-go-color-7";
  deviceFrame.dataset.device = deviceSelect.value;
  deviceFrame.dataset.orientation = orientationSelect.value;
  deviceScreen.style.width = `${width}px`;
  deviceScreen.style.height = `${height}px`;
  previewFrame.style.width = `${width}px`;
  previewFrame.style.height = `${height}px`;
  previewFrame.width = width;
  previewFrame.height = height;
  updateDeviceLabels();
  setChecking();
  requestAnimationFrame(applyScale);
  if (reload) previewFrame.src = previewUrl();
}

function inspectLayout() {
  try {
    const previewWindow = previewFrame.contentWindow;
    const previewDocument = previewFrame.contentDocument;
    if (!previewWindow || !previewDocument?.documentElement) return;
    const [expectedWidth, expectedHeight] = currentViewport();
    const actualWidth = Math.round(previewWindow.innerWidth);
    const actualHeight = Math.round(previewWindow.innerHeight);
    const root = previewDocument.documentElement;
    const body = previewDocument.body;
    const contentWidth = Math.max(root.scrollWidth, body?.scrollWidth || 0);
    const exact = actualWidth === expectedWidth && actualHeight === expectedHeight;
    const overflow = contentWidth > actualWidth + 1;
    const values = {
      actual: `${actualWidth} × ${actualHeight}`,
      expected: `${expectedWidth} × ${expectedHeight}`,
      content: contentWidth,
    };

    layoutStatus.className = `status-badge ${exact && !overflow ? "pass" : "fail"}`;
    layoutStatus.textContent = text(exact && !overflow ? "pass" : "fail");
    layoutDetail.textContent = overflow
      ? text("overflow", values)
      : exact
        ? text("exact", values)
        : text("mismatch", values);
  } catch {
    setChecking();
  }
}

deviceSelect.addEventListener("change", () => {
  orientationSelect.value = currentDevice().defaultOrientation;
  configureFrame();
});
orientationSelect.addEventListener("change", () => configureFrame());
modeSelect.addEventListener("change", () => configureFrame());
languageSelect.addEventListener("change", async () => {
  await loadLanguage();
  configureFrame();
});
scaleSelect.addEventListener("change", applyScale);
reloadPreview.addEventListener("click", () => configureFrame());
previewFrame.addEventListener("load", () => {
  clearTimeout(loadTimer);
  loadTimer = window.setTimeout(inspectLayout, 900);
});
window.addEventListener("resize", applyScale);

async function start() {
  const parameters = new URLSearchParams(location.search);
  languageSelect.value = parameters.get("lang") || "zh-Hant";
  if (!["zh-Hant", "en", "ja"].includes(languageSelect.value)) languageSelect.value = "zh-Hant";
  deviceSelect.value = parameters.get("device") || "boox-go-color-7";
  if (!devices[deviceSelect.value]) deviceSelect.value = "boox-go-color-7";
  orientationSelect.value = currentDevice().defaultOrientation;
  await loadLanguage();
  configureFrame();
  statusTimer = window.setInterval(inspectLayout, 1500);
}

start().catch(error => {
  layoutStatus.className = "status-badge fail";
  layoutStatus.textContent = "Locale error";
  layoutDetail.textContent = error.message;
});
window.addEventListener("beforeunload", () => window.clearInterval(statusTimer));
