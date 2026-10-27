export type Theme = "light" | "dark";
export const LAST_THEME_KEY = "shopping-appearance-last";
export const themeKey = (userId: string) => `shopping-appearance:${userId}`;
export function isTheme(value: unknown): value is Theme { return value === "light" || value === "dark"; }
export function cachedTheme(userId: string): Theme {
  try { const value = localStorage.getItem(themeKey(userId)); return isTheme(value) ? value : "light"; }
  catch { return "light"; }
}
export function rememberTheme(userId: string, theme: Theme): void {
  try { localStorage.setItem(themeKey(userId), theme); localStorage.setItem(LAST_THEME_KEY, theme); }
  catch { /* The database remains authoritative when browser storage is unavailable. */ }
}
export function accountTheme(value: unknown, userId: string): Theme {
  return isTheme(value) ? value : cachedTheme(userId);
}
export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#111c18" : "#176753");
}
