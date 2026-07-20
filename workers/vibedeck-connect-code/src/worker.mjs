const DEFAULT_TTL_SECONDS = 600;
const MIN_TTL_SECONDS = 60;
const MAX_TTL_SECONDS = 900;
const CODE_PATTERN = /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$/;
const INSTALLATION_PATTERN = /^vd-[0-9a-f]{16}$/;
const PROVISIONING_SECRET_PATTERN = /^[A-Za-z0-9_-]{43}$/;
const CLOUDFLARE_API_BASE = "https://api.cloudflare.com/client/v4";
const LANDING_SUBMIT_GUARD = 'document.addEventListener("submit",e=>{const f=e.target;if(!f.matches("form[data-once]"))return;if(f.dataset.submitting==="1"){e.preventDefault();return}f.dataset.submitting="1";f.querySelector("button[type=submit]")?.setAttribute("disabled","")});';
const LANDING_SUBMIT_GUARD_CSP_HASH = "'sha256-bfXKPBvv3fl+jHsvWGd3kmxKB0McbscPTDLop6BifXY='";

const translations = {
  "zh-Hant": {
    title: "連接 VibeDeck",
    eyebrow: "VIBEDECK · 裝置連線",
    intro: "在 Windows 電腦上產生一次性裝置連線碼，並在這裡輸入。",
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
    eyebrow: "VIBEDECK · DEVICE CONNECTION",
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
    eyebrow: "VIBEDECK · デバイス接続",
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

    if (request.method === "POST" && url.pathname === "/rate") {
      const payload = await request.json();
      const scope = String(payload.scope || "").replace(/[^a-z0-9-]/gi, "").slice(0, 32);
      const subject = String(payload.subject || "").replace(/[^a-f0-9]/gi, "").slice(0, 64);
      const limit = Math.min(1000, Math.max(1, Number(payload.limit || 1)));
      const windowMilliseconds = Math.min(86_400_000, Math.max(10_000, Number(payload.windowMilliseconds || 60_000)));
      if (!scope || !subject) return json({ error: "invalid_rate_key" }, 400);

      const key = `rate:${scope}:${subject}`;
      const now = Date.now();
      let rate = await this.state.storage.get(key);
      if (!rate || Number(rate.expiresAt || 0) <= now) {
        rate = { count: 0, expiresAt: now + windowMilliseconds };
      }
      if (Number(rate.count || 0) >= limit) {
        return json({ error: "rate_limited", retryAfter: Math.max(1, Math.ceil((rate.expiresAt - now) / 1000)) }, 429);
      }

      rate.count = Number(rate.count || 0) + 1;
      await this.state.storage.put(key, rate);
      return json({ allowed: true, remaining: Math.max(0, limit - rate.count), expiresAt: rate.expiresAt });
    }

    return json({ error: "not_found" }, 404);
  }
}

export class InstallationBroker {
  constructor(state, env) {
    this.state = state;
    this.env = env;
  }

  async fetch(request) {
    const url = new URL(request.url);
    if (request.method !== "POST" || url.pathname !== "/provision") {
      return json({ error: "not_found" }, 404);
    }

    const payload = await request.json();
    const installationId = normalizeInstallationId(payload.installationId);
    const secretHash = String(payload.secretHash || "").toLowerCase();
    const ipHash = String(payload.ipHash || "").toLowerCase();
    if (!installationId || !/^[a-f0-9]{64}$/.test(secretHash) || !/^[a-f0-9]{64}$/.test(ipHash)) {
      return json({ error: "invalid_request" }, 400);
    }

    const key = `installation:${installationId}`;
    const existing = await this.state.storage.get(key);
    if (existing && !timingSafeEqual(String(existing.secretHash || ""), secretHash)) {
      return json({ error: "installation_claimed" }, 409);
    }

    if (!existing) {
      const perIp = await this.consumeRate(`provision-ip:${ipHash}`, 3, 86_400_000);
      const global = await this.consumeRate("provision-global", 40, 86_400_000);
      if (!perIp || !global) return json({ error: "rate_limited" }, 429);
    }

    try {
      const provisioned = await ensureCloudflareTunnel(this.env, installationId, existing?.tunnelId || "");
      await this.state.storage.put(key, {
        secretHash,
        tunnelId: provisioned.tunnelId,
        publicUrl: provisioned.publicUrl,
        updatedAt: new Date().toISOString()
      });
      return json({ installationId, ...provisioned }, existing ? 200 : 201);
    } catch (error) {
      return json({ error: error?.code || "cloudflare_provisioning_failed" }, 503);
    }
  }

