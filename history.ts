export interface HistoryPage<T> { items: T[]; hasMore: boolean }
export function emptyHistory<T>(): HistoryPage<T> { return { items: [], hasMore: false }; }
export function mergeHistory<T extends { id: number }>(newer: T[], older: T[]): T[] {
  return [...new Map([...older, ...newer].map(item => [item.id, item])).values()].sort((a, b) => b.id - a.id);
}
export function refreshHistory<T extends { id: number }>(page: HistoryPage<T>, latest: T[]): HistoryPage<T> {
  // A gap larger than one page must reset the cursor; otherwise Load More would skip the gap.
  if (!page.items.length || (latest.length >= 50 && latest.at(-1)!.id > page.items[0].id))
    return { items: latest, hasMore: latest.length === 50 };
  return { items: mergeHistory(latest, page.items), hasMore: page.hasMore };
}
export function appendHistory<T extends { id: number }>(page: HistoryPage<T>, older: T[]): HistoryPage<T> {
  return { items: mergeHistory(page.items, older), hasMore: older.length === 50 };
}
