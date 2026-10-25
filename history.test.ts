import { test } from "node:test";
import assert from "node:assert/strict";
import { emptyHistory, mergeHistory, refreshHistory, appendHistory } from "../src/history.ts";
const records = (first: number, count: number) => Array.from({ length: count }, (_, i) => ({ id: first - i, read: false }));
test("history refresh preserves previously loaded older pages", () => {
  const first = refreshHistory(emptyHistory(), records(100, 50));
  const loaded = appendHistory(first, records(50, 50));
  const refreshed = refreshHistory(loaded, records(101, 50));
  assert.equal(refreshed.items.length, 101); assert.equal(refreshed.items.at(-1)?.id, 1);
});
test("new records replace older read state and page overlap is deduplicated", () => {
  assert.deepEqual(mergeHistory([{ id: 2, read: true }], [{ id: 2, read: false }, { id: 1, read: false }]), [{ id: 2, read: true }, { id: 1, read: false }]);
});
test("an empty last page ends Load More, including after a background refresh", () => {
  const ended = appendHistory(refreshHistory(emptyHistory(), records(100, 50)), []);
  assert.equal(refreshHistory(ended, records(101, 50)).hasMore, false);
});
test("a gap larger than one page resets the cursor instead of skipping unseen history", () => {
  const old = refreshHistory(emptyHistory(), records(50, 50));
  const next = refreshHistory(old, records(150, 50));
  assert.equal(next.items.length, 50); assert.equal(next.items.at(-1)?.id, 101); assert.equal(next.hasMore, true);
});
