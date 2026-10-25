import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import {
  ShoppingBasket,
  Plus,
  Bell,
  Users,
  ChevronDown,
  ArrowLeft,
  Check,
  Pencil,
  Trash2,
  Archive,
  History,
  ShoppingBag,
  Send,
  RotateCcw,
  LogOut,
  ShieldCheck,
  WifiOff,
  RefreshCw,
  Menu,
  X,
  Settings as SettingsIcon,
} from "lucide-react";
import { api, ApiError, refreshCsrf } from "./api";
import { realtime } from "./realtime";
import { date, duplicate, progress, safeInvite } from "./helpers";
import { Auth } from "./Auth";
import { Empty, Modal } from "./components";
import { Settings } from "./Settings";
import { Admin } from "./Admin";
import { PushSettings } from "./PushSettings";
import { localPushCleanup, syncPush } from "./push";
import type {
  Activity,
  Group,
  GroupDetail,
  ListBrief,
  ListDetail,
  Notice,
  Product,
  Summary,
  User,
} from "./types";

export type Run = (work: () => Promise<void>, reloadAfter?: boolean) => Promise<void>;
type Tab = "lists" | "people" | "activity" | "notices" | "profile" | "admin";
type Dialog =
  | { kind: "group" | "list" | "rename"; name?: string }
  | { kind: "product"; item?: Product }
  | { kind: "summary"; summary: Summary }
  | null;
const initial = new URLSearchParams(location.search);
const initialInvite = safeInvite(initial.get("invite"));
const initialConfirm = initial.get("confirm");
const initialReset = initial.get("reset");
const initialEmail = initial.get("email") || "";
if (initialInvite) sessionStorage.setItem("shopping-invite", initialInvite);
if (initialInvite || initialConfirm || initialReset) history.replaceState(null, "", "/");

