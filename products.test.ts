import { test } from "node:test";
import assert from "node:assert/strict";
import { parseQuantity, productAmount, persistProduct, type ProductDraft } from "../src/products.ts";
import type { Product } from "../src/types.ts";
const draft: ProductDraft = { text: "Мляко", quantity: 1.5, unit: "l" };
const item: Product = { id: "milk", text: "Мляко", quantity: 2, unit: "l", purchased: false, purchasedBy: null, purchasedAt: null, version: "old-version" };

test("decimal quantity accepts both Bulgarian comma and dot", () => {
  assert.equal(parseQuantity(" 1,5 "), 1.5);
  assert.equal(parseQuantity("0.125"), 0.125);
  assert.equal(parseQuantity("999999.999"), 999999.999);
});
test("invalid quantities cannot be silently rounded or coerced", () => {
  for (const value of ["", "0", "-1", "NaN", "Infinity", "1e2", "1,0001", "1000000", "2,5 kg", ".5", "1,2,3"])
    assert.throws(() => parseQuantity(value), value);
});
test("product amount uses the same decimal and unit labels as the server", () => {
  assert.equal(productAmount({ quantity: 1.5, unit: "kg" }), "1,5 кг");
  assert.equal(productAmount({ quantity: 2, unit: "pack" }), "2 пак.");
});
test("a duplicate warning cancelled by the user never saves or clears the draft", async () => {
  let calls = 0;
  const saved = await persistProduct(async () => { calls++; }, "/list", draft, [item], undefined, () => false);
  assert.equal(saved, false); assert.equal(calls, 0); assert.equal(draft.quantity, 1.5);
});
test("editing excludes the item itself and transmits its version and amount", async () => {
  let sent: unknown;
  await persistProduct(async (path, method, data) => { assert.equal(path, "/list/items/milk"); assert.equal(method, "PUT"); sent = data; }, "/list", draft, [item], item, () => { throw Error("Unexpected duplicate prompt"); });
  assert.deepEqual(sent, { ...draft, version: "old-version", allowDuplicate: false });
});
test("a duplicate appearing on the server during edit can be confirmed and retried once", async () => {
  const requests: any[] = [];
  const saved = await persistProduct(async (_path, _method, data) => { requests.push(data); if (requests.length === 1) throw { code: "duplicate" }; }, "/list", draft, [], item, () => true);
  assert.equal(saved, true); assert.equal(requests.length, 2); assert.equal(requests[1].allowDuplicate, true);
});
test("a server duplicate cancellation retains the form without reporting a saved product", async () => {
  const saved = await persistProduct(async () => { throw { code: "duplicate" }; }, "/list", draft, [], undefined, () => false);
  assert.equal(saved, false);
});
test("version conflicts never silently retry or overwrite another member", async () => {
  let count = 0;
  await assert.rejects(() => persistProduct(async () => { count++; throw Error("Conflict"); }, "/list", draft, [], item, () => true), /Conflict/);
  assert.equal(count, 1);
});
