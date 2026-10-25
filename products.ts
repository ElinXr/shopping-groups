import type { Product } from "./types.ts";
import { duplicate } from "./helpers.ts";

export const PRODUCT_UNITS = [
  ["pcs", "бр."], ["kg", "кг"], ["g", "г"], ["l", "л"], ["ml", "мл"], ["pack", "пак."],
] as const;
export type ProductUnit = typeof PRODUCT_UNITS[number][0];
export interface ProductDraft { text: string; quantity: number; unit: ProductUnit }
export type ProductRequest = (path: string, method: string, data: unknown) => Promise<unknown>;

export function parseQuantity(value: string): number {
  const trimmed = value.trim();
  const quantity = Number(trimmed.replace(",", "."));
  if (!/^\d+(?:[.,]\d{1,3})?$/.test(trimmed) || !Number.isFinite(quantity) || quantity <= 0 || quantity > 999999.999)
    throw new Error("Въведете количество над 0 с най-много 3 цифри след запетаята.");
  return quantity;
}
export function productAmount(item: Pick<Product, "quantity" | "unit">): string {
  const label = PRODUCT_UNITS.find(([key]) => key === item.unit)?.[1] ?? "бр.";
  return `${new Intl.NumberFormat("bg-BG", { maximumFractionDigits: 3, useGrouping: false }).format(item.quantity ?? 1)} ${label}`;
}
// Both add and edit use the same confirmation, including a duplicate created by another member meanwhile.
export async function persistProduct(request: ProductRequest, base: string, draft: ProductDraft,
  items: Product[], item: Product | undefined, ask: (message: string) => boolean): Promise<boolean> {
  let allowDuplicate = false;
  if (duplicate(items, draft.text, item?.id)) {
    allowDuplicate = ask("Има продукт със същото име. Да запазим и този отделен ред?");
    if (!allowDuplicate) return false;
  }
  const path = item ? `${base}/items/${item.id}` : `${base}/items`;
  const send = () => request(path, item ? "PUT" : "POST", { ...draft, ...(item ? { version: item.version } : {}), allowDuplicate });
  try { await send(); }
  catch (error) {
    if (!allowDuplicate && error && typeof error === "object" && "code" in error && error.code === "duplicate") {
      if (!ask("Друг участник вече е добавил продукт със същото име. Да запазим отделен ред?")) return false;
      allowDuplicate = true;
      await send();
    } else throw error;
  }
  return true;
}
