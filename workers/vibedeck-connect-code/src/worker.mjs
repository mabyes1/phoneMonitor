const DEFAULT_TTL_SECONDS = 600;
const MIN_TTL_SECONDS = 60;
const MAX_TTL_SECONDS = 900;
const CODE_PATTERN = /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$/;
const LANDING_SUBMIT_GUARD = 'document.addEventListener("submit",e=>{const f=e.target;if(!f.matches("form[data-once]"))return;if(f.dataset.submitting==="1"){e.preventDefault();return}f.dataset.submitting="1";f.querySelector("button[type=submit]")?.setAttribute("disabled","")});';
const LANDING_SUBMIT_GUARD_CSP_HASH = "'sha256-bfXKPBvv3fl+jHsvWGd3kmxKB0McbscPTDLop6BifXY='";

const translations = {
  "zh-Hant": {
    title: "連接 VibeDeck",
    eyebrow: "VIBEDECK · 電子紙連線",
    intro: "在 Windows 電腦上產生一次性連線碼，並在這裡輸入。",
    label: "8 位連線碼",
    placeholder: "例如 ABCD-EFGH",
    submit: "開啟這台電腦",
    note: "連線碼只會導向已設定的安全網址；手機仍必須由電腦端允許配對。",
    redirecting: "正在安全開啟這台電腦…",
    continue: "若未自動開啟，請點這裡繼續。",
    invalid: "連線碼無效、已使用或已過期。請回 Windows 電腦產生新的代碼。"
  },
  en: {
    title: "Connect VibeDeck",
    eyebrow: "VIBEDECK · E-PAPER CONNECTION",
    intro: "Create a one-time connection code on the Windows PC and enter it here.",
    label: "8-character connection code",
    placeholder: "For example ABCD-EFGH",
    submit: "Open this PC",
    note: "The code only opens the configured secure URL. Pairing still requires approval on the PC.",
    redirecting: "Opening this PC securely…",
    continue: "If it does not open automatically, continue here.",
    invalid: "This connection code is invalid, used, or expired. Create a new code on the Windows PC."
  },
  ja: {
    title: "VibeDeck に接続",
    eyebrow: "VIBEDECK · E-INK 接続",
    intro: "Windows PC で一時接続コードを作成し、ここに入力します。",
    label: "8 文字の接続コード",
    placeholder: "例: ABCD-EFGH",
    submit: "この PC を開く",
    note: "コードは設定済みの安全な URL を開くだけです。ペアリングは引き続き PC 側の許可が必要です。",
    redirecting: "この PC を安全に開いています…",
    continue: "自動的に開かない場合は、ここを選択してください。",
    invalid: "接続コードが無効、使用済み、または期限切れです。Windows PC で新しいコードを作成してください。"
  }
};

export class ConnectionCodeBroker {
  constructor(state) {
    this.state = state;
  }

  async fetch(request) {
    const url = new URL(request.url);
    if (request.method === "POST" && url.pathname === "/register") {
      const payload = await request.json();
      const code = normalizeCode(payload.code);
      const targetUrl = String(payload.targetUrl || "");
      const expiresAt = Number(payload.expiresAt || 0);
      if (!code || !targetUrl || !Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
        return json({ error: "invalid_request" }, 400);
      }

      const key = `code:${code}`;
      const existing = await this.state.storage.get(key);
      if (existing && Number(existing.expiresAt || 0) > Date.now()) {
        return json({ error: "code_collision" }, 409);
      }

      await this.state.storage.put(key, { targetUrl, expiresAt });
      return json({ code, expiresAt });
    }

    if (request.method === "POST" && url.pathname === "/resolve") {
      const payload = await request.json();
      const code = normalizeCode(payload.code);
      if (!code) return json({ error: "invalid_code" }, 404);

      const key = `code:${code}`;
      const stored = await this.state.storage.get(key);
      if (!stored || Number(stored.expiresAt || 0) <= Date.now()) {
        await this.state.storage.delete(key);
        return json({ error: "code_unavailable" }, 404);
      }

      await this.state.storage.delete(key);
      return json({ targetUrl: stored.targetUrl });
    }

    return json({ error: "not_found" }, 404);
  }
}

export default {
  fetch(request, env) {
    return handleRequest(request, env);
  }
};

export async function handleRequest(request, env) {
  const url = new URL(request.url);
  if (url.pathname === "/api/connect-codes" && request.method === "POST") {
    return registerConnectionCode(request, env);
  }
  if (url.pathname === "/connect" && request.method === "POST") {
    return resolveConnectionCode(request, env);
  }
  if (url.pathname === "/" && (request.method === "GET" || request.method === "HEAD")) {
    return landingPage(request, env);
  }
  return new Response("Not found", { status: 404, headers: securityHeaders("text/plain; charset=utf-8") });
}

