import assert from "node:assert/strict";
import test from "node:test";
import { ConnectionCodeBroker, InstallationBroker, handleRequest } from "../src/worker.mjs";

test("landing page is available in Traditional Chinese", async () => {
  const response = await handleRequest(new Request("https://vibedeck.pp.ua/?lang=zh-Hant"), {});
  const page = await response.text();

  assert.equal(response.status, 200);
  assert.match(page, /連接 VibeDeck/);
  assert.match(page, /form method="post" action="\/connect\?lang=zh-Hant" data-once/);
  assert.match(response.headers.get("content-security-policy"), /default-src 'none'/);
  assert.match(response.headers.get("content-security-policy"), /script-src 'sha256-bfXKPBvv3fl\+jHsvWGd3kmxKB0McbscPTDLop6BifXY='/);
});

test("landing page accepts HEAD preflight requests", async () => {
  const response = await handleRequest(new Request("https://vibedeck.pp.ua/?lang=zh-Hant", { method: "HEAD" }), {});

  assert.equal(response.status, 200);
  assert.equal(await response.text(), "");
  assert.equal(response.headers.get("cache-control"), "no-store");
});

test("registration rejects an endpoint outside the VibeDeck installation hostname", async () => {
  const response = await handleRequest(new Request("https://vibedeck.pp.ua/api/connect-codes", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ code: "ABCD2345", publicUrl: "https://example.test/" })
  }), { PUBLIC_BASE_DOMAIN: "vibedeck.pp.ua" });

  assert.equal(response.status, 400);
  assert.deepEqual(await response.json(), { error: "invalid_request" });
});

test("a connection code resolves once and is then deleted", async () => {
  const values = new Map();
  const broker = new ConnectionCodeBroker({
    storage: {
      get: key => values.get(key),
      put: (key, value) => values.set(key, value),
      delete: key => values.delete(key)
    }
  });
  const environment = {
    CONNECTION_CODES: {
      idFromName: name => name,
      get: () => ({
        fetch: (input, init) => broker.fetch(input instanceof Request ? input : new Request(input, init))
      })
    }
  };
  const expiresAt = Date.now() + 60_000;
  const registered = await broker.fetch(new Request("https://connection-code.internal/register", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ code: "ABCD2345", targetUrl: "https://vd-1234567890abcdef.vibedeck.pp.ua/", expiresAt })
  }));

  assert.equal(registered.status, 200);
  const firstUse = await handleRequest(new Request("https://vibedeck.pp.ua/connect?lang=en", {
    method: "POST",
    body: new URLSearchParams({ code: "ABCD-2345" })
  }), environment);
  const redirectPage = await firstUse.text();
  assert.equal(firstUse.status, 200);
  assert.match(redirectPage, /http-equiv="refresh" content="0;url=https:\/\/vd-1234567890abcdef\.vibedeck\.pp\.ua\/index\.html\?source=connection-code&amp;autopair=1&amp;lang=en"/);
  assert.doesNotMatch(redirectPage, /[?&]eink=/);
  assert.match(redirectPage, /Opening this PC securely/);

  const replay = await handleRequest(new Request("https://vibedeck.pp.ua/connect?lang=en", {
    method: "POST",
    body: new URLSearchParams({ code: "ABCD-2345" })
  }), environment);
  assert.equal(replay.status, 404);
});

test("connection endpoints rate limit repeated requests by client address", async () => {
  const values = new Map();
  const broker = new ConnectionCodeBroker({
    storage: {
      get: key => values.get(key),
      put: (key, value) => values.set(key, value),
      delete: key => values.delete(key)
    }
  });

  for (let attempt = 0; attempt < 20; attempt += 1) {
    const response = await broker.fetch(new Request("https://connection-code.internal/rate", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ scope: "test", subject: "a".repeat(64), limit: 20, windowMilliseconds: 60_000 })
    }));
    assert.equal(response.status, 200);
  }

  const blocked = await broker.fetch(new Request("https://connection-code.internal/rate", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ scope: "test", subject: "a".repeat(64), limit: 20, windowMilliseconds: 60_000 })
  }));
  assert.equal(blocked.status, 429);
});

test("installation provisioning creates a tunnel, ingress, and DNS record", async () => {
  const originalFetch = globalThis.fetch;
  const apiCalls = [];
  const tunnelId = "11111111-2222-4333-8444-555555555555";
  const tunnelToken = `eyJ${"x".repeat(100)}`;
  globalThis.fetch = async (input, init = {}) => {
    const url = new URL(String(input));
    apiCalls.push({ method: init.method || "GET", path: `${url.pathname}${url.search}` });
    let result = {};
    if (url.pathname.endsWith("/cfd_tunnel") && (init.method || "GET") === "GET") result = [];
    if (url.pathname.endsWith("/cfd_tunnel") && init.method === "POST") result = { id: tunnelId, token: tunnelToken };
    if (url.pathname === "/client/v4/zones") result = [{ id: "f".repeat(32), name: "vibedeck.pp.ua" }];
    if (url.pathname.endsWith("/dns_records") && (init.method || "GET") === "GET") result = [];
    return new Response(JSON.stringify({ success: true, result }), {
      status: 200,
      headers: { "content-type": "application/json" }
    });
  };

  try {
    const values = new Map();
    const environment = {
      PUBLIC_BASE_DOMAIN: "vibedeck.pp.ua",
      CLOUDFLARE_ACCOUNT_ID: "a".repeat(32),
      CLOUDFLARE_ZONE_ID: "f".repeat(32),
      CLOUDFLARE_API_TOKEN: "test-token-with-enough-length",
      INSTALLATIONS: null
    };
    const broker = new InstallationBroker({
      storage: {
        get: key => values.get(key),
        put: (key, value) => values.set(key, value)
      }
    }, environment);
    environment.INSTALLATIONS = {
      idFromName: name => name,
      get: () => ({
        fetch: (input, init) => broker.fetch(input instanceof Request ? input : new Request(input, init))
      })
    };

    const response = await handleRequest(new Request("https://vibedeck.pp.ua/api/installations/provision", {
      method: "POST",
      headers: { "content-type": "application/json", "cf-connecting-ip": "203.0.113.8" },
      body: JSON.stringify({
        installationId: "vd-1234567890abcdef",
        provisioningSecret: "A".repeat(43),
        productVersion: "0.1.29",
        platform: "windows-x64"
      })
    }), environment);
    const payload = await response.json();

    assert.equal(response.status, 201);
    assert.equal(payload.publicUrl, "https://vd-1234567890abcdef.vibedeck.pp.ua/");
    assert.equal(payload.tunnelId, tunnelId);
    assert.equal(payload.tunnelToken, tunnelToken);
    assert.ok(apiCalls.some(call => call.method === "PUT" && call.path.includes("/configurations")));
    assert.ok(apiCalls.some(call => call.method === "POST" && call.path.endsWith("/dns_records")));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("installation provisioning stays unavailable without the Cloudflare API secret", async () => {
  const response = await handleRequest(new Request("https://vibedeck.pp.ua/api/installations/provision", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ installationId: "vd-1234567890abcdef", provisioningSecret: "A".repeat(43) })
  }), {
    PUBLIC_BASE_DOMAIN: "vibedeck.pp.ua",
    CLOUDFLARE_ACCOUNT_ID: "a".repeat(32),
    INSTALLATIONS: {}
  });

  assert.equal(response.status, 503);
  assert.deepEqual(await response.json(), { error: "provisioning_not_configured" });
});
