// CDP screenshot tool: node shot.js <url> <outfile> <width> <height> <waitMs> [mobile]
const http = require("http");

const [, , url, outfile, w = "390", h = "844", waitMs = "4000", mobile = "1"] = process.argv;
const CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const PORT = 9333;

function jget(path) {
  return new Promise((resolve, reject) => {
    http.get({ host: "127.0.0.1", port: PORT, path }, (res) => {
      let data = "";
      res.on("data", (c) => (data += c));
      res.on("end", () => resolve(JSON.parse(data)));
    }).on("error", reject);
  });
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const { spawn } = require("child_process");
  const args = [
    "--headless=new", "--disable-gpu", "--no-sandbox", "--hide-scrollbars",
    "--ignore-certificate-errors",
    `--remote-debugging-port=${PORT}`,
    `--user-data-dir=${process.env.TEMP}/chrome-cdp-${Date.now()}`,
    "about:blank",
  ];
  const chrome = spawn(CHROME, args, { stdio: "ignore" });
  try {
    let targets = null;
    for (let i = 0; i < 40; i++) {
      await sleep(250);
      try { targets = await jget("/json/list"); break; } catch {}
    }
    const page = targets.find((t) => t.type === "page");
    const ws = new WebSocket(page.webSocketDebuggerUrl);
    let id = 0;
    const pending = new Map();
    const send = (method, params = {}) =>
      new Promise((resolve) => {
        const mid = ++id;
        pending.set(mid, { resolve });
        ws.send(JSON.stringify({ id: mid, method, params }));
      });
    ws.onmessage = (ev) => {
      const msg = JSON.parse(ev.data);
      if (msg.id && pending.has(msg.id)) {
        pending.get(msg.id).resolve(msg.result);
        pending.delete(msg.id);
      }
    };
    await new Promise((r) => (ws.onopen = r));
    await send("Page.enable");
    if (mobile === "1") {
      await send("Emulation.setDeviceMetricsOverride", {
        width: +w, height: +h, deviceScaleFactor: 2, mobile: true,
      });
      await send("Emulation.setUserAgentOverride", {
        userAgent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
      });
      await send("Emulation.setTouchEmulationEnabled", { enabled: true });
    } else {
      await send("Emulation.setDeviceMetricsOverride", { width: +w, height: +h, deviceScaleFactor: 1, mobile: false });
    }
    await send("Page.navigate", { url });
    await sleep(+waitMs);
    const shot = await send("Page.captureScreenshot", { format: "png" });
    require("fs").writeFileSync(outfile, Buffer.from(shot.data, "base64"));
    console.log("saved", outfile);
    const m = await send("Runtime.evaluate", {
      expression: `JSON.stringify({sw:document.documentElement.scrollWidth,iw:window.innerWidth,bodyClass:document.body.className,overflow:document.documentElement.scrollWidth>window.innerWidth})`,
      returnByValue: true,
    });
    console.log("metrics", m.result.value);
  } finally {
    chrome.kill("SIGKILL");
  }
})().catch((e) => { console.error(e); process.exit(1); });
