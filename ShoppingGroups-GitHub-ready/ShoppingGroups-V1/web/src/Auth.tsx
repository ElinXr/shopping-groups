import { useState, type FormEvent } from "react";
import { ShoppingBasket, ArrowRight, Users, CheckCheck, LockKeyhole } from "lucide-react";
import { api, refreshCsrf } from "./api";
import type { User } from "./types";

export function Auth({
  onLogin,
  invite,
  reset,
  email,
  onResetDone,
}: {
  onLogin: (user: User) => void;
  invite: boolean;
  reset: string | null;
  email: string;
  onResetDone: () => void;
}) {
  const [mode, setMode] = useState<"login" | "register" | "forgot">("login");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage("");
    setError("");
    const data = Object.fromEntries(new FormData(event.currentTarget));
    try {
      if (reset) {
        await api("/auth/reset-password", "POST", { ...data, token: reset });
        onResetDone();
        setMessage("Паролата е сменена. Влезте в профила си.");
      } else if (mode === "forgot") {
        const r = await api<{ message: string }>("/auth/forgot-password", "POST", data);
        setMessage(r.message);
      } else if (mode === "register") {
        const r = await api<{ message: string }>("/auth/register", "POST", data);
        setMessage(r.message);
        setMode("login");
      } else {
        await refreshCsrf();
        await api("/auth/login", "POST", data);
        await refreshCsrf();
        onLogin(await api<User>("/auth/me"));
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Няма връзка със сървъра.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="auth-layout">
      <section className="auth-story">
        <a className="brand" href="/">
          <span className="brand-icon">
            <ShoppingBasket size={25} />
          </span>
          Shopping Groups
        </a>
        <div>
          <span className="eyebrow">ЕДИН СПИСЪК. ЦЯЛАТА ГРУПА.</span>
          <h1>
            Малките покупки.
            <br />
            По-лесни заедно.
          </h1>
          <p>От „свърши млякото“ до „вече е купено“ — всичко е на едно място.</p>
          <div className="story-list">
            <span>
              <Users />
              Вашите хора, вашата частна група
            </span>
            <span>
              <CheckCheck />
              Промени в реално време
            </span>
            <span>
              <LockKeyhole />
              Лични списъци. Споделена грижа.
            </span>
          </div>
        </div>
        <small>Shopping Groups · V1</small>
      </section>
      <section className="auth-form">
        <span className="eyebrow">ДОБРЕ ДОШЛИ</span>
        <h2>
          {reset
            ? "Нова парола"
            : mode === "register"
              ? "Да започнем заедно"
              : mode === "forgot"
                ? "Забравена парола"
                : "Вашите покупки ви чакат"}
        </h2>
        <p className="muted">
          {invite
            ? "Имате покана за частна група. Влезте или създайте профил, за да я приемете."
            : "Влезте, създайте група и поканете близките си."}
        </p>
        {error && (
          <p role="alert" className="alert error">
            {error}
          </p>
        )}
        {message && (
          <p role="status" className="alert success">
            {message}
          </p>
        )}
        <form onSubmit={submit}>
          <fieldset disabled={busy}>
            {mode === "register" && !reset && (
              <label>
                Вашето име
                <input
                  name="displayName"
                  autoComplete="name"
                  maxLength={80}
                  required
                  placeholder="Как да ви наричаме?"
                />
              </label>
            )}
            <label>
              Имейл адрес
              <input
                name="email"
                type="email"
                defaultValue={email}
                autoComplete="email"
                maxLength={254}
                required
                placeholder="you@example.com"
              />
            </label>
            {(mode !== "forgot" || reset) && (
              <label>
                Парола
                <input
                  name="password"
                  type="password"
                  minLength={reset || mode === "register" ? 10 : undefined}
                  autoComplete={reset || mode === "register" ? "new-password" : "current-password"}
                  required
                  placeholder={
                    mode === "register" || reset
                      ? "Поне 10 знака, главна буква, цифра и символ"
                      : "Вашата парола"
                  }
                />
              </label>
            )}
            <button className="primary wide" type="submit">
              {busy
                ? "Момент…"
                : reset
                  ? "Запази паролата"
                  : mode === "register"
                    ? "Създай профил"
                    : mode === "forgot"
                      ? "Изпрати линк"
                      : "Влез"}
              <ArrowRight size={18} />
            </button>
          </fieldset>
        </form>
        {!reset && (
          <>
            <button
              className="text-button"
              onClick={() => {
                setMode(mode === "login" ? "register" : "login");
                setError("");
                setMessage("");
              }}
            >
              {mode === "login" ? "Нямате профил? Създайте един" : "Вече имате профил? Влезте"}
            </button>
            {mode === "login" && (
              <button className="text-button muted" onClick={() => setMode("forgot")}>
                Забравена парола
              </button>
            )}
          </>
        )}
      </section>
    </main>
  );
}
