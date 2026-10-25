import { Check, Clock3, Pencil, Plus, RotateCcw, Trash2, Users } from "lucide-react";
import { date } from "./helpers";
import { previewProduct, summaryCount, summaryTime } from "./summary-preview";
import type { Summary } from "./types";

const actions = {
  "item.added": { label: "Добавен продукт", icon: Plus, tone: "added" },
  "item.edited": { label: "Променен продукт", icon: Pencil, tone: "edited" },
  "item.purchased": { label: "Купен продукт", icon: Check, tone: "purchased" },
  "item.deleted": { label: "Премахнат продукт", icon: Trash2, tone: "deleted" },
  "item.unpurchased": { label: "Отново за покупка", icon: RotateCcw, tone: "unpurchased" },
};

export function SummaryPreview({ summary, listName, groupName, onRefresh }: {
  summary: Summary;
  listName?: string;
  groupName?: string;
  onRefresh: () => void;
}) {
  const entries = summary.entries ?? [];
  const remaining = Math.max(0, summary.count - entries.length);
  return (
    <section className="share-preview" aria-label="Преглед на известието">
      <p className="share-intro">
        {summary.count ? `Ето какво ще споделите${listName ? ` за „${listName}“` : ""}.` : "Групата вече е в течение. Ще можете да изпратите известие, когато има нова промяна."}
      </p>
      <p className="share-count">{summaryCount(summary.count)}</p>
      {entries.length > 0 ? (
        <ol className="share-changes" aria-label="Промени за споделяне">
          {entries.map(change => {
            const action = actions[change.kind as keyof typeof actions] ?? actions["item.edited"];
            const product = previewProduct(change.product);
            const Icon = action.icon;
            return (
              <li className={`share-change ${action.tone}`} key={change.id}>
                <span className="share-change-icon" aria-hidden="true"><Icon size={20} /></span>
                <div className="share-change-content">
                  <span className="share-action">{action.label}</span>
                  <div className="share-product">
                    <strong>{product.name}</strong>
                    {product.amount && <span className="share-amount">{product.amount}</span>}
                  </div>
                  {change.previousProduct && <p className="share-previous">Преди: {change.previousProduct}</p>}
                  <p className="share-author">
                    <span className="share-avatar" aria-hidden="true">{Array.from(change.actorName)[0]?.toLocaleUpperCase("bg-BG")}</span>
                    <span>{change.actorName}</span>
                    <span aria-hidden="true">·</span>
                    <Clock3 size={12} aria-hidden="true" />
                    <time dateTime={change.at} title={date(change.at)}>{summaryTime(change.at)}</time>
                  </p>
                </div>
              </li>
            );
          })}
        </ol>
      ) : summary.count > 0 ? (
        <div className="share-missing" role="status">
          <p>Обновете прегледа, за да видите подробностите преди изпращане.</p>
          <button type="button" className="secondary" onClick={onRefresh}>Обнови прегледа</button>
        </div>
      ) : null}
      {remaining > 0 && entries.length > 0 && <p className="share-more">И още {remaining} {remaining === 1 ? "промяна" : "промени"}. Пълната история е в списъка.</p>}
      {summary.count > 0 && <p className="share-recipient">
        <Users size={17} aria-hidden="true" />
        <span>До всички участници{groupName ? ` в „${groupName}“` : " в групата"}</span>
      </p>}
    </section>
  );
}