export function App() {
  const [user, setUser] = useState<User | null>(null);
  const [initializing, setInitializing] = useState(true);
  const [invite, setInvite] = useState(safeInvite(sessionStorage.getItem("shopping-invite")));
  const [reset, setReset] = useState(initialReset);
  const [groups, setGroups] = useState<Group[]>([]);
  const [groupId, setGroupId] = useState("");
  const [group, setGroup] = useState<GroupDetail | null>(null);
  const [lists, setLists] = useState<ListBrief[]>([]);
  const [listId, setListId] = useState("");
  const [list, setList] = useState<ListDetail | null>(null);
  const [notices, setNotices] = useState<Notice[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [tab, setTab] = useState<Tab>(initial.get("notifications") === "1" ? "notices" : "lists");
  const [view, setView] = useState("active");
  const [dialog, setDialog] = useState<Dialog>(null);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [online, setOnline] = useState(navigator.onLine);
  const [connected, setConnected] = useState(false);
  const [tick, setTick] = useState(0);
  const [mobileMenu, setMobileMenu] = useState(false);
  const locked = useRef(false);
  const reload = useCallback(() => setTick((t) => t + 1), []);
  const run: Run = async (work, reloadAfter = true) => {
    if (locked.current) return;
    if (!navigator.onLine) {
      setError("Няма интернет връзка. Промените са възможни след свързване.");
      return;
    }
    locked.current = true;
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await work();
      if (reloadAfter) reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Няма връзка със сървъра.");
      if (e instanceof ApiError && e.status === 409) reload();
    } finally {
      locked.current = false;
      setBusy(false);
    }
  };
  useEffect(() => {
    let active = true;
    async function boot() {
      try {
        if (initialConfirm) {
          await api("/auth/confirm", "POST", { email: initialEmail, token: initialConfirm });
          if (active) setSuccess("Имейлът е потвърден.");
        }
        const me = await api<User>("/auth/me");
        if (active) setUser(me);
      } catch (e) {
        if (active && (!(e instanceof ApiError) || e.status !== 401))
          setError(e instanceof Error ? e.message : "Няма връзка със сървъра.");
      } finally {
        if (active) setInitializing(false);
      }
    }
    void boot();
    const on = () => {
      setOnline(true);
      reload();
    };
    const off = () => setOnline(false);
    const expired = () => {
      setUser(null);
      setGroup(null);
      setList(null);
      setGroups([]);
      setNotices([]);
      setActivities([]);
      setListId("");
      setGroupId("");
      setDialog(null);
      setError("Сесията е изтекла. Влезте отново.");
    };
    window.addEventListener("online", on);
    window.addEventListener("offline", off);
    window.addEventListener("session-expired", expired);
    return () => {
      active = false;
      window.removeEventListener("online", on);
      window.removeEventListener("offline", off);
      window.removeEventListener("session-expired", expired);
    };
  }, [reload]);
  useEffect(() => {
    if (!user) return;
    const sync = () => { void syncPush(user.id).catch(() => {}); };
    sync();
    window.addEventListener("focus", sync);
    const opened = (event: MessageEvent) => {
      if (event.data?.type === "open-notifications") { setTab("notices"); reload(); }
    };
    navigator.serviceWorker?.addEventListener("message", opened);
    return () => {
      window.removeEventListener("focus", sync);
      navigator.serviceWorker?.removeEventListener("message", opened);
    };
  }, [user?.id, reload]);
  useEffect(() => {
    if (!user) return;
    let timer: ReturnType<typeof setTimeout>;
    const disconnect = realtime(() => {
      clearTimeout(timer);
      timer = setTimeout(reload, 180);
    }, setConnected);
    const poll = setInterval(reload, 30000);
    window.addEventListener("focus", reload);
    return () => {
      disconnect();
      clearTimeout(timer);
      clearInterval(poll);
      window.removeEventListener("focus", reload);
    };
  }, [user?.id, reload]);
  useEffect(() => {
    if (!user || !online) return;
    let active = true;
    setLoading(true);
    async function load() {
      try {
        const [gs, ns] = await Promise.all([
          api<Group[]>("/groups"),
          api<Notice[]>("/notifications/"),
        ]);
        if (!active) return;
        setGroups(gs);
        setNotices(ns);
        const selected = gs.find((g) => g.id === groupId) || gs[0];
        if (!selected) {
          setGroupId("");
          setGroup(null);
          setLists([]);
          setList(null);
          return;
        }
        if (selected.id !== groupId) {
          setGroupId(selected.id);
          return;
        }
        if (selected.disabled) {
          setGroup(null);
          setLists([]);
          setList(null);
          return;
        }
        const [g, ls] = await Promise.all([
          api<GroupDetail>(`/groups/${groupId}`),
          api<ListBrief[]>(`/groups/${groupId}/lists/?view=${view}`),
        ]);
        if (!active) return;
        setGroup(g);
        setLists(ls);
        if (listId) {
          try {
            const l = await api<ListDetail>(`/groups/${groupId}/lists/${listId}`);
            if (active) setList(l);
          } catch (e) {
            if (active && e instanceof ApiError && e.status === 404) {
              setListId("");
              setList(null);
            } else throw e;
          }
        } else setList(null);
        if (tab === "activity") {
          const a = await api<Activity[]>(
            `/groups/${groupId}/activity${listId ? `?listId=${listId}` : ""}`,
          );
          if (active) setActivities(a);
        }
      } catch (e) {
        if (active) {
          setError(e instanceof Error ? e.message : "Не успяхме да заредим данните.");
          setGroup(null);
          setList(null);
          setLists([]);
        }
      } finally {
        if (active) setLoading(false);
      }
    }
    void load();
    return () => {
      active = false;
    };
  }, [user?.id, groupId, listId, view, tab, tick, online]);
  useEffect(() => {
    if (!success) return;
    const t = setTimeout(() => setSuccess(""), 7000);
    return () => clearTimeout(t);
  }, [success]);
  function switchGroup(id: string) {
    setGroupId(id);
    setListId("");
    setList(null);
    setGroup(null);
    setView("active");
    setTab("lists");
    setMobileMenu(false);
    setError("");
  }
  function navigate(next: Tab) {
    setTab(next);
    setMobileMenu(false);
    setError("");
  }
  async function logout() {
    await api("/auth/logout", "POST");
    await localPushCleanup().catch(() => {});
    await refreshCsrf();
    setUser(null);
    setGroupId("");
    setListId("");
    setGroup(null);
    setList(null);
    setGroups([]);
    setLists([]);
    setActivities([]);
    setNotices([]);
    setDialog(null);
  }
  const unread = notices.filter((n) => !n.readAt).length;
  const nav = [
    { key: "lists" as const, label: "Моите списъци", icon: ShoppingBasket },
    { key: "people" as const, label: "Хора и покани", icon: Users },
    { key: "activity" as const, label: "История", icon: History },
    { key: "notices" as const, label: "Известия", icon: Bell },
  ];
  if (initializing)
    return (
      <div className="boot">
        <ShoppingBasket size={42} />
        <p>Подготвяме вашето място…</p>
      </div>
    );
  if (!user || reset)
    return (
      <>
        {error && (
          <div role="alert" className="global-alert error">
            {error}
            <button onClick={() => setError("")} aria-label="Затвори">
              <X size={18} />
            </button>
          </div>
        )}
        {success && (
          <div role="status" className="global-alert success">
            {success}
          </div>
        )}
        <Auth
          onLogin={setUser}
          invite={!!invite}
          reset={reset}
          email={initialEmail}
          onResetDone={() => setReset(null)}
        />
      </>
    );
  const base = `/groups/${groupId}/lists/${listId}`;
  const stats = list ? progress(list.items) : { purchased: 0, total: 0 };
  const shopping = !!list?.shoppers.some((s) => s.userId === user.id);
  return (
    <div className="app-shell">
      <aside className={mobileMenu ? "sidebar open" : "sidebar"}>
        <a href="/" className="brand">
          <span className="brand-icon">
            <ShoppingBasket size={24} />
          </span>
          Shopping Groups
        </a>
        <button
          className="close-menu icon-button"
          aria-label="Затвори менюто"
          onClick={() => setMobileMenu(false)}
        >
          <X />
        </button>
        <div className="group-switch">
          <label htmlFor="group-choice">ВАШАТА ГРУПА</label>
          <div>
            <select
              id="group-choice"
              value={groupId}
              onChange={(e) => switchGroup(e.target.value)}
              aria-label="Избери група"
            >
              {groups.length === 0 && <option value="">Създайте група</option>}
              {groups.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.name}
                  {g.disabled ? " · спряна" : ""}
                </option>
              ))}
            </select>
            <ChevronDown size={15} />
          </div>
          <button className="text-button" onClick={() => setDialog({ kind: "group" })}>
            <Plus size={15} />
            Нова група
          </button>
        </div>
        <nav aria-label="Основна навигация">
          {nav.map((n) => (
            <button
              className={tab === n.key ? "nav-item selected" : "nav-item"}
              key={n.key}
              onClick={() => navigate(n.key)}
            >
              <n.icon size={20} />
              {n.label}
              {n.key === "notices" && unread > 0 && <span className="badge">{unread}</span>}
            </button>
          ))}
          {user.isAdmin && (
            <button
              className={tab === "admin" ? "nav-item selected" : "nav-item"}
              onClick={() => navigate("admin")}
            >
              <ShieldCheck size={20} />
              Администрация
            </button>
          )}
        </nav>
        <div className="sidebar-bottom">
          <div className="privacy">
            <ShieldCheck size={18} />
            <p>
              Само вашата група.<small>Споделено с хората, които избирате.</small>
            </p>
          </div>
          <button className="profile-link" onClick={() => navigate("profile")}>
            <span className="avatar">{user.displayName.slice(0, 1).toUpperCase()}</span>
            <span>
              <strong>{user.displayName}</strong>
              <small>Моят профил</small>
            </span>
            <SettingsIcon size={17} />
          </button>
        </div>
      </aside>
      {mobileMenu && <div className="menu-backdrop" onClick={() => setMobileMenu(false)} />}
      <div className="main-shell">
        <header className="topbar">
          <button
            className="icon-button mobile-menu"
            onClick={() => setMobileMenu(true)}
            aria-label="Отвори менюто"
          >
            <Menu />
          </button>
          <div className="breadcrumb">
            Вашето пространство<span>/</span>
            <strong>{group?.name || "Shopping Groups"}</strong>
          </div>
          <div className="topbar-actions">
            <span className={connected ? "connection" : "connection disconnected"}>
              <span />
              {!online ? "Офлайн" : connected ? "На живо" : "Свързване…"}
            </span>
            <button
              className="icon-button"
              aria-label="Обнови данните"
              onClick={reload}
              disabled={loading}
            >
              <RefreshCw size={18} className={loading ? "spin" : ""} />
            </button>
            <button
              className="icon-button notice-button"
              aria-label={`Известия: ${unread} непрочетени`}
              onClick={() => navigate("notices")}
            >
              <Bell size={20} />
              {unread > 0 && <i />}
            </button>
          </div>
        </header>
        <main className="main-content">
          {!online && (
            <p role="status" className="alert warning">
              <WifiOff size={18} />
              Няма интернет връзка. Показаните данни може да са остарели. Свържете се, за да правите
              промени.
            </p>
          )}
          {error && (
            <div role="alert" className="alert error">
              {error}
              <button
                className="icon-button"
                aria-label="Затвори грешката"
                onClick={() => setError("")}
              >
                <X size={18} />
              </button>
            </div>
          )}
          {success && (
            <p role="status" className="alert success">
              {success}
            </p>
          )}
          {invite && (
            <div className="invite-banner">
              <div>
                <strong>Поканени сте в група</strong>
                <p>Приемете поканата, за да видите споделените списъци.</p>
              </div>
              <button
                className="primary"
                disabled={busy || !online}
                onClick={() => {
                  void run(async () => {
                    const r = await api<{ groupId: string }>("/invitations/accept", "POST", {
                      token: invite,
                    });
                    sessionStorage.removeItem("shopping-invite");
                    setInvite(null);
                    switchGroup(r.groupId);
                    setSuccess("Добре дошли в групата!");
                  });
                }}
              >
                Приеми поканата
              </button>
              <button
                className="icon-button"
                aria-label="Отхвърли поканата"
                onClick={() => {
                  sessionStorage.removeItem("shopping-invite");
                  setInvite(null);
                }}
              >
                <X size={18} />
              </button>
            </div>
          )}
          {tab === "admin" && user.isAdmin ? (
            <Admin run={run} busy={busy} tick={tick} me={user.id} />
          ) : tab === "profile" ? (
            <section className="card profile-card">
              <span className="eyebrow">ВАШИЯТ ПРОФИЛ</span>
              <h1>{user.displayName}</h1>
              <p>
                {user.email} · {user.emailConfirmed ? "Потвърден имейл" : "Непотвърден имейл"}
              </p>
              {!user.emailConfirmed && (
                <button
                  className="secondary"
                  disabled={busy}
                  onClick={() => {
                    void run(async () => {
                      await api("/auth/resend-confirmation", "POST", { email: user.email });
                      setSuccess("Изпратихме линк за потвърждение.");
                    });
                  }}
                >
                  Изпрати потвърждение
                </button>
              )}
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  const data = new FormData(e.currentTarget);
                  void run(async () => {
                    await api("/auth/profile", "PUT", {
                      displayName: data.get("displayName"),
                      emailNotifications: data.has("emailNotifications"),
                    });
                    setUser(await api<User>("/auth/me"));
                    setSuccess("Профилът е запазен.");
                  });
                }}
              >
                <label>
                  Вашето име
                  <input
                    name="displayName"
                    defaultValue={user.displayName}
                    maxLength={80}
                    required
                  />
                </label>
                <label className="check-label">
                  <input
                    type="checkbox"
                    name="emailNotifications"
                    defaultChecked={user.emailNotifications}
                  />
                  Получавай известия и по имейл
                </label>
                <p className="hint">
                  Имейлите изискват потвърден адрес. Известията в приложението са винаги налични.
                </p>
                <button className="primary" disabled={busy}>
                  Запази
                </button>
              </form>
              <PushSettings userId={user.id} />
              <button
                className="secondary"
                disabled={busy}
                onClick={() => {
                  void run(logout);
                }}
              >
                <LogOut size={17} />
                Излез от профила
              </button>
            </section>
          ) : tab === "notices" ? (
            <>
              <div className="page-heading">
                <div>
                  <span className="eyebrow">ВСИЧКО В ЕДИН ПОГЛЕД</span>
                  <h1>Вашите известия</h1>
                  <p className="muted">
                    Начало на пазаруване, завършени списъци и обобщения от групата.
                  </p>
                </div>
              </div>
              {notices.length === 0 ? (
                <Empty title="Тук е спокойно">Новите известия ще се появят на това място.</Empty>
              ) : (
                <div className="notice-list">
                  {notices.map((n) => (
                    <button
                      key={n.id}
                      className={`card notice ${n.readAt ? "" : "unread"}`}
                      onClick={() => {
                        void run(async () => {
                          await api(`/notifications/${n.id}/read`, "PUT");
                          switchGroup(n.groupId);
                          if (n.listId) setListId(n.listId);
                        });
                      }}
                    >
                      <span className="notice-icon">
                        {n.kind === "completed" ? (
                          <Check />
                        ) : n.kind === "started" ? (
                          <ShoppingBag />
                        ) : (
                          <Bell />
                        )}
                      </span>
                      <div>
                        <p>{n.text}</p>
                        <small>{date(n.at)}</small>
                      </div>
                      {!n.readAt && <i />}
                    </button>
                  ))}
                  {notices.length >= 50 && (
                    <button
                      className="secondary"
                      onClick={() => {
                        void run(async () => {
                          const older = await api<Notice[]>(
                            `/notifications/?before=${notices[notices.length - 1].id}`,
                          );
                          setNotices((n) => [...n, ...older]);
                        }, false);
                      }}
                    >
                      По-стари известия
                    </button>
                  )}
                </div>
              )}
            </>
          ) : !group ? (
            loading ? (
              <p className="muted">Зареждане…</p>
            ) : (
              <Empty
                title={
                  groups.some((g) => g.id === groupId && g.disabled)
                    ? "Групата е временно спряна"
                    : "Първият списък започва с вашите хора"
                }
              >
                {groups.some((g) => g.id === groupId && g.disabled) ? (
                  "Свържете се с администратора."
                ) : (
                  <>
                    Създайте частна група за семейството, съквартирантите или приятелите.
                    <br />
                    <button className="primary" onClick={() => setDialog({ kind: "group" })}>
                      <Plus size={18} />
                      Създай група
                    </button>
                  </>
                )}
              </Empty>
            )
          ) : tab === "people" ? (
            <>
              <div className="page-heading">
                <div>
                  <span className="eyebrow">ВАШАТА ЧАСТНА ГРУПА</span>
                  <h1>Заедно в {group.name}</h1>
                </div>
              </div>
              <Settings
                key={group.id}
                group={group}
                me={user.id}
                run={run}
                busy={busy}
                tick={tick}
                onLeave={() => switchGroup("")}
              />
            </>
          ) : tab === "activity" ? (
            <>
              <div className="page-heading">
                <div>
                  <span className="eyebrow">ИСТОРИЯ НА ПРОМЕНИТЕ</span>
                  <h1>{list ? list.name : "Какво се случва в групата"}</h1>
                  <p className="muted">Кой добави, промени или купи — без догадки.</p>
                </div>
                {listId && (
                  <button
                    className="secondary"
                    onClick={() => {
                      setListId("");
                      setList(null);
                    }}
                  >
                    Цялата група
                  </button>
                )}
              </div>
              <section className="card activity-list">
                {activities.length === 0 ? (
                  <Empty title="Все още няма промени" />
                ) : (
                  activities.map((a) => (
                    <div className="activity" key={a.id}>
                      <span className="activity-dot" />
                      <div>
                        <p>{a.text}</p>
                        <small>
                          {a.actorName} · {date(a.at)}
                        </small>
                      </div>
                    </div>
                  ))
                )}
                {activities.length >= 50 && (
                  <button
                    className="secondary"
                    onClick={() => {
                      void run(async () => {
                        const more = await api<Activity[]>(
                          `/groups/${groupId}/activity?before=${activities[activities.length - 1].id}${listId ? `&listId=${listId}` : ""}`,
                        );
                        setActivities((a) => [...a, ...more]);
                      }, false);
                    }}
                  >
                    По-стари промени
                  </button>
                )}
              </section>
            </>
          ) : listId && list ? (
            <>
              <button
                className="back-link"
                onClick={() => {
                  setListId("");
                  setList(null);
                }}
              >
                <ArrowLeft size={17} />
                Всички списъци
              </button>
              <div className="page-heading list-heading">
                <div>
                  <span className="eyebrow">
                    {list.completed
                      ? "ГОТОВО. ВСИЧКО Е КУПЕНО."
                      : "МАЛКО ОРГАНИЗАЦИЯ, ПОВЕЧЕ СПОКОЙСТВИЕ"}
                  </span>
                  <h1>
                    {list.name}
                    <button
                      className="icon-button"
                      disabled={busy}
                      aria-label="Преименувай списъка"
                      onClick={() => setDialog({ kind: "rename", name: list.name })}
                    >
                      <Pencil size={18} />
                    </button>
                  </h1>
                  <p className="muted">
                    {stats.purchased} от {stats.total} купени продукта
                    {list.archived ? " · Архивиран за вас" : ""}
                  </p>
                </div>
                <button
                  className={shopping ? "secondary" : "primary"}
                  disabled={busy || !online || list.completed || stats.total === 0}
                  onClick={() => {
                    void run(async () => {
                      await api(`${base}/shopping/${shopping ? "stop" : "start"}`, "POST");
                    });
                  }}
                >
                  <ShoppingBag size={19} />
                  {shopping ? "Приключи пазаруването" : "Аз ще пазарувам"}
                </button>
              </div>
              <div className="list-grid">
                <section className="card products-card">
                  <div className="products-heading">
                    <h2>
                      За покупка <span className="count">{stats.total - stats.purchased}</span>
                    </h2>
                    <span className={list.completed ? "status complete" : "status"}>
                      {list.completed ? "Завършен" : "Активен"}
                    </span>
                  </div>
                  <progress
                    max={stats.total || 1}
                    value={stats.purchased}
                    aria-label={`${stats.purchased} от ${stats.total} купени`}
                  />
                  <form
                    className="add-product"
                    onSubmit={(e) => {
                      e.preventDefault();
                      const form = e.currentTarget;
                      const text = String(new FormData(form).get("text") || "");
                      void run(async () => {
                        let allowDuplicate = false;
                        if (duplicate(list.items, text)) {
                          allowDuplicate = confirm(
                            "Този продукт вече е в списъка. Да го добавим отново?",
                          );
                          if (!allowDuplicate) return;
                        }
                        try {
                          await api(`${base}/items`, "POST", { text, allowDuplicate });
                        } catch (e) {
                          if (
                            e instanceof ApiError &&
                            e.code === "duplicate" &&
                            confirm(e.message)
                          ) {
                            await api(`${base}/items`, "POST", { text, allowDuplicate: true });
                          } else throw e;
                        }
                        form.reset();
                      });
                    }}
                  >
                    <input
                      name="text"
                      required
                      maxLength={200}
                      aria-label="Нов продукт"
                      placeholder="Какво ни трябва? Например: мляко 2 л"
                      disabled={busy || !online}
                    />
                    <button
                      type="submit"
                      className="primary"
                      disabled={busy || !online}
                      aria-label="Добави продукт"
                    >
                      <Plus size={22} />
                      <span>Добави</span>
                    </button>
                  </form>
                  {list.items.length === 0 ? (
                    <Empty title="Какво ще купуваме?">
                      Добавете първия продукт. Всички от групата могат да допълват списъка.
                    </Empty>
                  ) : (
                    <div className="product-list">
                      {[...list.items]
                        .sort((a, b) => Number(a.purchased) - Number(b.purchased))
                        .map((item) => (
                          <div
                            className={item.purchased ? "product purchased" : "product"}
                            key={item.id}
                          >
                            <button
                              className="purchase-check"
                              role="checkbox"
                              aria-checked={item.purchased}
                              aria-label={`${item.purchased ? "Върни за покупка" : "Отбележи като купено"}: ${item.text}`}
                              disabled={busy || !online}
                              onClick={() => {
                                void run(async () => {
                                  await api(`${base}/items/${item.id}/purchase`, "PUT", {
                                    purchased: !item.purchased,
                                    version: item.version,
                                  });
                                });
                              }}
                            >
                              {item.purchased && <Check size={18} />}
                            </button>
                            <div className="product-text">
                              <span>{item.text}</span>
                              {item.purchased && (
                                <small>
                                  Купено от{" "}
                                  {group.members.find((m) => m.userId === item.purchasedBy)
                                    ?.displayName || "участник"}
                                </small>
                              )}
                            </div>
                            <div className="product-actions">
                              <button
                                className="icon-button"
                                aria-label={`Промени ${item.text}`}
                                disabled={busy || !online}
                                onClick={() => setDialog({ kind: "product", item })}
                              >
                                <Pencil size={16} />
                              </button>
                              <button
                                className="icon-button danger"
                                aria-label={`Изтрий ${item.text}`}
                                disabled={busy || !online}
                                onClick={() => {
                                  if (confirm(`Да изтрием „${item.text}“?`))
                                    void run(async () => {
                                      await api(
                                        `${base}/items/${item.id}?version=${item.version}`,
                                        "DELETE",
                                      );
                                    });
                                }}
                              >
                                <Trash2 size={16} />
                              </button>
                            </div>
                          </div>
                        ))}
                    </div>
                  )}
                </section>
                <aside className="list-aside">
                  <section className="card shoppers">
                    <span className="eyebrow">ПАЗАРУВАМЕ ЗАЕДНО</span>
                    <h3>{list.shoppers.length ? "В магазина сега" : "Кой ще пазарува?"}</h3>
                    {list.shoppers.length ? (
                      list.shoppers.map((s) => (
                        <div className="shopper" key={s.userId}>
                          <span className="avatar">{s.displayName.slice(0, 1)}</span>
                          <span>
                            {s.displayName}
                            {s.userId === user.id ? " (вие)" : ""}
                          </span>
                          <span className="live-dot" />
                        </div>
                      ))
                    ) : (
                      <p className="muted">
                        С бутона „Аз ще пазарувам“ групата автоматично ще бъде известена.
                      </p>
                    )}
                    <p className="hint">Няколко души могат да пазаруват едновременно.</p>
                  </section>
                  <section className="card">
                    <h3>Дръжте всички в течение</h3>
                    <p className="muted">
                      Прегледайте последните промени и изпратете едно обобщено известие.
                    </p>
                    <button
                      className="secondary wide"
                      disabled={busy || !online}
                      onClick={() => {
                        void run(async () => {
                          const s = await api<Summary>(`${base}/summary`);
                          setDialog({ kind: "summary", summary: s });
                        });
                      }}
                    >
                      <Send size={17} />
                      Извести групата
                    </button>
                  </section>
                  <div className="list-tools">
                    <button className="text-button" onClick={() => setTab("activity")}>
                      <History size={17} />
                      История на списъка
                    </button>
                    <button
                      className="text-button"
                      disabled={busy}
                      onClick={() => {
                        void run(async () => {
                          await api(`${base}/archive`, "PUT", { archived: !list.archived });
                          setListId("");
                          setList(null);
                        });
                      }}
                    >
                      <Archive size={17} />
                      {list.archived ? "Извади от архива" : "Архивирай за мен"}
                    </button>
                    <button
                      className="text-button danger"
                      disabled={busy}
                      onClick={() => {
                        if (
                          confirm(
                            "Да изтрием списъка за цялата група? Може да бъде възстановен до 90 дни.",
                          )
                        )
                          void run(async () => {
                            await api(base, "DELETE");
                            setListId("");
                            setList(null);
                          });
                      }}
                    >
                      <Trash2 size={17} />
                      Изтрий списъка
                    </button>
                  </div>
                </aside>
              </div>
            </>
          ) : listId && loading ? (
            <p className="muted">Зареждане на списъка…</p>
          ) : (
            <>
              <div className="page-heading">
                <div>
                  <span className="eyebrow">
                    ДОБРЕ ДОШЛИ, {user.displayName.toLocaleUpperCase("bg-BG")}
                  </span>
                  <h1>Какво ще купуваме?</h1>
                  <p className="muted">Един списък по-малко в главата. Всичко нужно — тук.</p>
                </div>
                <button
                  className="primary"
                  onClick={() => setDialog({ kind: "list" })}
                  disabled={busy || !online}
                >
                  <Plus size={19} />
                  Нов списък
                </button>
              </div>
              <div className="section-toolbar">
                <div className="tabs" aria-label="Филтри за списъците">
                  {[
                    ["active", "Всички"],
                    ["archived", "Архив"],
                    ["trash", "Кошче"],
                  ].map(([key, label]) => (
                    <button
                      key={key}
                      aria-pressed={view === key}
                      className={view === key ? "active" : ""}
                      onClick={() => setView(key)}
                    >
                      {label}
                    </button>
                  ))}
                </div>
                <span className="muted list-count">{lists.length} списъка</span>
              </div>
              {view === "trash" && (
                <p className="hint">
                  Изтритите списъци могат да бъдат възстановени в рамките на 90 дни. След този срок
                  се премахват окончателно.
                </p>
              )}
              {lists.length === 0 ? (
                <Empty
                  title={
                    view === "trash"
                      ? "Кошчето е празно"
                      : view === "archived"
                        ? "Няма архивирани списъци"
                        : "Място за следващата покупка"
                  }
                >
                  {view === "active"
                    ? "Създайте списък за седмицата, за дома или за нещо специално."
                    : "Тук ще намерите списъците от избрания раздел."}
                </Empty>
              ) : (
                <div className="list-cards">
                  {lists.map((l) => (
                    <article className="card list-card" key={l.id}>
                      <button
                        className="list-card-main"
                        onClick={() => {
                          if (view !== "trash") {
                            setListId(l.id);
                            setList(null);
                          }
                        }}
                        disabled={view === "trash"}
                      >
                        <div className="list-card-top">
                          <span className="list-icon">
                            <ShoppingBasket size={24} />
                          </span>
                          <span className={l.completed ? "status complete" : "status"}>
                            {view === "trash" ? "Изтрит" : l.completed ? "Завършен" : "Активен"}
                          </span>
                        </div>
                        <h2>{l.name}</h2>
                        <p className="muted">
                          {l.purchased} от {l.total} продукта купени
                        </p>
                        <progress
                          max={l.total || 1}
                          value={l.purchased}
                          aria-label={`${l.purchased} от ${l.total}`}
                        />
                        <div className="list-card-bottom">
                          {l.shoppers > 0 ? (
                            <span className="shopping-now">
                              <span className="live-dot" />
                              {l.shoppers} пазаруват сега
                            </span>
                          ) : (
                            <span>
                              {l.completed
                                ? "Всичко е купено. Браво!"
                                : "Готов за следващото пазаруване"}
                            </span>
                          )}
                        </div>
                      </button>
                      {view === "trash" && (
                        <button
                          className="secondary"
                          disabled={busy}
                          onClick={() => {
                            void run(async () => {
                              await api(`/groups/${groupId}/lists/${l.id}/restore`, "POST");
                              setSuccess("Списъкът е възстановен.");
                            });
                          }}
                        >
                          <RotateCcw size={17} />
                          Възстанови
                        </button>
                      )}
                      {view === "archived" && (
                        <button
                          className="text-button"
                          disabled={busy}
                          onClick={() => {
                            void run(async () => {
                              await api(`/groups/${groupId}/lists/${l.id}/archive`, "PUT", {
                                archived: false,
                              });
                            });
                          }}
                        >
                          Извади от архива
                        </button>
                      )}
                    </article>
                  ))}
                  {view === "active" && (
                    <button className="new-list-card" onClick={() => setDialog({ kind: "list" })}>
                      <span>
                        <Plus size={24} />
                      </span>
                      <strong>Още един списък</strong>
                      <small>За всичко, което предстои</small>
                    </button>
                  )}
                </div>
              )}
              <div className="quiet-note">
                <Users size={18} />
                <p>
                  Виждате едно и също. Пазарувате заедно.
                  <small>Промените се споделят автоматично с {group.name}.</small>
                </p>
              </div>
            </>
          )}
        </main>
        <nav className="bottom-nav" aria-label="Мобилна навигация">
          {nav.map((n) => (
            <button
              key={n.key}
              className={tab === n.key ? "active" : ""}
              onClick={() => navigate(n.key)}
            >
              <n.icon size={21} />
              <span>{n.key === "people" ? "Хора" : n.key === "lists" ? "Списъци" : n.label}</span>
            </button>
          ))}
        </nav>
      </div>
      {dialog && (
        <Modal
          title={
            dialog.kind === "group"
              ? "Нова частна група"
              : dialog.kind === "list"
                ? "Нов списък"
                : dialog.kind === "rename"
                  ? "Име на списъка"
                  : dialog.kind === "product"
                    ? "Промени продукта"
                    : "Извести групата"
          }
          close={() => setDialog(null)}
        >
          {dialog.kind === "summary" ? (
            <>
              <p className="muted">
                {dialog.summary.count} промени от последното изпратено обобщение.
              </p>
              <pre className="summary-preview">
                {dialog.summary.summary || "Няма нови промени за изпращане."}
              </pre>
              <p className="hint">
                При изпращане ще включим и новите промени, направени междувременно.
              </p>
              <button
                className="primary wide"
                disabled={busy || !dialog.summary.count}
                onClick={() => {
                  void run(async () => {
                    await api(`${base}/notify`, "POST");
                    setDialog(null);
                    setSuccess("Обобщението е изпратено до групата.");
                  });
                }}
              >
                <Send size={17} />
                Изпрати обобщението
              </button>
            </>
          ) : (
            <form
              onSubmit={(e: FormEvent<HTMLFormElement>) => {
                e.preventDefault();
                const data = new FormData(e.currentTarget);
                const current = dialog;
                void run(async () => {
                  if (current.kind === "group") {
                    const r = await api<{ id: string }>("/groups", "POST", {
                      name: data.get("name"),
                    });
                    switchGroup(r.id);
                  } else if (current.kind === "list") {
                    const r = await api<{ id: string }>(`/groups/${groupId}/lists/`, "POST", {
                      name: data.get("name"),
                    });
                    setListId(r.id);
                    setView("active");
                    setTab("lists");
                  } else if (current.kind === "rename")
                    await api(base, "PUT", { name: data.get("name") });
                  else if (current.kind === "product" && current.item) {
                    const text = String(data.get("text"));
                    let allowDuplicate = false;
                    if (list && duplicate(list.items, text, current.item.id)) {
                      allowDuplicate = confirm(
                        "Има продукт със същия текст. Да запазим промяната?",
                      );
                      if (!allowDuplicate) return;
                    }
                    await api(`${base}/items/${current.item.id}`, "PUT", {
                      text,
                      version: current.item.version,
                      allowDuplicate,
                    });
                  }
                  setDialog(null);
                });
              }}
            >
              <label>
                {dialog.kind === "product" ? "Продукт" : "Име"}
                <input
                  autoFocus
                  name={dialog.kind === "product" ? "text" : "name"}
                  defaultValue={dialog.kind === "product" ? dialog.item?.text : dialog.name || ""}
                  required
                  maxLength={dialog.kind === "product" ? 200 : 100}
                  placeholder={
                    dialog.kind === "group"
                      ? "Например: Нашият дом"
                      : dialog.kind === "list"
                        ? "Например: Покупки за седмицата"
                        : ""
                  }
                />
              </label>
              {dialog.kind === "group" && (
                <p className="hint">Само поканените участници ще имат достъп до групата.</p>
              )}
              <button className="primary wide" disabled={busy || !online}>
                Запази
              </button>
            </form>
          )}
        </Modal>
      )}
    </div>
  );
}
