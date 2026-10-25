import { useState } from "react";
import { Plus } from "lucide-react";
import type { Product } from "./types";
import { parseQuantity, PRODUCT_UNITS, type ProductDraft, type ProductUnit } from "./products";

export function ProductForm({ item, disabled, compact = false, onSave }: {
  item?: Product; disabled: boolean; compact?: boolean; onSave: (draft: ProductDraft) => Promise<boolean>;
}) {
  const [error, setError] = useState("");
  return <form className={compact ? "add-product product-form" : "product-form"} onSubmit={event => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    setError("");
    try {
      const draft: ProductDraft = { text: String(data.get("text") ?? "").trim(),
        quantity: parseQuantity(String(data.get("quantity") ?? "")), unit: String(data.get("unit")) as ProductUnit };
      void onSave(draft).then(saved => { if (saved && !item) form.reset(); })
        .catch(() => setError("Промяната не беше запазена. Опитайте отново."));
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Проверете количеството."); }
  }}>
    <label className="product-name-field">Продукт
      <input name="text" defaultValue={item?.text ?? ""} required maxLength={200} disabled={disabled}
        autoFocus={!compact} placeholder="Например: мляко" />
    </label>
    <div className="amount-fields">
      <label>Количество
        <input name="quantity" type="text" inputMode="decimal" required pattern="[0-9]+([.,][0-9]{1,3})?"
          defaultValue={String(item?.quantity ?? 1).replace(".", ",")} disabled={disabled} maxLength={10}
          title="Например: 2 или 1,5. Най-много 3 цифри след запетаята." />
      </label>
      <label>Мярка
        <select name="unit" defaultValue={item?.unit ?? "pcs"} disabled={disabled}>
          {PRODUCT_UNITS.map(([key, label]) => <option value={key} key={key}>{label}</option>)}
        </select>
      </label>
    </div>
    {item?.purchased && <p className="hint">Промяна на купен продукт го връща за покупка. Така новото количество няма да се счита за вече купено.</p>}
    {error && <p className="alert error" role="alert">{error}</p>}
    <button type="submit" className={compact ? "primary" : "primary wide"} disabled={disabled}>
      {compact && <Plus size={20} />}<span>{item ? "Запази промените" : "Добави"}</span>
    </button>
  </form>;
}
