import { useEffect, useState } from "react";
import { Bell, BellOff } from "lucide-react";
import { api } from "./api";
import { pushDeviceId } from "./push-device";
import { disablePush, enablePush, pushKey, supportsPush, syncPush, type PushConfig, type PushState } from "./push";

export function PushSettings({ userId }: { userId: string }) {
  const [config, setConfig] = useState<PushConfig | null>(null);
  const [registration, setRegistration] = useState<ServiceWorkerRegistration | null>(null);
  const [enabled, setEnabled] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const supported = supportsPush();
  useEffect(() => {
    let active = true;
    if (!supported) return;
    void Promise.all([api<PushConfig>("/push/config"), navigator.serviceWorker.ready])
      .then(async ([settings, worker]) => {
        const current = settings.enabled && await syncPush(userId);
        if (active) { setConfig(settings); setRegistration(worker); setEnabled(current); }
      })
      .catch(() => { if (active) setError("Настройките за известия не могат да се заредят. Опитайте след обновяване на страницата."); });
    return () => { active = false; };
  }, [userId, supported]);
  async function enable() {
    if (!registration || !config) return;
    setBusy(true); setError(""); setMessage("");
    try {
      // Called directly by the button gesture; the browser requests permission here.
      const subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: pushKey(config.publicKey) });
      await enablePush(subscription, userId);
      setEnabled(true);
      setMessage("Известията са включени за това устройство.");
    } catch {
      setError(Notification.permission === "denied"
        ? "Известията са блокирани. Разрешете ги от настройките на браузъра или устройството за този сайт."
        : "Известията не бяха включени. Проверете разрешението и интернет връзката и опитайте отново.");
    } finally { setBusy(false); }
  }
  return (
    <section className="push-settings" aria-labelledby="push-heading">
      <h2 id="push-heading"><Bell size={20} /> Известия на това устройство</h2>
      <p className="muted">Получавайте известие при начало и завършване на пазаруване и когато групата изпрати обобщение, дори приложението да е затворено.</p>
      <p className="hint">Включват се отделно на всяко устройство. Известията показват автора и часа на промените по българско време. Подробностите са достъпни и в приложението.</p>
      {!supported ? <p className="hint">Този браузър не предлага push известия тук. На iPhone/iPad добавете сайта на началния екран и го отворете от иконата. Необходима е iOS/iPadOS 16.4 или по-нова.</p> : (
        <>
          <p><strong>{enabled ? "Включени за това устройство" : "Изключени за това устройство"}</strong></p>
          {config && !config.enabled && <p className="hint">Известията за устройства са временно изключени от администратора.</p>}
          {!enabled ? <button type="button" className="secondary" disabled={busy || !config?.enabled || !registration || Notification.permission === "denied"} onClick={() => void enable()}>
            <Bell size={17} /> Включи известията
          </button> : (
            <div className="member-actions">
              <button type="button" className="secondary" disabled={busy} onClick={() => {
                setBusy(true); setError(""); setMessage("");
                void api("/push/devices/" + pushDeviceId() + "/test", "POST")
                  .then(() => setMessage("Тестовото известие е поставено за изпращане. Изчакайте няколко секунди."))
                  .catch(() => setError("Тестовото известие не беше заявено. Опитайте отново."))
                  .finally(() => setBusy(false));
              }}>Изпрати тестово известие</button>
              <button type="button" className="secondary" disabled={busy} onClick={() => {
                setBusy(true); setError(""); setMessage("");
                void disablePush().then(() => { setEnabled(false); setMessage("Известията са изключени за това устройство."); })
                  .catch(() => setError("Изключването не е потвърдено. Проверете връзката и опитайте отново."))
                  .finally(() => setBusy(false));
              }}><BellOff size={17} /> Изключи</button>
              <button type="button" className="text-button" disabled={busy} onClick={() => {
                void api<PushState>("/push/devices/" + pushDeviceId()).then(state => {
                  setEnabled(state.enabled);
                  setMessage(state.lastError ? "" : state.lastAcceptedAt ? "Има потвърдено приемане от услугата за известия. Показването зависи от настройките на устройството." : "Още няма потвърдено приемане от услугата за известия.");
                  setError(state.lastError ? "Последният опит не е приет. Можете да заявите ново тестово известие." : "");
                }).catch(() => setError("Състоянието не може да се провери."));
              }}>Провери изпращането</button>
            </div>
          )}
          {Notification.permission === "denied" && <p className="hint">Разрешението е отказано. Променете настройката за известия на този сайт в браузъра или устройството, след което обновете страницата.</p>}
        </>
      )}
      {message && <p role="status" className="alert success">{message}</p>}
      {error && <p role="alert" className="alert error">{error}</p>}
    </section>
  );
}