  async consumeRate(key, limit, windowMilliseconds) {
    const now = Date.now();
    let rate = await this.state.storage.get(key);
    if (!rate || Number(rate.expiresAt || 0) <= now) {
      rate = { count: 0, expiresAt: now + windowMilliseconds };
    }
    if (Number(rate.count || 0) >= limit) return false;
    rate.count = Number(rate.count || 0) + 1;
    await this.state.storage.put(key, rate);
    return true;
  }
}

export default {
  fetch(request, env) {
    return handleRequest(request, env);
  }
};

export async function handleRequest(request, env) {
  const url = new URL(request.url);
  if (url.pathname === "/api/installations/provision" && request.method === "POST") {
    return provisionInstallation(request, env);
  }
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
  const rateLimited = await enforceRateLimit(request, env, "connect-register", 30, 60_000);
  if (rateLimited) return rateLimited;
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
  const rateLimited = await enforceRateLimit(request, env, "connect-resolve", 20, 60_000);
  if (rateLimited) {
    const locale = localeFor(request, new URL(request.url));
    return landingPage(request, env, translations[locale].invalid, 429);
  }
  const form = await request.formData();
  const code = normalizeCode(form.get("code"));
  const locale = localeFor(request, new URL(request.url));
  if (!code) return landingPage(request, env, translations[locale].invalid, 400);

  const response = await callBroker(env, "/resolve", { code });
  if (!response.ok) return landingPage(request, env, translations[locale].invalid, 404);
  const payload = await response.json();
  const target = new URL(payload.targetUrl);
  target.pathname = "/index.html";
  target.searchParams.set("source", "connection-code");
  target.searchParams.set("autopair", "1");
  target.searchParams.set("lang", locale);
  return connectionRedirectPage(locale, target.toString());
}

async function provisionInstallation(request, env) {
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("application/json")) {
    return json({ error: "json_required" }, 415);
  }
  if (!isProvisioningConfigured(env)) {
    return json({ error: "provisioning_not_configured" }, 503);
  }

  let payload;
  try {
    payload = await request.json();
  } catch {
    return json({ error: "invalid_json" }, 400);
  }

  const installationId = normalizeInstallationId(payload.installationId);
  const provisioningSecret = String(payload.provisioningSecret || "");
  if (!installationId || !PROVISIONING_SECRET_PATTERN.test(provisioningSecret)) {
    return json({ error: "invalid_request" }, 400);
  }

  const secretHash = await sha256Hex(provisioningSecret);
  const ipHash = await sha256Hex(clientAddress(request));
  const id = env.INSTALLATIONS.idFromName("vibedeck-installation-broker");
  const stub = env.INSTALLATIONS.get(id);
  return stub.fetch("https://installation.internal/provision", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ installationId, secretHash, ipHash })
  });
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

async function enforceRateLimit(request, env, scope, limit, windowMilliseconds) {
  if (!env.CONNECTION_CODES) return null;
  const response = await callBroker(env, "/rate", {
    scope,
    subject: await sha256Hex(clientAddress(request)),
    limit,
    windowMilliseconds
  });
  return response.status === 429 ? response : null;
}

async function ensureCloudflareTunnel(env, installationId, knownTunnelId) {
  const hostname = `${installationId}.${normalizeBaseDomain(env.PUBLIC_BASE_DOMAIN)}`;
  const tunnelName = `vibedeck-${installationId}`;
  let tunnelId = String(knownTunnelId || "");
  let tunnelToken = "";

  if (!tunnelId) {
    const listed = await cloudflareApi(env, `/accounts/${env.CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel?name=${encodeURIComponent(tunnelName)}&is_deleted=false&per_page=10`);
    const match = Array.isArray(listed) ? listed.find(tunnel => tunnel?.name === tunnelName && tunnel?.id) : null;
    tunnelId = String(match?.id || "");
  }

  if (!tunnelId) {
    const created = await cloudflareApi(env, `/accounts/${env.CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel`, {
      method: "POST",
      body: JSON.stringify({ name: tunnelName, config_src: "cloudflare" })
    });
    tunnelId = String(created?.id || "");
    tunnelToken = String(created?.token || "");
  }
  if (!/^[a-f0-9-]{36}$/i.test(tunnelId)) throw provisioningError("invalid_tunnel_response");

  await cloudflareApi(env, `/accounts/${env.CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel/${tunnelId}/configurations`, {
    method: "PUT",
    body: JSON.stringify({
      config: {
        ingress: [
          { hostname, service: "http://127.0.0.1:5000" },
          { service: "http_status:404" }
        ]
      }
    })
  });
  await upsertTunnelDns(env, hostname, tunnelId);

  if (!tunnelToken) {
    tunnelToken = String(await cloudflareApi(env, `/accounts/${env.CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel/${tunnelId}/token`) || "");
  }
  if (tunnelToken.length < 80) throw provisioningError("invalid_tunnel_token");

  return {
    publicUrl: `https://${hostname}/`,
    tunnelId,
    tunnelToken
  };
}

