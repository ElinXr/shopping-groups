let fallbackId = "";
let fallbackOwner = "";
function uuid(): string {
  if (typeof crypto.randomUUID === "function") return crypto.randomUUID();
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  const hex = Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
  return hex.slice(0, 8) + "-" + hex.slice(8, 12) + "-" + hex.slice(12, 16) + "-" + hex.slice(16, 20) + "-" + hex.slice(20);
}
export function pushDeviceId(): string {
  if (!fallbackId) fallbackId = uuid();
  try {
    const saved = localStorage.getItem("shopping-push-device");
    if (saved && /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(saved)) return saved;
    localStorage.setItem("shopping-push-device", fallbackId);
  } catch { /* Storage can be unavailable in private browsing. */ }
  return fallbackId;
}
export function pushOwner(): string {
  try { return localStorage.getItem("shopping-push-owner") || fallbackOwner; }
  catch { return fallbackOwner; }
}
export function rememberPushOwner(userId: string): void {
  fallbackOwner = userId;
  try {
    if (userId) localStorage.setItem("shopping-push-owner", userId);
    else localStorage.removeItem("shopping-push-owner");
  } catch { /* Device notifications still work for the current session. */ }
}
export function renewPushDeviceId(): void {
  fallbackId = uuid();
  try { localStorage.setItem("shopping-push-device", fallbackId); } catch { /* Private storage. */ }
}
