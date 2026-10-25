import { test } from "node:test";
import assert from "node:assert/strict";
import { summaryCount, previewProduct, summaryTime, summarySendPath } from "../src/summary-preview.ts";

test("share heading uses natural Bulgarian singular, plural and empty-state text", () => {
  assert.equal(summaryCount(1), "1 промяна за споделяне");
  assert.equal(summaryCount(2), "2 промени за споделяне");
  assert.equal(summaryCount(0), "Всичко вече е споделено");
});
test("known quantities become separate display badges without changing product names", () => {
  assert.deepEqual(previewProduct("Ябълки · 1,5 кг"), { name: "Ябълки", amount: "1,5 кг" });
  assert.deepEqual(previewProduct("Мляко · 2 бр."), { name: "Мляко", amount: "2 бр." });
  assert.deepEqual(previewProduct("Брашно · 0.125 пак."), { name: "Брашно", amount: "0.125 пак." });
});
test("legacy free text remains intact instead of guessing an amount", () => {
  for (const label of ["test", "Мляко 2 л", "Сирене · домашно", "Продукт · 1 кутия", "Продукт · 1,2345 кг"])
    assert.deepEqual(previewProduct(label), { name: label, amount: null });
});
test("separators inside a product name do not swallow its actual amount suffix", () => {
  assert.deepEqual(previewProduct("Сирене · домашно · 0,25 кг"), { name: "Сирене · домашно", amount: "0,25 кг" });
});
test("today and original action times use Bulgarian winter and summer time", () => {
  assert.equal(summaryTime("2026-10-25T20:35:00Z", new Date("2026-10-25T21:00:00Z")), "днес, 22:35");
  assert.equal(summaryTime("2026-07-15T10:00:00Z", new Date("2026-07-15T11:00:00Z")), "днес, 13:00");
});
test("the original date remains visible after midnight and invalid dates fail safely", () => {
  const time = summaryTime("2026-10-25T20:35:00Z", new Date("2026-10-25T22:30:00Z"));
  assert.ok(!time.includes("днес")); assert.ok(time.endsWith("22:35"));
  assert.equal(summaryTime("invalid"), "");
});
test("send requests bind to the preview cursor and reject invalid boundaries", () => {
  assert.equal(summarySendPath("/groups/g/lists/l", 123), "/groups/g/lists/l/notify?through=123");
  for (const cursor of [0, -1, 1.5, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1])
    assert.throws(() => summarySendPath("/list", cursor));
});
