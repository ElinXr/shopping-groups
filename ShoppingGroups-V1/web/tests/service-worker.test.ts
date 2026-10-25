import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
const source = readFileSync(new URL("../public/sw.js", import.meta.url), "utf8");
function worker(windows: any[] = []) {
  const handlers: Record<string, (event: any) => void> = {};
  const added: string[] = [];
  const deleted: string[] = [];
  const stored: string[] = [];
  const displayed: { title: string; options: any }[] = [];
  const opened: string[] = [];
  const cache = {
    addAll: async (urls: string[]) => added.push(...urls),
    put: async (request: { url: string }) => stored.push(request.url),
  };
  const self = {
    location: { origin: "https://shopping.test" },
    __PRECACHE_ASSETS__: ["/assets/app-test.js"],
    __SHELL_VERSION__: "test",
    skipWaiting: () => {},
    registration: { showNotification: async (title: string, options: any) => { displayed.push({ title, options }); } },
    clients: {
      claim: async () => {},
      matchAll: async () => windows,
      openWindow: async (url: string) => { opened.push(url); },
    },
    addEventListener: (name: string, callback: (event: any) => void) => {
      handlers[name] = callback;
    },
  };
  vm.runInNewContext(source, {
    self,
    caches: {
      open: async () => cache,
      keys: async () => ["shopping-shell-old", "shopping-shell-test"],
      delete: async (key: string) => {
        deleted.push(key);
      },
      match: async () => ({ shell: true }),
    },
    fetch: async () => {
      throw Error("offline");
    },
    URL,
    Promise,
  });
  return { handlers, added, deleted, stored, displayed, opened };
}
test("installation precaches the shell and hashed build assets without user data", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.install({
    waitUntil: (p: Promise<unknown>) => {
      pending = p;
    },
  });
  await pending;
  assert.ok(w.added.includes("/assets/app-test.js"));
  assert.ok(w.added.includes("/index.html"));
  assert.ok(w.added.every((url) => !url.includes("/api") && !url.includes("?")));
});

test("push without a payload immediately displays a generic device notification", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.push({ waitUntil: (p: Promise<unknown>) => { pending = p; } });
  await pending;
  assert.equal(w.displayed.length, 1);
  assert.equal(w.displayed[0].title, "Shopping Groups");
  assert.ok(w.displayed[0].options.body.includes("нови известия"));
  assert.equal(w.stored.length, 0);
});
test("push ignores untrusted notification payload links and shopping text", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.push({
    data: { json: () => ({ body: "private shopping list", url: "https://evil.example" }) },
    waitUntil: (p: Promise<unknown>) => { pending = p; },
  });
  await pending;
  assert.ok(!JSON.stringify(w.displayed).includes("private shopping list"));
  assert.ok(!JSON.stringify(w.displayed).includes("evil.example"));
});
test("notification click opens the same-origin notifications page when no tab exists", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  let closed = false;
  w.handlers.notificationclick({
    notification: { close: () => { closed = true; }, data: { url: "https://evil.example" } },
    waitUntil: (p: Promise<unknown>) => { pending = p; },
  });
  await pending;
  assert.equal(closed, true);
  assert.deepEqual(w.opened, ["/?notifications=1"]);
});
test("notification click focuses an existing app tab and selects its inbox", async () => {
  let focused = false;
  let message: any;
  const w = worker([{ url: "https://shopping.test/", focus: async () => { focused = true; }, postMessage: (data: any) => { message = data; } }]);
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.notificationclick({
    notification: { close: () => {} },
    waitUntil: (p: Promise<unknown>) => { pending = p; },
  });
  await pending;
  assert.equal(focused, true);
  assert.equal(message.type, "open-notifications");
  assert.equal(w.opened.length, 0);
});
test("service worker never intercepts private API, hub, mutation or token URLs", () => {
  const w = worker();
  for (const [path, method] of [
    ["/api/groups", "GET"],
    ["/hubs/events", "GET"],
    ["/?invite=secret", "GET"],
    ["/?reset=secret", "GET"],
    ["/assets/a.js", "POST"],
  ]) {
    let intercepted = false;
    w.handlers.fetch({
      request: { url: "https://shopping.test" + path, method, mode: "navigate" },
      respondWith: () => {
        intercepted = true;
      },
    });
    assert.equal(intercepted, false, `${method} ${path}`);
  }
});
test("offline navigation returns only the cached application shell", async () => {
  const w = worker();
  let response: Promise<unknown> | undefined;
  w.handlers.fetch({
    request: { url: "https://shopping.test/", method: "GET", mode: "navigate" },
    respondWith: (p: Promise<unknown>) => {
      response = p;
    },
  });
  assert.deepEqual(await response, { shell: true });
});
test("activation removes old build caches", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.activate({
    waitUntil: (p: Promise<unknown>) => {
      pending = p;
    },
  });
  await pending;
  assert.deepEqual(w.deleted, ["shopping-shell-old"]);
});

test("decrypted push content displays the author and original addition time", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.push({
    data: { json: () => ({ version: 1, body: "Мария · 25.10.2026 14:25 — Добавено: Мляко", at: "2026-10-25T12:35:00Z", url: "https://evil.example" }) },
    waitUntil: (p: Promise<unknown>) => { pending = p; },
  });
  await pending;
  assert.ok(w.displayed[0].options.body.includes("Мария"));
  assert.ok(w.displayed[0].options.body.includes("14:25"));
  assert.equal(w.displayed[0].options.timestamp, Date.parse("2026-10-25T12:35:00Z"));
  assert.ok(!JSON.stringify(w.displayed).includes("evil.example"));
});
test("malformed push JSON still produces a visible fallback notification", async () => {
  const w = worker();
  let pending: Promise<unknown> = Promise.resolve();
  w.handlers.push({
    data: { json: () => { throw new Error("malformed"); } },
    waitUntil: (p: Promise<unknown>) => { pending = p; },
  });
  await pending;
  assert.equal(w.displayed.length, 1);
});