import { useEffect, useState } from "react";
import { Copy, UserPlus, Mail, LogOut } from "lucide-react";
import { api } from "./api";
import { QR } from "./QR";
import { date } from "./helpers";
import type { GroupDetail, Invite } from "./types";
import type { Run } from "./App";

export function Settings({
  group,
  me,
  run,
  busy,
  tick,
  onLeave,
}: {
  group: GroupDetail;
  me: string;
  run: Run;
  busy: boolean;
  tick: number;
  onLeave: () => void;
}) {
  const [invitations, setInvitations] = useState<Invite[]>([]);
  const [url, setUrl] = useState("");
  const [copied, setCopied] = useState(false);
  const owner = group.role === "owner";
  useEffect(() => {
    let active = true;
    if (owner)
      api<Invite[]>(`/groups/${group.id}/invitations`)
        .then((x) => {
          if (active) setInvitations(x);
        })
        .catch(() => {});
    return () => {
      active = false;
    };
  }, [group.id, owner, tick]);
  return (
    <div className="settings-grid">
      <section className="card">
        <h2>
          Хората в групата <span className="count">{group.members.length}</span>
        </h2>
        <p className="muted">Всеки може да добавя, променя, изтрива и купува продукти.</p>
        <div className="member-list">
          {group.members.map((member) => (
            <div className="member" key={member.userId}>
              <span className="avatar">{member.displayName.slice(0, 1).toUpperCase()}</span>
              <div>
                <strong>
                  {member.displayName}
                  {member.userId === me ? " (вие)" : ""}
                </strong>
                <small>{member.role === "owner" ? "Собственик" : "Участник"}</small>
              </div>
              {owner && member.userId !== me && (
                <div className="member-actions">
                  <button
                    disabled={busy}
                    className="text-button"
                    onClick={() => {
                      if (confirm(`Да прехвърлим собствеността на ${member.displayName}?`))
                        void run(async () => {
                          await api(`/groups/${group.id}/transfer`, "POST", {
                            userId: member.userId,
                          });
                        });
                    }}
                  >
                    Направи собственик
                  </button>
                  <button
                    disabled={busy}
                    className="text-button danger"
                    onClick={() => {
                      if (confirm(`Да премахнем ${member.displayName}?`))
                        void run(async () => {
                          await api(`/groups/${group.id}/members/${member.userId}`, "DELETE");
                        });
                    }}
                  >
                    Премахни
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
        {!owner && (
          <button
            className="secondary danger"
            disabled={busy}
            onClick={() => {
              if (confirm("Да напуснете тази група? Ще загубите достъп до нейните списъци."))
                void run(async () => {
                  await api(`/groups/${group.id}/members/${me}`, "DELETE");
                  onLeave();
                });
            }}
          >
            <LogOut size={17} />
            Напусни групата
          </button>
        )}
        {owner && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const name = new FormData(e.currentTarget).get("name");
              void run(async () => {
                await api(`/groups/${group.id}`, "PUT", { name });
              });
            }}
          >
            <label>
              Име на групата
              <input
                name="name"
                defaultValue={group.name}
                key={group.name}
                required
                maxLength={100}
              />
            </label>
            <button disabled={busy} className="secondary">
              Запази името
            </button>
          </form>
        )}
      </section>
      {owner && (
        <section className="card">
          <h2>
            <UserPlus size={20} />
            Поканете вашите хора
          </h2>
          <p className="muted">Групите са частни. Присъединяването става само с валидна покана.</p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const data = new FormData(e.currentTarget);
              void run(async () => {
                const result = await api<{ url: string }>(
                  `/groups/${group.id}/invitations`,
                  "POST",
                  {
                    email: data.get("email") || null,
                    maxUses: Number(data.get("maxUses")),
                    expiresInDays: Number(data.get("expiresInDays")),
                  },
                );
                setUrl(result.url);
                setCopied(false);
              });
            }}
          >
            <fieldset disabled={busy}>
              <label>
                Имейл (по желание)
                <input
                  type="email"
                  name="email"
                  placeholder="Празно за линк / QR покана"
                  maxLength={254}
                />
              </label>
              <div className="two-columns">
                <label>
                  Валидност (дни)
                  <input
                    type="number"
                    min={1}
                    max={30}
                    defaultValue={7}
                    name="expiresInDays"
                    required
                  />
                </label>
                <label>
                  Брой използвания
                  <input type="number" min={1} max={100} defaultValue={5} name="maxUses" required />
                </label>
              </div>
              <p className="hint">
                Поканата по имейл е еднократна и изисква потвърден адрес на получателя.
              </p>
              <button className="primary">
                <Mail size={17} />
                Създай покана
              </button>
            </fieldset>
          </form>
          {url && (
            <div className="invite-result">
              <QR value={url} />
              <label>
                Линк за покана
                <input value={url} readOnly onFocus={(e) => e.currentTarget.select()} />
              </label>
              <button
                className="secondary"
                onClick={() => {
                  void navigator.clipboard
                    .writeText(url)
                    .then(() => setCopied(true))
                    .catch(() => setCopied(false));
                }}
              >
                <Copy size={17} />
                {copied ? "Копирано" : "Копирай линка"}
              </button>
              <p className="hint">
                Пазете този линк. Всеки с него може да се присъедини, докато е валиден.
              </p>
            </div>
          )}
          <h3>Последни покани</h3>
          {invitations.map((inv) => (
            <div className="invitation" key={inv.id}>
              <div>
                <strong>{inv.email || "Линк / QR"}</strong>
                <small>
                  {inv.revokedAt
                    ? "Отменена"
                    : new Date(inv.expiresAt) < new Date()
                      ? "Изтекла"
                      : `${inv.uses} / ${inv.maxUses} използвания · до ${date(inv.expiresAt)}`}
                </small>
              </div>
              {!inv.revokedAt && (
                <button
                  className="text-button danger"
                  disabled={busy}
                  onClick={() => {
                    void run(async () => {
                      await api(`/groups/${group.id}/invitations/${inv.id}`, "DELETE");
                    });
                  }}
                >
                  Отмени
                </button>
              )}
            </div>
          ))}
        </section>
      )}
    </div>
  );
}