async function registerConnectionCode(request, env) {
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("application/json")) {
    return json({ error: "json_required" }, 415);
  }

  let payload;
  try {
    payload = await request.json();
  } catch {
    return json({ error: "invalid_json" }, 400);
  }

  const code = normalizeCode(payload.code);
  const targetUrl = normalizeTargetUrl(payload.publicUrl, env.PUBLIC_BASE_DOMAIN);
  if (!code || !targetUrl) {
    return json({ error: "invalid_request" }, 400);
  }

  if (!(await verifyTargetEndpoint(targetUrl))) {
    return json({ error: "unverified_endpoint" }, 422);
  }

  const expiresAt = Date.now() + getTtlMilliseconds(env);
  const response = await callBroker(env, "/register", { code, targetUrl, expiresAt });
  if (!response.ok) return response;
  return json({ code, expiresAt: new Date(expiresAt).toISOString() }, 201);
}

async function resolveConnectionCode(request, env) {
  const form = await request.formData();
  const code = normalizeCode(form.get("code"));
  const locale = localeFor(request, new URL(request.url));
  if (!code) return landingPage(request, env, translations[locale].invalid, 400);

  const response = await callBroker(env, "/resolve", { code });
  if (!response.ok) return landingPage(request, env, translations[locale].invalid, 404);
  const payload = await response.json();
  const target = new URL(payload.targetUrl);
  target.pathname = "/index.html";
  target.searchParams.set("eink", "1");
  target.searchParams.set("source", "connection-code");
  target.searchParams.set("autopair", "1");
  target.searchParams.set("lang", locale);
  return connectionRedirectPage(locale, target.toString());
}

async function callBroker(env, path, payload) {
  const id = env.CONNECTION_CODES.idFromName("vibedeck-connection-code-broker");
  const stub = env.CONNECTION_CODES.get(id);
  return stub.fetch(`https://connection-code.internal${path}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(payload)
  });
}

async function verifyTargetEndpoint(targetUrl) {
  try {
    const endpoint = new URL("/api/connect", targetUrl);
    const response = await fetch(endpoint.toString(), { headers: { accept: "application/json" } });
    if (!response.ok) return false;
    if (new URL(response.url).hostname.toLowerCase() !== endpoint.hostname.toLowerCase()) return false;
    const info = await response.json();
    const reportedUrl = normalizeTargetUrl(info.PublicUrl || info.publicUrl, endpoint.hostname.split(".").slice(1).join("."));
    return Boolean(info.UsesTrustedPublicUrl ?? info.usesTrustedPublicUrl) && reportedUrl === targetUrl;
  } catch {
    return false;
  }
}

function normalizeTargetUrl(value, baseDomain) {
  try {
    const endpoint = new URL(String(value || ""));
    const normalizedBaseDomain = String(baseDomain || "").trim().toLowerCase().replace(/^\.+|\.+$/g, "");
    const hostPattern = new RegExp(`^vd-[0-9a-f]{16}\\.${escapeRegExp(normalizedBaseDomain)}$`, "i");
    if (endpoint.protocol !== "https:" || endpoint.port || endpoint.username || endpoint.password ||
      endpoint.pathname !== "/" || endpoint.search || endpoint.hash || !hostPattern.test(endpoint.hostname)) {
      return "";
    }
    return `https://${endpoint.hostname.toLowerCase()}/`;
  } catch {
    return "";
  }
}

function normalizeCode(value) {
  const code = String(value || "").toUpperCase().replace(/[^A-Z0-9]/g, "");
  return CODE_PATTERN.test(code) ? code : "";
}

function getTtlMilliseconds(env) {
  const candidate = Number.parseInt(env.CODE_TTL_SECONDS || "", 10);
  const seconds = Number.isFinite(candidate)
    ? Math.min(MAX_TTL_SECONDS, Math.max(MIN_TTL_SECONDS, candidate))
    : DEFAULT_TTL_SECONDS;
  return seconds * 1000;
}

function localeFor(request, url) {
  const requested = url.searchParams.get("lang");
  if (requested === "zh-Hant" || requested === "en" || requested === "ja") return requested;
  const accepted = request.headers.get("accept-language") || "";
  if (/\bja(?:[-_,;]|$)/i.test(accepted)) return "ja";
  if (/\bzh(?:[-_,;]|$)/i.test(accepted)) return "zh-Hant";
  return "en";
}

