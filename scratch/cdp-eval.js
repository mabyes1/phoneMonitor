// node cdp-eval.js <url> <w> <h> <waitMs> <mobile> <evalJs> — prints eval result JSON
const http = require("http");
const [, , url, w = "844", h = "390", waitMs = "5000", mobile = "1", evalJs = "1"] = process.argv;
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const PORT = 9335;
function jget(path) {
  return new Promise((resolve, reject) => {
    http.get({ host: "127.0.0.1", port: PORT, path }, (res) => {
      let d = ""; res.on("data", (c) => (d += c)); res.on("end", () => resolve(JSON.parse(d)));
    }).on("error", reject);
  });
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const { spawn } = require("child_process");
  const chrome = spawn(CHROME, [
    "--headless=new", "--disable-gpu", "--no-sandbox", "--ignore-certificate-errors",
    `--remote-debugging-port=${PORT}`, `--user-data-dir=${process.env.TEMP}/chrome-cdp-${Date.now()}`, "about:blank",
  ], { stdio: "ignore" });
  try {
    let targets = null;
    for (let i = 0; i < 40; i++) { await sleep(250); try { targets = await jget("/json/list"); break; } catch {} }
    const page = targets.find((t) => t.type === "page");
    const ws = new WebSocket(page.webSocketDebuggerUrl);
    let id = 0; const pending = new Map();
    const send = (method, params = {}) => new Promise((resolve) => {
      const mid = ++id; pending.set(mid, { resolve });
      ws.send(JSON.stringify({ id: mid, method, params }));
    });
    ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      if (m.id && pending.has(m.id)) { pending.get(m.id).resolve(m.result); pending.delete(m.id); }
    };
    await new Promise((r) => (ws.onopen = r));
    if (mobile === "1") {
      await send("Emulation.setDeviceMetricsOverride", { width: +w, height: +h, deviceScaleFactor: 2, mobile: true });
      await send("Emulation.setUserAgentOverride", { userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1" });
    } else {
      await send("Emulation.setDeviceMetricsOverride", { width: +w, height: +h, deviceScaleFactor: 1, mobile: false });
    }
    await send("Page.navigate", { url });
    await sleep(+waitMs);
    const r = await send("Runtime.evaluate", { expression: evalJs, returnByValue: true });
    console.log(JSON.stringify(r.result.value ?? r.result, null, 1));
  } finally { chrome.kill("SIGKILL"); }
})().catch((e) => { console.error(e); process.exit(1); });
