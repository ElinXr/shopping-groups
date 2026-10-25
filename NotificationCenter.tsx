import { Bell, Check, ShoppingBag } from "lucide-react";
import { Empty } from "./components";
import { date } from "./helpers";
import type { Notice } from "./types";

export function NotificationCenter({ notices, hasMore, busy, onOpen, onLoadMore }: {
  notices: Notice[]; hasMore: boolean; busy: boolean; onOpen: (notice: Notice) => void; onLoadMore: () => void;
}) {
  return <>
    <div className="page-heading"><div>
      <span className="eyebrow">НОВО ОТ ВАШИТЕ ХОРА</span>
      <h1>Вашите известия</h1><p className="muted">Покупките вървят по-лесно, когато всички са в течение.</p>
    </div></div>
    {notices.length === 0 ? <Empty title="Тук е спокойно">Новините от групата ще се появят тук.</Empty> :
      <div className="notice-list">{notices.map(notice => <button key={notice.id} disabled={busy}
        className={`card notice ${notice.readAt ? "" : "unread"}`} onClick={() => onOpen(notice)}>
        <span className="notice-icon" aria-hidden="true">{notice.kind === "completed" ? <Check /> : notice.kind === "started" ? <ShoppingBag /> : <Bell />}</span>
        <div className="notice-content">
          <h2>{notice.title || (notice.kind === "completed" ? "Покупките са готови" : notice.kind === "started" ? "Някой пазарува за групата" : "Нови промени в списъка")}</h2>
          <p>{notice.text}</p>
          <small>{notice.senderName ? `Изпратено от ${notice.senderName} · ` : ""}{date(notice.at)}</small>
          {notice.listId && notice.listAvailable === false && <small className="notice-context">Списъкът вече не е наличен. Известието остава в историята.</small>}
        </div>
        {!notice.readAt && <i aria-label="Непрочетено" />}
      </button>)}
      {hasMore && <button className="secondary" disabled={busy} onClick={onLoadMore}>По-стари известия</button>}
    </div>}
  </>;
}
