import type { Group } from "./types.ts";
import { normalize } from "./helpers.ts";
const preferenceKey = (userId: string) => `shopping-group:${userId}`;
export function preferredGroup(userId: string): string {
  try { return localStorage.getItem(preferenceKey(userId)) ?? ""; } catch { return ""; }
}
export function rememberGroup(userId: string, groupId: string): void {
  try { if (groupId) localStorage.setItem(preferenceKey(userId), groupId); } catch { /* Storage is optional. */ }
}
export function chooseGroup(groups: Group[], requestedId: string): Group | undefined {
  return groups.find(group => group.id === requestedId && !group.disabled)
    ?? groups.find(group => !group.disabled) ?? groups[0];
}
export function searchGroups(groups: Group[], query: string): Group[] {
  const words = normalize(query).split(" ").filter(Boolean);
  return groups.filter(group => words.every(word => normalize(group.name).includes(word)))
    .toSorted((a, b) => Number(a.disabled) - Number(b.disabled) || a.name.localeCompare(b.name, "bg-BG"));
}
