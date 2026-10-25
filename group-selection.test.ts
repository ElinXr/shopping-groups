import { test } from "node:test";
import assert from "node:assert/strict";
import { chooseGroup, searchGroups, preferredGroup, rememberGroup } from "../src/group-selection.ts";
import type { Group } from "../src/types.ts";
const group = (id: string, name: string, disabled = false): Group => ({ id, name, disabled, role: "member", version: "1" });

test("a user's selected group survives reordering or a page refresh", () => {
  const groups = [group("a", "А"), group("b", "Б")];
  assert.equal(chooseGroup(groups, "b")?.id, "b");
  assert.equal(chooseGroup(groups.toReversed(), "b")?.id, "b");
});
test("a removed or disabled preference falls back to an accessible active group", () => {
  const groups = [group("off", "А", true), group("on", "Б")];
  assert.equal(chooseGroup(groups, "off")?.id, "on");
  assert.equal(chooseGroup(groups, "removed")?.id, "on");
  assert.equal(chooseGroup([], "removed"), undefined);
  assert.equal(chooseGroup([groups[0]], "off")?.disabled, true);
});
test("group search accepts Bulgarian case and independent words with extra spaces", () => {
  const groups = [group("a", "Нашият дом и семейство"), group("b", "На работа")];
  assert.deepEqual(searchGroups(groups, " СЕМЕЙСТВО   ДОМ ").map(g => g.id), ["a"]);
  assert.deepEqual(searchGroups(groups, "несъществуваща"), []);
});
test("available groups sort before disabled groups and full long names remain searchable", () => {
  const name = "Приятели за голямото пътуване и покупките в планината";
  const groups = [group("off", "А", true), group("long", name)];
  assert.deepEqual(searchGroups(groups, "").map(g => g.id), ["long", "off"]);
  assert.equal(searchGroups(groups, "планината")[0].name, name);
});
test("the last selected group is stored separately for each signed-in account", () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, "localStorage"); const values = new Map<string, string>();
  Object.defineProperty(globalThis, "localStorage", { configurable: true, value: { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value) } });
  try { rememberGroup("maria", "home"); rememberGroup("ivan", "work"); assert.equal(preferredGroup("maria"), "home"); assert.equal(preferredGroup("ivan"), "work"); assert.equal(preferredGroup("new-account"), ""); }
  finally { if (original) Object.defineProperty(globalThis, "localStorage", original); else Reflect.deleteProperty(globalThis, "localStorage"); }
});
test("restricted browser storage never blocks group selection", () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
  Object.defineProperty(globalThis, "localStorage", { configurable: true, get: () => { throw Error("Blocked"); } });
  try { assert.equal(preferredGroup("maria"), ""); assert.doesNotThrow(() => rememberGroup("maria", "home")); }
  finally { if (original) Object.defineProperty(globalThis, "localStorage", original); else Reflect.deleteProperty(globalThis, "localStorage"); }
});
