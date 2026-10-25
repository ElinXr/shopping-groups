import type { Product } from "./types.ts";
export function normalize(value: string) {
  return value.normalize("NFKC").trim().replace(/\s+/gu, " ").toLocaleUpperCase("en-US");
}
export function duplicate(items: Product[], text: string, except?: string) {
  return items.some((x) => x.id !== except && normalize(x.text) === normalize(text));
}
export function progress(items: Product[]) {
  return { purchased: items.filter((x) => x.purchased).length, total: items.length };
}
export function date(value: string) {
  return new Intl.DateTimeFormat("bg-BG", { dateStyle: "short", timeStyle: "short", timeZone: "Europe/Sofia" }).format(
    new Date(value),
  );
}
export function safeInvite(value: string | null) {
  return value && /^[a-f0-9]{64}$/i.test(value) ? value : null;
}
