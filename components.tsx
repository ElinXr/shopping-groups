import { useEffect, useRef, type ReactNode } from "react";
import { X } from "lucide-react";
import { LogoMark } from "./Brand";

export function Empty({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="empty">
      <span className="empty-icon">
        <LogoMark size={36} />
      </span>
      <h3>{title}</h3>
      <p className="muted">{children}</p>
    </div>
  );
}
export function Modal({
  title,
  children,
  close,
}: {
  title: string;
  children: ReactNode;
  close: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = ref.current!;
    element.showModal();
    return () => element.close();
  }, []);
  return (
    <dialog
      ref={ref}
      onCancel={close}
      onClick={(e) => {
        if (e.target === e.currentTarget) close();
      }}
      aria-labelledby="dialog-title"
    >
      <div className="dialog-heading">
        <h2 id="dialog-title">{title}</h2>
        <button className="icon-button" aria-label="Затвори" onClick={close}>
          <X />
        </button>
      </div>
      {children}
    </dialog>
  );
}
