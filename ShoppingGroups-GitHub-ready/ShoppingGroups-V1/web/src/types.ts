export interface User {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  emailNotifications: boolean;
  isAdmin: boolean;
}
export interface Group {
  id: string;
  name: string;
  role: string;
  disabled: boolean;
  version: string;
}
export interface Member {
  userId: string;
  displayName: string;
  role: string;
}
export interface GroupDetail extends Group {
  members: Member[];
}
export interface ListBrief {
  id: string;
  name: string;
  completed: boolean;
  completedAt: string | null;
  deletedAt: string | null;
  total: number;
  purchased: number;
  shoppers: number;
}
export interface Product {
  id: string;
  text: string;
  version: string;
  purchased: boolean;
  purchasedBy: string | null;
  purchasedAt: string | null;
}
export interface ListDetail {
  id: string;
  groupId: string;
  name: string;
  completed: boolean;
  completedAt: string | null;
  archived: boolean;
  items: Product[];
  shoppers: { userId: string; displayName: string; startedAt: string }[];
}
export interface Activity {
  id: number;
  listId: string | null;
  actorName: string;
  kind: string;
  text: string;
  at: string;
}
export interface Notice {
  id: number;
  groupId: string;
  listId: string | null;
  kind: string;
  text: string;
  at: string;
  readAt: string | null;
}
export interface Invite {
  id: string;
  email: string | null;
  expiresAt: string;
  revokedAt: string | null;
  maxUses: number;
  uses: number;
}
export interface Summary {
  count: number;
  summary: string;
  through: number;
}
