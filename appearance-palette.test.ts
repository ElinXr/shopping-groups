import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const css = readFileSync(new URL("../src/theme.css", import.meta.url), "utf8");
const palettes = [...css.matchAll(/(:root(?:\[data-theme="dark"\])?)\s*\{([^}]+)\}/g)].map(match => ({ name: match[1], values: Object.fromEntries([...match[2].matchAll(/--([a-z-]+):\s*(#[0-9a-f]{6,8});/g)].map(item => [item[1], item[2]])) }));
function luminance(color: string) {
  const rgb = [1, 3, 5].map(at => parseInt(color.slice(at, at + 2), 16) / 255).map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4);
  return .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2];
}
function contrast(a: string, b: string) { const x = luminance(a), y = luminance(b); return (Math.max(x, y) + .05) / (Math.min(x, y) + .05); }
for (const palette of palettes) test(`${palette.name} palette keeps readable text and controls on every main surface`, () => {
  const p = palette.values;
  const styles = readFileSync(new URL("../src/styles.css", import.meta.url), "utf8");
  for (const match of styles.matchAll(/var\(--([a-z-]+)\)/g)) assert.ok(p[match[1]], `Missing theme token: ${match[1]}`);
  assert.ok(contrast(p.focus, p.surface) >= 3, "Keyboard focus remains visible");
  for (const foreground of ["text", "text-secondary", "muted", "green"])
    for (const background of ["page", "surface", "surface-soft", "surface-accent", "auth-surface"])
      assert.ok(contrast(p[foreground], p[background]) >= 4.5, `${foreground} on ${background}: ${contrast(p[foreground], p[background]).toFixed(2)}`);
  for (const background of ["primary-bg", "primary-hover", "progress"]) assert.ok(contrast(p["on-primary"], p[background]) >= 4.5, `button/purchase text on ${background}`);
  assert.ok(contrast(p["danger-text"], p["danger-bg"]) >= 4.5); assert.ok(contrast(p["warning-text"], p["warning-bg"]) >= 4.5);
});
test("mobile login controls avoid small text while manual browser zoom stays available", () => {
  const styles = readFileSync(new URL("../src/styles.css", import.meta.url), "utf8");
  const sizes = [...styles.matchAll(/\.auth-form input\s*\{[^}]*font-size:\s*(\d+)px/g)].map(match => Number(match[1]));
  assert.ok(sizes.length > 0 && sizes.every(size => size >= 16));
  const html = readFileSync(new URL("../index.html", import.meta.url), "utf8");
  assert.ok(!/user-scalable\s*=\s*(no|0)|maximum-scale\s*=\s*1/i.test(html));
  assert.ok(html.indexOf("theme-init.js") < html.indexOf("/src/main.tsx"));
});