async function upsertTunnelDns(env, hostname, tunnelId) {
  const zoneId = await resolveZoneId(env);
  const target = `${tunnelId}.cfargotunnel.com`;
  const records = await cloudflareApi(env, `/zones/${zoneId}/dns_records?type=CNAME&name=${encodeURIComponent(hostname)}&per_page=10`);
  const existing = Array.isArray(records) ? records[0] : null;
  const body = JSON.stringify({ type: "CNAME", name: hostname, content: target, proxied: true, ttl: 1 });
  if (existing?.id) {
    await cloudflareApi(env, `/zones/${zoneId}/dns_records/${existing.id}`, { method: "PATCH", body });
    return;
  }
  await cloudflareApi(env, `/zones/${zoneId}/dns_records`, { method: "POST", body });
}

async function resolveZoneId(env) {
  const configuredZoneId = String(env.CLOUDFLARE_ZONE_ID || "");
  if (/^[a-f0-9]{32}$/i.test(configuredZoneId)) return configuredZoneId;
  const domain = normalizeBaseDomain(env.PUBLIC_BASE_DOMAIN);
  const zones = await cloudflareApi(env, `/zones?name=${encodeURIComponent(domain)}&status=active&per_page=5`);
  const zone = Array.isArray(zones) ? zones.find(candidate => candidate?.name === domain && candidate?.id) : null;
  if (!zone?.id) throw provisioningError("cloudflare_zone_not_found");
  return zone.id;
}

async function cloudflareApi(env, path, init = {}) {
  const response = await fetch(`${CLOUDFLARE_API_BASE}${path}`, {
    ...init,
    headers: {
      authorization: `Bearer ${env.CLOUDFLARE_API_TOKEN}`,
      "content-type": "application/json",
      ...(init.headers || {})
    }
  });
  let payload;
  try {
    payload = await response.json();
  } catch {
    throw provisioningError("cloudflare_invalid_response");
  }
  if (!response.ok || payload?.success !== true) {
    throw provisioningError("cloudflare_api_failed");
  }
  return payload.result;
}

function isProvisioningConfigured(env) {
  return Boolean(env.INSTALLATIONS &&
    /^[a-f0-9]{32}$/i.test(String(env.CLOUDFLARE_ACCOUNT_ID || "")) &&
    /^[a-f0-9]{32}$/i.test(String(env.CLOUDFLARE_ZONE_ID || "")) &&
    String(env.CLOUDFLARE_API_TOKEN || "").length >= 20 &&
    normalizeBaseDomain(env.PUBLIC_BASE_DOMAIN));
}

function provisioningError(code) {
  const error = new Error(code);
  error.code = code;
  return error;
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

function normalizeInstallationId(value) {
  const installationId = String(value || "").trim().toLowerCase();
  return INSTALLATION_PATTERN.test(installationId) ? installationId : "";
}

function normalizeBaseDomain(value) {
  const domain = String(value || "").trim().toLowerCase().replace(/^\.+|\.+$/g, "");
  return /^[a-z0-9.-]+\.[a-z]{2,}$/i.test(domain) ? domain : "";
}

function clientAddress(request) {
  return String(request.headers.get("cf-connecting-ip") || request.headers.get("x-forwarded-for") || "unknown")
    .split(",")[0]
    .trim()
    .slice(0, 80);
}

async function sha256Hex(value) {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(String(value || "")));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}

function timingSafeEqual(left, right) {
  if (left.length !== right.length) return false;
  let difference = 0;
  for (let index = 0; index < left.length; index += 1) {
    difference |= left.charCodeAt(index) ^ right.charCodeAt(index);
  }
  return difference === 0;
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
