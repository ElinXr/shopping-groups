import { useId, useState } from "react";
import { Check, ChevronDown, Plus, Search, Users } from "lucide-react";
import { Modal } from "./components";
import { searchGroups } from "./group-selection";
import type { Group } from "./types";

export function GroupPicker({ groups, selectedId, disabled, onSelect, onCreate }: {
  groups: Group[]; selectedId: string; disabled: boolean; onSelect: (id: string) => void; onCreate: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const labelId = useId();
  const selected = groups.find(group => group.id === selectedId);
  const visible = searchGroups(groups, query);
  function create() { setOpen(false); onCreate(); }
  return <section className="group-switch group-picker" aria-labelledby={labelId}>
    <span id={labelId} className="group-picker-label">ВАШИТЕ ГРУПИ</span>
    <button type="button" className="group-picker-trigger" aria-haspopup="dialog" aria-expanded={open}
      disabled={disabled || groups.length === 0} onClick={() => { setQuery(""); setOpen(true); }}>
      <Users size={18} aria-hidden="true" />
      <span><strong title={selected?.name}>{selected?.name ?? "Все още нямате група"}</strong>
        <small>{selected?.disabled ? "Временно спряна" : selected?.role === "owner" ? "Вие сте собственик" : selected ? "Споделени покупки" : "Създайте първата си група"}</small></span>
      <ChevronDown size={16} aria-hidden="true" />
    </button>
    <button type="button" className="text-button" disabled={disabled} onClick={create}><Plus size={16} /> Нова група</button>
    {open && <Modal title="Изберете своята група" close={() => setOpen(false)}>
      <p className="muted">Всяка група има собствени хора и списъци. Избраното място се запомня за вашия профил.</p>
      <label className="group-search"><Search size={18} aria-hidden="true" />
        <input autoFocus value={query} onChange={event => setQuery(event.target.value)} placeholder="Търсете по име" aria-label="Търси група по име" />
      </label>
      <div className="group-picker-options" aria-label="Вашите групи">
        {visible.map(group => <button type="button" key={group.id}
          className={`group-option ${group.id === selectedId ? "is-selected" : ""}`}
          aria-current={group.id === selectedId ? "true" : undefined} disabled={group.disabled || disabled}
          onClick={() => { setOpen(false); if (group.id !== selectedId) onSelect(group.id); }}>
          <span className="group-option-avatar" aria-hidden="true">{Array.from(group.name)[0]?.toLocaleUpperCase("bg-BG")}</span>
          <span className="group-option-copy"><strong>{group.name}</strong>
            <small>{group.disabled ? "Временно спряна от администратор" : group.role === "owner" ? "Собственик" : "Участник"}
              {group.memberCount !== undefined && !group.disabled ? ` · ${group.memberCount} ${group.memberCount === 1 ? "участник" : "участници"}` : ""}</small>
          </span>
          {group.id === selectedId && <Check size={19} aria-label="Текуща група" />}
        </button>)}
        {visible.length === 0 && <p className="group-search-empty">Няма група с това име. Опитайте с друга дума или създайте нова.</p>}
      </div>
      <button type="button" className="secondary wide" disabled={disabled} onClick={create}><Plus size={18} /> Създай нова група</button>
    </Modal>}
  </section>;
}
