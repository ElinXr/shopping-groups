import { Users } from "lucide-react";

export function GroupContext({ name, members, compact = false, label = "Избрана група" }: {
  name: string; members?: number; compact?: boolean; label?: string;
}) {
  return (
    <div className={`group-context ${compact ? "is-compact" : ""}`} role="note" aria-label={label}>
      <span className="group-context-icon" aria-hidden="true"><Users size={compact ? 18 : 24} /></span>
      <div><span className="group-context-label">{label}</span><strong>{name}</strong>
        {members !== undefined && <small>{members} {members === 1 ? "участник" : "участници"} · споделени списъци</small>}
      </div>
    </div>
  );
}
