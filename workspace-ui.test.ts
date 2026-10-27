import { test } from "node:test";
import assert from "node:assert/strict";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { component } from "./render-component.ts";
import { listCount, workspaceTitle } from "../src/workspace.ts";
const render = (value: any, props: any) => renderToStaticMarkup(createElement(value, props));
test("the shared brand uses the same vector mark for authentication and application navigation", () => {
  const { Brand, LogoMark } = component("Brand");
  assert.match(render(Brand, {}), /src="\/icon.svg"/); assert.match(render(Brand, { compact: true }), /src="\/icon.svg"/);
  assert.match(render(LogoMark, { size: 56 }), /src="\/icon.svg"/);
  assert.ok(render(Brand, {}).includes("Shopping Groups — начало"));
});
test("list and creation context show the full group name and member count safely", () => {
  const { GroupContext } = component("GroupContext");
  const html = render(GroupContext, { name: 'Нашият дом <приятели>', members: 3, compact: true, label: "Новият списък ще бъде в група" });
  assert.ok(html.includes("Новият списък ще бъде в група")); assert.ok(html.includes("Нашият дом &lt;приятели&gt;"));
  assert.ok(html.includes("3 участници")); assert.ok(!html.includes("<приятели>"));
});
test("the main group picker announces its selected group and exposes a clear create action", () => {
  const { GroupPicker } = component("GroupPicker");
  const html = render(GroupPicker, { groups: [{ id: "home", name: "Нашият дом", role: "owner", memberCount: 3, disabled: false, version: "1" }], selectedId: "home", disabled: false, onSelect() {}, onCreate() {} });
  assert.ok(html.includes("Смени групата. Текуща група: Нашият дом")); assert.ok(html.includes("3 участници · собственик"));
  assert.ok(html.includes("Създай нова група")); assert.ok(html.includes('aria-haspopup="dialog"'));
});
test("appearance controls identify the selected persisted theme", () => {
  const { ThemeSettings } = component("ThemeSettings");
  const html = render(ThemeSettings, { theme: "dark", disabled: false, onChange() {} });
  const selected = [...html.matchAll(/<button[^>]*aria-pressed="true"[^>]*>([\s\S]*?)<\/button>/g)];
  assert.equal(selected.length, 1); assert.ok(selected[0][1].includes("Тъмна")); assert.ok(!selected[0][0].includes("disabled")); assert.ok(html.includes("Запазва се към профила ви"));
});
test("list workspace labels distinguish group lists, archive and trash with correct counts", () => {
  assert.equal(workspaceTitle("active"), "Моите списъци"); assert.equal(workspaceTitle("archived"), "Архивирани списъци"); assert.equal(workspaceTitle("trash"), "Кошче");
  assert.equal(listCount(1), "1 списък"); assert.equal(listCount(3), "3 списъка");
});