function landingPage(request, env, error = "", status = 200) {
  const locale = localeFor(request, new URL(request.url));
  const copy = translations[locale];
  const errorBlock = error ? `<p class="error" role="alert">${escapeHtml(error)}</p>` : "";
  const page = `<!doctype html><html lang="${locale}" dir="ltr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="theme-color" content="#101820"><title>${escapeHtml(copy.title)}</title><style>body{margin:0;background:#101820;color:#edf4fb;font:18px/1.5 system-ui,sans-serif}.shell{max-width:560px;margin:0 auto;padding:48px 24px}.card{padding:28px;border:1px solid #324559;border-radius:18px;background:#17232e}.eyebrow{margin:0 0 12px;color:#8fd1ff;font-size:13px;font-weight:700;letter-spacing:.08em}.intro,.note{color:#b9c8d6}.note{font-size:14px}.error{padding:12px 14px;border:1px solid #d98b81;border-radius:10px;background:#482522;color:#ffe9e4}label{display:grid;gap:8px;margin:24px 0 14px;font-weight:700}input,button{box-sizing:border-box;width:100%;min-height:52px;border-radius:10px;font:inherit}input{border:1px solid #70869a;padding:10px 14px;background:#0d151c;color:#fff;text-transform:uppercase;letter-spacing:.1em}button{border:0;background:#dbeeff;color:#10202d;font-weight:800}button:disabled{cursor:wait;opacity:.65}</style></head><body><main class="shell"><section class="card"><p class="eyebrow">${escapeHtml(copy.eyebrow)}</p><h1>${escapeHtml(copy.title)}</h1><p class="intro">${escapeHtml(copy.intro)}</p>${errorBlock}<form method="post" action="/connect?lang=${encodeURIComponent(locale)}" data-once><label>${escapeHtml(copy.label)}<input name="code" inputmode="text" autocomplete="one-time-code" autocapitalize="characters" spellcheck="false" maxlength="9" pattern="[A-Za-z2-9-]{8,9}" placeholder="${escapeHtml(copy.placeholder)}" required autofocus></label><button type="submit">${escapeHtml(copy.submit)}</button></form><p class="note">${escapeHtml(copy.note)}</p></section></main><script>${LANDING_SUBMIT_GUARD}</script></body></html>`;
  return new Response(request.method === "HEAD" ? null : page, { status, headers: securityHeaders("text/html; charset=utf-8") });
}

function connectionRedirectPage(locale, targetUrl) {
  const copy = translations[locale];
  const safeTargetUrl = escapeHtml(targetUrl);
  const page = `<!doctype html><html lang="${locale}" dir="ltr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta http-equiv="refresh" content="0;url=${safeTargetUrl}"><meta name="theme-color" content="#101820"><title>${escapeHtml(copy.title)}</title><style>body{margin:0;background:#101820;color:#edf4fb;font:18px/1.5 system-ui,sans-serif}.shell{max-width:560px;margin:0 auto;padding:48px 24px}.card{padding:28px;border:1px solid #324559;border-radius:18px;background:#17232e}.eyebrow{margin:0 0 12px;color:#8fd1ff;font-size:13px;font-weight:700;letter-spacing:.08em}a{color:#10202d;display:inline-block;margin-top:14px;padding:12px 16px;border-radius:10px;background:#dbeeff;font-weight:800;text-decoration:none}</style></head><body><main class="shell"><section class="card"><p class="eyebrow">${escapeHtml(copy.eyebrow)}</p><h1>${escapeHtml(copy.redirecting)}</h1><a href="${safeTargetUrl}">${escapeHtml(copy.continue)}</a></section></main></body></html>`;
  return new Response(page, { status: 200, headers: securityHeaders("text/html; charset=utf-8") });
}

function json(value, status = 200) {
  return new Response(JSON.stringify(value), { status, headers: securityHeaders("application/json; charset=utf-8") });
}

function securityHeaders(contentType) {
  return {
    "content-type": contentType,
    "cache-control": "no-store",
    "content-security-policy": `default-src 'none'; style-src 'unsafe-inline'; script-src ${LANDING_SUBMIT_GUARD_CSP_HASH}; form-action 'self'; base-uri 'none'; frame-ancestors 'none'`,
    "referrer-policy": "no-referrer",
    "x-content-type-options": "nosniff",
    "x-frame-options": "DENY"
  };
}

function escapeHtml(value) {
  return String(value || "").replace(/[&<>\"']/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" })[character]);
}

function escapeRegExp(value) {
  return String(value || "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
