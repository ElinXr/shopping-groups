import { api, ApiError } from "./api";
import { pushDeviceId, pushOwner, rememberPushOwner, renewPushDeviceId } from "./push-device";

export interface PushConfig { enabled: boolean; publicKey: string }
export interface PushState { enabled: boolean; lastAcceptedAt: string | null; lastError: string | null }
export function supportsPush(): boolean {
  return window.isSecureContext && "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}
export function pushKey(publicKey: string): ArrayBuffer {
  const text = publicKey.replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(text + "=".repeat((4 - text.length % 4) % 4));
  return Uint8Array.from(raw, c => c.charCodeAt(0)).buffer;
}
export async function localPushCleanup(): Promise<void> {
  rememberPushOwner("");
  renewPushDeviceId();
  if (!("serviceWorker" in navigator)) return;
  const registration = await navigator.serviceWorker.getRegistration("/");
  if (!registration) return;
  const subscription = await registration.pushManager?.getSubscription();
  if (subscription) await subscription.unsubscribe();
  for (const notification of await registration.getNotifications()) notification.close();
}
export async function syncPush(userId: string): Promise<boolean> {
  if (!supportsPush()) return false;
  if (!pushOwner()) return false;
  // Cookies are shared by tabs: an old tab must not revoke the new account's device.
  const current = await api<{ id: string }>("/auth/me");
  if (current.id !== userId) {
    window.dispatchEvent(new Event("session-expired"));
    return false;
  }
  if (pushOwner() && pushOwner() !== userId) {
    await localPushCleanup();
    return false;
  }
  if (pushOwner() !== userId || Notification.permission !== "granted") return false;
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  if (!subscription) { rememberPushOwner(""); return false; }
  await api("/push/devices/" + pushDeviceId(), "PUT", { endpoint: subscription.endpoint });
  return true;
}
export async function enablePush(subscription: PushSubscription, userId: string): Promise<void> {
  try { await api("/push/devices/" + pushDeviceId(), "PUT", { endpoint: subscription.endpoint }); }
  catch (error) {
    if (error instanceof ApiError && error.status === 409) await localPushCleanup();
    throw error;
  }
  rememberPushOwner(userId);
}
export async function disablePush(): Promise<void> {
  // Revoke on the server before reporting success, even if browser unsubscribe fails.
  try { await api("/push/devices/" + pushDeviceId(), "DELETE"); }
  catch (error) {
    if (!(error instanceof Error && "status" in error && error.status === 404)) throw error;
  }
  await localPushCleanup();
}
