import { pushDeviceId } from "./push-device";
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
export async function refreshCsrf() {
  const r = await fetch("/api/security/csrf", { credentials: "same-origin", cache: "no-store" });
  if (!r.ok) throw new ApiError(r.status, "csrf", "Няма връзка със сървъра.");
  csrf = (await r.json()).token;
}
export async function api<T = void>(path: string, method = "GET", data?: unknown): Promise<T> {
  if (method !== "GET" && !csrf) await refreshCsrf();
  const r = await fetch(`/api${path}`, {
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
    if (problem.code === "csrf") csrf = "";
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
