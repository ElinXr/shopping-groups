import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import { accountTheme, applyTheme, cachedTheme, isTheme, LAST_THEME_KEY, rememberTheme, themeKey } from "../src/theme.ts";

function withStorage(action: (values: Map<string, string>) => void) {
  const original = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
  const values = new Map<string, string>();
  Object.defineProperty(globalThis, "localStorage", { configurable: true, value: { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value) } });
  try { action(values); }
  finally { if (original) Object.defineProperty(globalThis, "localStorage", original); else Reflect.deleteProperty(globalThis, "localStorage"); }
}
test("only explicit light or dark themes are accepted", () => {
  assert.ok(isTheme("light")); assert.ok(isTheme("dark"));
  for (const value of ["system", "DARK", "", null, undefined, {}, "<script>"]) assert.equal(isTheme(value), false);
});
test("appearance cache persists independently for each account and server preferences win", () => withStorage(values => {
  rememberTheme("maria", "dark"); rememberTheme("ivan", "light");
  assert.equal(cachedTheme("maria"), "dark"); assert.equal(cachedTheme("ivan"), "light");
  assert.equal(accountTheme("light", "maria"), "light"); assert.equal(accountTheme("dark", "ivan"), "dark");
  assert.equal(values.get(themeKey("maria")), "dark"); assert.equal(values.get(LAST_THEME_KEY), "light");
}));
test("unknown accounts and corrupt browser values never inherit another user's theme", () => withStorage(values => {
  rememberTheme("maria", "dark"); values.set(themeKey("ivan"), "invalid");
  assert.equal(cachedTheme("ivan"), "light"); assert.equal(accountTheme(undefined, "new-user"), "light");
}));
test("restricted local storage cannot prevent restoring a server-saved appearance", () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
  Object.defineProperty(globalThis, "localStorage", { configurable: true, get: () => { throw Error("Blocked"); } });
  try { assert.equal(accountTheme("dark", "maria"), "dark"); assert.equal(cachedTheme("maria"), "light"); assert.doesNotThrow(() => rememberTheme("maria", "dark")); }
  finally { if (original) Object.defineProperty(globalThis, "localStorage", original); else Reflect.deleteProperty(globalThis, "localStorage"); }
});
test("appearance updates the page and browser chrome without replacing app content", () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, "document"); const attributes = new Map<string, string>();
  const fake = { documentElement: { dataset: {} as Record<string, string> }, querySelector: () => ({ setAttribute: (key: string, value: string) => attributes.set(key, value) }) };
  Object.defineProperty(globalThis, "document", { configurable: true, value: fake });
  try { applyTheme("dark"); assert.equal(fake.documentElement.dataset.theme, "dark"); assert.equal(attributes.get("content"), "#111c18"); applyTheme("light"); assert.equal(fake.documentElement.dataset.theme, "light"); }
  finally { if (original) Object.defineProperty(globalThis, "document", original); else Reflect.deleteProperty(globalThis, "document"); }
});
const bootstrap = readFileSync(new URL("../public/theme-init.js", import.meta.url), "utf8");
test("early bootstrap restores a validated persistent appearance before React mounts", () => {
  const document = { documentElement: { dataset: {} as any }, querySelector: () => null };
  vm.runInNewContext(bootstrap, { document, localStorage: { getItem: () => "dark" } });
  assert.equal(document.documentElement.dataset.theme, "dark");
});
test("early bootstrap tolerates missing, corrupt and blocked browser storage", () => {
  for (const value of [null, "invalid", "<script>"]) {
    const document = { documentElement: { dataset: {} as any }, querySelector: () => null };
    vm.runInNewContext(bootstrap, { document, localStorage: { getItem: () => value } });
    assert.equal(document.documentElement.dataset.theme, "light");
  }
  const document = { documentElement: { dataset: {} as any }, querySelector: () => null };
  vm.runInNewContext(bootstrap, { document, localStorage: { getItem: () => { throw Error("Blocked"); } } });
  assert.equal(document.documentElement.dataset.theme, "light");
});
