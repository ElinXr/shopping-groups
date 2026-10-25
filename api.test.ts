import { test } from "node:test";
import assert from "node:assert/strict";
import { api, refreshCsrf, ApiError } from "../src/api.ts";
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

test("a rejected stale CSRF token refreshes once and preserves the exact action", async () => {
  const original = globalThis.fetch; const requests: any[] = [];
  globalThis.fetch = async (url, options) => {
    requests.push({ url, options });
    if (String(url).endsWith("/csrf")) return json({ token: requests.length === 1 ? "old" : "new" });
    return requests.length === 2 ? json({ code: "csrf", title: "Expired" }, 400) : new Response(null, { status: 204 });
  };
  try {
    await refreshCsrf(); await api("/groups", "POST", { name: "Нашият дом" });
    assert.equal(requests.length, 4);
    assert.equal(requests[1].options.body, requests[3].options.body);
    assert.equal(requests[3].options.headers["X-CSRF-TOKEN"], "new");
  } finally { globalThis.fetch = original; }
});
test("permission, validation and concurrency failures never replay an action", async () => {
  const original = globalThis.fetch; let attempts = 0;
  globalThis.fetch = async url => String(url).endsWith("/csrf") ? json({ token: "valid" }) : (attempts++, json({ code: "conflict", title: "Има по-нова промяна." }, 409));
  try { await refreshCsrf(); await assert.rejects(() => api("/item", "PUT", { quantity: 2 }), (error: unknown) => error instanceof ApiError && error.status === 409); assert.equal(attempts, 1); }
  finally { globalThis.fetch = original; }
});
test("a persistent CSRF failure cannot create an infinite retry loop", async () => {
  const original = globalThis.fetch; let attempts = 0;
  globalThis.fetch = async url => String(url).endsWith("/csrf") ? json({ token: "valid" }) : (attempts++, json({ code: "csrf" }, 400));
  try { await refreshCsrf(); await assert.rejects(() => api("/groups", "POST", {}), (error: unknown) => error instanceof ApiError && error.code === "csrf"); assert.equal(attempts, 2); }
  finally { globalThis.fetch = original; }
});
test("connection errors use a readable Bulgarian message without browser stack text", async () => {
  const original = globalThis.fetch;
  globalThis.fetch = async () => { throw new TypeError("Failed to fetch"); };
  try { await assert.rejects(() => api("/groups"), (error: unknown) => error instanceof ApiError && error.code === "network" && error.message.startsWith("Няма връзка със сървъра.")); }
  finally { globalThis.fetch = original; }
});
