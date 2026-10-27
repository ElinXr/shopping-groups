import { useEffect, useLayoutEffect, useRef, type Dispatch, type SetStateAction } from "react";
import { api, ApiError } from "./api";
import { accountTheme, applyTheme, rememberTheme, type Theme } from "./theme";
import type { User } from "./types";

export function useAppearance(user: User | null, setUser: Dispatch<SetStateAction<User | null>>) {
  const revision = useRef(0);
  useLayoutEffect(() => {
    if (!user) return;
    const selected = accountTheme(user.theme, user.id);
    applyTheme(selected);
    rememberTheme(user.id, selected);
  }, [user?.id, user?.theme]);

  useEffect(() => {
    if (!user) return;
    let active = true;
    const userId = user.id;
    const refresh = () => {
      const observedRevision = revision.current;
      void api<User>("/auth/me").then(latest => {
        if (!active || revision.current !== observedRevision) return;
        if (latest.id !== userId) { window.dispatchEvent(new Event("session-expired")); return; }
        setUser(current => current?.id === userId ? { ...current, theme: latest.theme } : current);
      }).catch(() => {});
    };
    window.addEventListener("focus", refresh);
    return () => { active = false; window.removeEventListener("focus", refresh); };
  }, [user?.id, setUser]);

  async function saveTheme(theme: Theme): Promise<void> {
    if (!user) return;
    revision.current++;
    try {
      const saved = await api<{ theme: Theme }>("/auth/preferences", "PUT", { theme, userId: user.id });
      setUser(current => current?.id === user.id ? { ...current, theme: saved.theme } : current);
    } catch (error) {
      if (error instanceof ApiError && error.code === "session_changed") window.dispatchEvent(new Event("session-expired"));
      throw error;
    } finally {
      // Also invalidate focus requests started while the preference was being saved.
      revision.current++;
    }
  }
  return { theme: user ? accountTheme(user.theme, user.id) : "light" as Theme, saveTheme };
}
