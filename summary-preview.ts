export function summaryCount(count: number): string {
  if (count === 0) return "Всичко вече е споделено";
  return `${count} ${count === 1 ? "промяна" : "промени"} за споделяне`;
}
export function previewProduct(label: string): { name: string; amount: string | null } {
  // This is display-only: preserve unfamiliar legacy free text rather than guessing an amount.
  const match = /^(.*) · (\d+(?:[.,]\d{1,3})? (?:бр\.|кг|г|л|мл|пак\.))$/u.exec(label);
  return match ? { name: match[1], amount: match[2] } : { name: label, amount: null };
}
export function summaryTime(value: string, now = new Date()): string {
  const at = new Date(value);
  if (!Number.isFinite(at.getTime())) return "";
  const zone = "Europe/Sofia";
  const day = new Intl.DateTimeFormat("bg-BG", { timeZone: zone, dateStyle: "short" });
  const clock = new Intl.DateTimeFormat("bg-BG", { timeZone: zone, hour: "2-digit", minute: "2-digit" });
  const prefix = day.format(at) === day.format(now) ? "днес" : day.format(at);
  return `${prefix}, ${clock.format(at)}`;
}
export function summarySendPath(base: string, through: number): string {
  if (!Number.isSafeInteger(through) || through <= 0) throw new Error("Обновете прегледа на известието и опитайте отново.");
  return `${base}/notify?through=${through}`;
}
