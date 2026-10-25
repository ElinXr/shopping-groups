import { test } from "node:test";
import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import { pushDeviceId, renewPushDeviceId, pushOwner, rememberPushOwner } from "../src/push-device.ts";

test("a browser installation keeps one device ID until logout or explicit cleanup rotates it", () => {
  const values = new Map<string, string>();
  Object.defineProperty(globalThis, "localStorage", { configurable: true, value: {
    getItem: (key: string) => values.get(key) || null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key),
  } });
  renewPushDeviceId();
  const before = pushDeviceId();
  assert.equal(pushDeviceId(), before);
  renewPushDeviceId();
  assert.notEqual(pushDeviceId(), before);
});
test("private-browsing storage failure retains a device ID and account binding for the current page", () => {
  Object.defineProperty(globalThis, "localStorage", { configurable: true, get: () => { throw new Error("storage blocked"); } });
  renewPushDeviceId();
  const id = pushDeviceId();
  assert.equal(pushDeviceId(), id);
  rememberPushOwner("user-one");
  assert.equal(pushOwner(), "user-one");
  rememberPushOwner("");
  assert.equal(pushOwner(), "");
});
test("an HTTP development browser without randomUUID can still generate a cryptographically random device ID", () => {
  const original = globalThis.crypto;
  Object.defineProperty(globalThis, "crypto", { configurable: true, value: { getRandomValues: webcrypto.getRandomValues.bind(webcrypto) } });
  try {
    renewPushDeviceId();
    assert.match(pushDeviceId(), /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  } finally { Object.defineProperty(globalThis, "crypto", { configurable: true, value: original }); }
});
