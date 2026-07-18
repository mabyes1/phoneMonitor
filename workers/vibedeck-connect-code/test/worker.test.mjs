import assert from "node:assert/strict";
import test from "node:test";
import { ConnectionCodeBroker, handleRequest } from "../src/worker.mjs";

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
  assert.match(redirectPage, /http-equiv="refresh" content="0;url=https:\/\/vd-1234567890abcdef\.vibedeck\.pp\.ua\/index\.html\?eink=1&amp;source=connection-code&amp;autopair=1&amp;lang=en"/);
  assert.match(redirectPage, /Opening this PC securely/);

  const replay = await handleRequest(new Request("https://vibedeck.pp.ua/connect?lang=en", {
    method: "POST",
    body: new URLSearchParams({ code: "ABCD-2345" })
  }), environment);
  assert.equal(replay.status, 404);
});
