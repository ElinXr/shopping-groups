import { Check, Moon, Sun } from "lucide-react";
import type { Theme } from "./theme";

export function ThemeSettings({ theme, disabled, onChange }: {
  theme: Theme; disabled: boolean; onChange: (theme: Theme) => void;
}) {
  return (
    <section className="theme-settings" aria-labelledby="appearance-heading">
      <h2 id="appearance-heading">Външен вид</h2>
      <p className="muted">Изберете удобната за вас тема. Запазва се към профила ви за следващото влизане и другите ви устройства.</p>
      <div className="theme-options" role="group" aria-label="Избор на тема">
        {(["light", "dark"] as const).map(value => {
          const Icon = value === "light" ? Sun : Moon;
          return (
            <button type="button" key={value} className={`theme-option ${theme === value ? "is-selected" : ""}`}
              aria-pressed={theme === value} disabled={disabled} onClick={() => { if (theme !== value) onChange(value); }}>
              <Icon size={22} aria-hidden="true" />
              <span>{value === "light" ? "Светла" : "Тъмна"}</span>
              {theme === value && <Check size={17} aria-label="Избрана тема" />}
            </button>
          );
        })}
      </div>
    </section>
  );
}
