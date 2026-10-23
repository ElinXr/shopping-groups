import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
const source = readFileSync(new URL("../public/sw.js", import.meta.url), "utf8");
function worker() {
  const handlers: Record<string, (event: any) => void> = {};
  const added: string[] = [];
  const deleted: string[] = [];
  const stored: string[] = [];
  const cache = {
    addAll: async (urls: string[]) => added.push(...urls),
    put: async (request: { url: string }) => stored.push(request.url),
  };
  const self = {
    location: { origin: "https://shopping.test" },
    __PRECACHE_ASSETS__: ["/assets/app-test.js"],
    __SHELL_VERSION__: "test",
    skipWaiting: () => {},
    clients: { claim: async () => {} },
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
  return { handlers, added, deleted, stored };
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
