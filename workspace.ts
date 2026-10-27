export function listCount(count: number): string {
  return `${count} ${count === 1 ? "списък" : "списъка"}`;
}
export function workspaceTitle(view: string): string {
  return view === "archived" ? "Архивирани списъци" : view === "trash" ? "Кошче" : "Моите списъци";
}
