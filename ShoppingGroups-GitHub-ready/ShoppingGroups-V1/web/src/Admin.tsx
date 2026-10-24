import { useEffect, useState } from "react";
import { api } from "./api";
import { date } from "./helpers";
import type { Run } from "./App";
interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  disabled: boolean;
  emailConfirmed: boolean;
}
interface AdminGroup {
  id: string;
  name: string;
  disabled: boolean;
  members: number;
}
interface Audit {
  id: number;
  actorId: string;
  action: string;
  target: string;
  at: string;
}
interface Email {
  id: number;
  to: string;
  subject: string;
  attempts: number;
  lastError: string | null;
}
export function Admin({
  run,
  busy,
  tick,
  me,
}: {
  run: Run;
  busy: boolean;
  tick: number;
  me: string;
}) {
  const [overview, setOverview] = useState<Record<string, number>>({});
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [groups, setGroups] = useState<AdminGroup[]>([]);
  const [audit, setAudit] = useState<Audit[]>([]);
  const [emails, setEmails] = useState<Email[]>([]);
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(0);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    Promise.all([
      api<Record<string, number>>("/admin/overview"),
      api<AdminUser[]>(`/admin/users?q=${encodeURIComponent(query)}&page=${page}`),
      api<AdminGroup[]>(`/admin/groups?page=${page}`),
      api<Audit[]>("/admin/audit"),
      api<Email[]>("/admin/emails"),
    ])
      .then(([o, u, g, a, e]) => {
        if (active) {
          setOverview(o);
          setUsers(u);
          setGroups(g);
          setAudit(a);
          setEmails(e);
          setError("");
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [tick, query, page]);
  const labels: Record<string, string> = {
    users: "Потребители",
    groups: "Групи",
    lists: "Списъци",
    activeShoppers: "Пазаруват сега",
    pendingEmails: "Чакащи имейли",
    failedEmails: "Неуспешни имейли",
  };
  return (
    <div className="admin">
      <h1>Администрация</h1>
      {error && (
        <p className="alert error" role="alert">
          {error}
        </p>
      )}
      <div className="stats">
        {Object.entries(overview).map(([key, value]) => (
          <div className="card" key={key}>
            <strong>{value}</strong>
            <span>{labels[key] || key}</span>
          </div>
        ))}
      </div>
      <section className="card">
        <h2>Потребители</h2>
        <label>
          Търсене
          <input
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setPage(0);
            }}
            placeholder="Имейл или име"
          />
        </label>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Име</th>
                <th>Имейл</th>
                <th>Статус</th>
                <th>Действие</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id}>
                  <td>{u.displayName}</td>
                  <td>
                    {u.email}
                    {u.emailConfirmed ? " ✓" : ""}
                  </td>
                  <td>{u.disabled ? "Спрян" : "Активен"}</td>
                  <td>
                    <button
                      className="secondary"
                      disabled={busy || u.id === me}
                      onClick={() => {
                        if (confirm(`${u.disabled ? "Активиране" : "Спиране"} на ${u.email}?`))
                          void run(async () => {
                            await api(`/admin/users/${u.id}/disabled`, "PUT", {
                              disabled: !u.disabled,
                            });
                          });
                      }}
                    >
                      {u.disabled ? "Активирай" : "Спри"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      <section className="card">
        <h2>Групи</h2>
        {groups.map((g) => (
          <div className="invitation" key={g.id}>
            <div>
              <strong>{g.name}</strong>
              <small>
                {g.members} участници · {g.disabled ? "Спряна" : "Активна"}
              </small>
            </div>
            <button
              className="secondary"
              disabled={busy}
              onClick={() => {
                if (confirm(`${g.disabled ? "Активиране" : "Спиране"} на групата?`))
                  void run(async () => {
                    await api(`/admin/groups/${g.id}/disabled`, "PUT", { disabled: !g.disabled });
                  });
              }}
            >
              {g.disabled ? "Активирай" : "Спри"}
            </button>
          </div>
        ))}
        <div className="toolbar">
          <button className="secondary" disabled={page === 0} onClick={() => setPage((p) => p - 1)}>
            Назад
          </button>
          <span>Страница {page + 1}</span>
          <button
            className="secondary"
            disabled={users.length < 50 && groups.length < 50}
            onClick={() => setPage((p) => p + 1)}
          >
            Напред
          </button>
        </div>
      </section>
      <section className="card">
        <h2>Чакащи имейли</h2>
        {emails.length === 0 ? (
          <p className="muted">Няма чакащи съобщения.</p>
        ) : (
          emails.map((e) => (
            <div className="invitation" key={e.id}>
              <div>
                <strong>{e.subject}</strong>
                <small>
                  {e.to} · {e.attempts} опита · {e.lastError || "Чака изпращане"}
                </small>
              </div>
              <button
                className="secondary"
                disabled={busy}
                onClick={() => {
                  void run(async () => {
                    await api(`/admin/emails/${e.id}/retry`, "POST");
                  });
                }}
              >
                Опитай пак
              </button>
            </div>
          ))
        )}
      </section>
      <section className="card">
        <h2>Административен журнал</h2>
        {audit.map((a) => (
          <p className="audit" key={a.id}>
            {a.action} · {a.target}
            <small>
              {date(a.at)} · {a.actorId}
            </small>
          </p>
        ))}
        {audit.length > 0 && (
          <button
            className="text-button"
            onClick={() => {
              void run(async () => {
                const older = await api<Audit[]>(
                  `/admin/audit?before=${audit[audit.length - 1].id}`,
                );
                setAudit((a) => [...a, ...older]);
              }, false);
            }}
          >
            Още записи
          </button>
        )}
      </section>
    </div>
  );
}
