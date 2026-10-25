import { pushDeviceId } from "./push-device.ts";
export class ApiError extends Error {
  status: number;
  code: string;
  constructor(status: number, code: string, message: string) {
    super(message);
    this.status = status;
    this.code = code;
  }
}
let csrf = "";
let csrfRequest: Promise<void> | null = null;
async function fetchResponse(url: string, options: RequestInit): Promise<Response> {
  try { return await fetch(url, options); }
  catch { throw new ApiError(0, "network", "Няма връзка със сървъра. Проверете интернет връзката и опитайте отново."); }
}
export async function refreshCsrf(): Promise<void> {
  if (!csrfRequest) csrfRequest = (async () => {
    const response = await fetchResponse("/api/security/csrf", { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) throw new ApiError(response.status, "csrf", "Не успяхме да обновим сесията. Опитайте отново.");
    csrf = (await response.json()).token;
  })().finally(() => { csrfRequest = null; });
  return csrfRequest;
}
export async function api<T = void>(path: string, method = "GET", data?: unknown): Promise<T> {
  return sendApi<T>(path, method, data, true);
}
async function sendApi<T>(path: string, method: string, data: unknown, retryCsrf: boolean): Promise<T> {
  if (method !== "GET" && !csrf) await refreshCsrf();
  const r = await fetchResponse(`/api${path}`, {
    method,
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      ...(path === "/auth/logout" ? { "X-Push-Device": pushDeviceId() } : {}),
      ...(data === undefined ? {} : { "Content-Type": "application/json" }),
      ...(method === "GET" ? {} : { "X-CSRF-TOKEN": csrf }),
    },
    body: data === undefined ? undefined : JSON.stringify(data),
  });
  if (!r.ok) {
    const problem = await r.json().catch(() => ({}));
    if (problem.code === "csrf") {
      csrf = "";
      // Antiforgery rejected this request before its action ran, so one retry is safe.
      if (retryCsrf) { await refreshCsrf(); return sendApi<T>(path, method, data, false); }
    }
    if (r.status === 401) {
      csrf = "";
      if (!path.startsWith("/auth/")) window.dispatchEvent(new Event("session-expired"));
    }
    throw new ApiError(
      r.status,
      problem.code ?? "",
      problem.title ??
        (r.status === 429
          ? "Твърде много опити. Изчакайте няколко минути."
          : "Заявката не е изпълнена."),
    );
  }
  return r.status === 204 || r.headers.get("content-length") === "0"
    ? (undefined as T)
    : await r.text().then((t) => (t ? (JSON.parse(t) as T) : (undefined as T)));
}
