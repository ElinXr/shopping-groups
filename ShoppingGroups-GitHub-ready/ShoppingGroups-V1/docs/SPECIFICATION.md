# Shopping Groups V1 — implementation contract

Source: the available user messages from “Размер на проектите” and the implementation request of 22 October 2026. The source conversation reader returned the assistant replies as unresolved content references, so their hidden text could not be recovered. This document distinguishes confirmed rules from implementation choices.

## Confirmed rules

- Public, mobile-first React/TypeScript PWA; ASP.NET Core 10 Web API; PostgreSQL and EF Core Identity; SignalR realtime.
- Groups are private. Invitations support link, QR and email. Several lists belong to each group.
- Household goods are free text; there is no catalog, price tracking or GPS/maps in V1.
- All group members can add, edit, delete and purchase all products, including products added by somebody else.
- Duplicates produce a warning but can be explicitly allowed.
- Several members can shop at the same time using “Аз ще пазарувам”.
- Starting shopping and completing a list automatically notify group members.
- Manual “Извести” previews and sends a summary of product changes since the last manual summary.
- Adding an unpurchased product to a completed list reactivates it. Completed lists stay visible until archived by a user.
- Deleted lists can be restored for 90 days. Activity history and an admin panel are included.

## Implementation choices where no complete earlier decision was available

| Topic | V1 behavior |
| --- | --- |
| Membership roles | Creator is owner; everyone else is a member. Owner manages group name, invitations and membership. |
| List permissions | Every member can create, rename, soft-delete and restore a list. |
| Archive | Personal to each user, reversible, available for both active and completed lists. New products reactivate state but respect personal archive preferences. |
| Ownership | Owner may transfer to an existing member. An owner must transfer before leaving. No group hard-delete endpoint. |
| Completion | At least one undeleted product, and all undeleted products purchased. Removing the final pending product can complete a nonempty list. Empty lists are active. Unpurchasing also reactivates. |
| Shopping runs | One active session per member per list; multiple different members allowed. First participant starts the run and its notification. Further joins do not repeat it. If all shoppers stop, a later start creates a new run. Completion ends all sessions. Sessions stay active until explicitly ended, completion, removal or suspension; no guessed inactivity timeout. |
| Notifications | Durable in-app inbox, SignalR invalidations and polling fallback. Email copy for confirmed recipients who opt in. In-app delivery remains enabled. No background Web Push/SMS provider in V1. |
| Manual summary | All product mutations since the previous manual send; counts for all changes and details of the latest 20. Automatic start/completion does not consume the manual cursor. Sending includes any newly arrived changes after preview. |
| Duplicate matching | Unicode compatibility normalization, trimmed/collapsed whitespace and invariant uppercase; purchased items count too. Free-text quantities remain distinct. |
| Invitations | Random 256-bit token, only SHA-256 hash stored. Link may allow 1–100 uses, expires in 1–30 days (UI defaults to 7). Email invitations have one use and require a matching confirmed address. Owner can revoke. |
| Accounts | Identity password hashing and cookie sessions; registration, email confirmation, reset, lockout, profile and email preferences. Unconfirmed users can use link invitations, but cannot accept an email-address invitation or receive notification copies. |
| Conflict handling | Optimistic group token serializes group mutations; products additionally require their current version. Conflicts return 409, refresh the client and require retry. Mutations and notifications commit together. |
| Offline | Installable shell is available offline. No private API data is cached by the service worker. Existing on-screen data may remain visible in the current tab. Mutations require a connection; no offline write queue. |
| Retention | At exactly 90 days restore expires. Background cleanup removes the list, products, sessions, personal archive entries and list activity/notifications. Backups may retain data according to the operator’s retention policy. |
| Admin | Environment-provisioned Admin role; user/group suspension, counts, pending email retries and an administrative audit. Public registration cannot request this role. Admin receives no group-content bypass. |

## Explicit boundaries

V1 does not include GPS, store hours, payments, barcode scanning, product catalogs, native iOS/Android packages or background Web Push. The invitation QR is for scanning with the phone’s usual camera. No hosting or delivered production email is claimed until infrastructure confirms it.
