import { test } from "node:test";
import assert from "node:assert/strict";
import { normalize, duplicate, progress, safeInvite } from "../src/helpers.ts";
import { decodeFrames, RECORD_SEPARATOR } from "../src/realtime.ts";
import type { Product } from "../src/types.ts";
const item = (text: string, id = "1", purchased = false): Product => ({
  id,
  text,
  quantity: 1,
  unit: "pcs",
  purchased,
  version: "v",
  purchasedBy: null,
  purchasedAt: null,
});
test("duplicate normalization matches server case and whitespace rules", () =>
  assert.equal(normalize("  прясно\tмляко "), normalize("ПРЯСНО  МЛЯКО")));
test("duplicate normalization handles compatibility characters", () =>
  assert.equal(normalize("ＡＢＣ"), normalize("abc")));
test("warn for already purchased duplicate too", () =>
  assert.equal(duplicate([item("мляко", "1", true)], "МЛЯКО"), true));
test("editing item does not warn about itself", () =>
  assert.equal(duplicate([item("мляко")], "мляко", "1"), false));
test("editing item warns if another matching item exists", () =>
  assert.equal(duplicate([item("мляко"), item("мляко", "2")], "МЛЯКО", "1"), true));
test("different quantities are distinct free text", () =>
  assert.equal(duplicate([item("мляко 1 л")], "мляко 2 л"), false));
test("empty list has zero progress", () =>
  assert.deepEqual(progress([]), { purchased: 0, total: 0 }));
test("progress includes exactly purchased products", () =>
  assert.deepEqual(progress([item("a"), item("b", "2", true)]), { purchased: 1, total: 2 }));
test("invitation accepts exactly 256-bit hex token", () =>
  assert.equal(safeInvite("A".repeat(64)), "A".repeat(64)));
test("invitation rejects injected URLs and wrong lengths", () => {
  assert.equal(safeInvite("https://evil.example"), null);
  assert.equal(safeInvite("A".repeat(63)), null);
  assert.equal(safeInvite(null), null);
});
test("SignalR JSON protocol decodes handshake and batched invocations", () =>
  assert.deepEqual(
    decodeFrames(
      "{}" +
        RECORD_SEPARATOR +
        '{"type":1,"target":"Changed","arguments":[{"groupId":"g"}]}' +
        RECORD_SEPARATOR,
    ),
    [{}, { type: 1, target: "Changed", arguments: [{ groupId: "g" }] }],
  ));
test("SignalR ping is parsed without being treated as a change", () =>
  assert.deepEqual(decodeFrames('{"type":6}' + RECORD_SEPARATOR), [{ type: 6 }]));
test("SignalR malformed frames fail closed", () =>
  assert.throws(() => decodeFrames("not-json" + RECORD_SEPARATOR)));
