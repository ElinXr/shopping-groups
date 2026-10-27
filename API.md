# API reference

All `/api` routes except registration/login/account-token actions and `/security/csrf` require authentication. Every mutation additionally requires `X-CSRF-TOKEN` obtained from `GET /api/security/csrf` with the same cookie jar. Refresh this token after login/logout. JSON property names use camelCase. IDs are UUIDs unless otherwise noted.

Validation and domain errors use Problem Details with `status`, `title` and, where applicable, `code`. Important codes: `duplicate` (409, can explicitly override), `conflict` (409, reload/retry), `restore_expired` (410), `no_changes` (400), `email_required` (403), `csrf` (400). Outsiders receive 404 for group data to avoid existence disclosure.

| Method /api route | Request / behavior |
| --- | --- |
| GET `/security/csrf` | `{token}` and antiforgery cookie |
| POST `/auth/register` | `{email,password,displayName}`; password at least 10 with Identity complexity |
| POST `/auth/login` | `{email,password}`; persistent cookie |
| POST `/auth/logout` | End cookie session |
| GET `/auth/me` | User profile, email confirmation/preference, `isAdmin` |
| PUT `/auth/profile` | `{displayName,emailNotifications}` |
| POST `/auth/confirm` | `{email,token}` |
| POST `/auth/resend-confirmation` | `{email}` |
| POST `/auth/forgot-password` | `{email}`; generic non-enumerating response |
| POST `/auth/reset-password` | `{email,token,password}` |
| GET, POST `/groups` | List own groups / create `{name}` |
| GET, PUT `/groups/{g}` | Details/members / owner rename `{name}` |
| POST `/groups/{g}/transfer` | Owner transfer `{userId}` |
| DELETE `/groups/{g}/members/{userId}` | Owner removal or own departure; owner must transfer first |
| GET, POST `/groups/{g}/invitations` | Owner list / create `{email?,maxUses,expiresInDays}`; creation returns `{id,url,expiresAt}` |
| DELETE `/groups/{g}/invitations/{i}` | Owner revoke |
| POST `/invitations/accept` | `{token}`; returns `{groupId}` |
| GET `/groups/{g}/activity?before=&listId=` | Up to 50 records, newest first; `before` is last numeric ID |
| GET `/groups/{g}/lists/?view=` | Default active visibility; `archived` for personal archive, `trash` for restore window |
| POST `/groups/{g}/lists/` | `{name}` |
| GET, PUT, DELETE `/groups/{g}/lists/{l}` | Details / rename `{name}` / group-wide soft deletion |
| POST `/groups/{g}/lists/{l}/restore` | Restore before 90-day expiry |
| PUT `/groups/{g}/lists/{l}/archive` | `{archived}` personal preference |
| POST `/groups/{g}/lists/{l}/items` | `{text,quantity:1,unit:"pcs",allowDuplicate:false}`; returns `{id,version}` |
| PUT `/groups/{g}/lists/{l}/items/{p}` | `{text,quantity,unit,version,allowDuplicate:false}` |
| DELETE `/groups/{g}/lists/{l}/items/{p}?version={v}` | Delete using current product version |
| PUT `/groups/{g}/lists/{l}/items/{p}/purchase` | `{purchased,version}` |
| POST `/groups/{g}/lists/{l}/shopping/start` | Start own session, automatic first-start alert |
| POST `/groups/{g}/lists/{l}/shopping/stop` | End own session |
| GET `/groups/{g}/lists/{l}/summary` | `{count,summary,through,entries}` preview |
| POST `/groups/{g}/lists/{l}/notify` | Send current pending summary to all members |
| GET `/notifications/?before=` | Up to 50 own inbox records for accessible groups |
| PUT `/notifications/{numericId}/read` | Mark own notification read |
| GET `/admin/overview` | Admin counts |
| GET `/admin/users?q=&page=` | Admin user metadata, zero-based page of 50 |
| GET `/admin/groups?page=` | Admin group metadata, zero-based page of 50 |
| PUT `/admin/users/{userId}/disabled` | `{disabled}`; cannot disable self |
| PUT `/admin/groups/{g}/disabled` | `{disabled}` |
| GET `/admin/audit?before=` | Administrative audit, up to 50 |
| GET `/admin/emails` | Up to 100 pending outbox status records, no bodies |
| POST `/admin/emails/{numericId}/retry` | Reset failure counter and retry time |

`/health/live` and `/health/ready` are anonymous. The current liveness response includes `{status:"ok",release:"group-theme-v5"}` to identify the deployed update. `/hubs/events` is the authenticated SignalR endpoint; client subscribes to `Changed({groupId})`. Keep `credentials: same-origin` and use `ws`/`wss` according to the page scheme.
# Device push notifications

These routes require authentication and the existing CSRF header for mutations.

| Method /api route | Request / behavior |
| --- | --- |
| GET /push/config | enabled, publicKey; never returns a private VAPID key |
| GET /push/devices/{id} | Own device state: enabled, expiresAt, lastAcceptedAt, lastError; other devices appear disabled |
| PUT /push/devices/{id} | Body: {endpoint,p256dh,auth}. Register/refresh own device with validated P-256 browser encryption keys, bounded to the authenticated cookie expiry and security stamp |
| DELETE /push/devices/{id} | Revoke own device and pending deliveries; 404 for another user's device |
| POST /push/devices/{id}/test | Queue an own-device test push; 202 means queued, not delivered |

POST /auth/logout accepts X-Push-Device (browser installation UUID) to revoke that device alongside the cookie session. The frontend then unsubscribes and rotates the local device identifier. All other devices remain enabled. RFC 8291 aes128gcm encrypts detailed content for the browser. Notification summaries retain the original activity author/time separately from the sender/time of the notification. Display times use Europe/Sofia. Device previews prioritize the latest action; long summaries are bounded while the full inbox text remains available. Device preview settings control visibility on the lock screen. See [setup, behavior and limits](PUSH-NOTIFICATIONS.md).

## Product experience v3 additions

Products return `quantity` (positive decimal, at most three fractional digits, max 999999.999) and `unit` (`pcs`, `kg`, `g`, `l`, `ml`, `pack`). Legacy creates default to 1 pcs; omitted edit fields retain their stored values. A changed product resets purchased/by/at and recomputes completion; an exact no-op keeps its version and purchase. Duplicate warnings compare names independently from quantity and never merge amounts automatically.

GET `/groups` also returns `memberCount` for the authenticated user's groups. Notification records additionally return `title`, `senderName`, and `listAvailable`; an unavailable/deleted list does not remove the historical notification. New summaries use readable sentences with the original action author/time. Old saved notification text is unchanged. See [v3 behavior and verification](PRODUCT-IMPROVEMENTS-V3.md).

## Friendly summary v4

GET `/groups/{g}/lists/{l}/summary` returns up to 20 newest `entries`: `{id,kind,product,previousProduct,actorName,at}`, alongside total `count`, plain-text `summary` and `through`. Previous product is null except for parsed edit activity. Dates remain UTC; display formatting belongs to the client.

POST `/groups/{g}/lists/{l}/notify?through={id}` sends only pending product changes through that cursor and advances LastSummaryActivityId to the last included event. Newer events remain pending. Missing through retains legacy behavior; nonpositive values return 400, and an already-consumed range returns the existing no_changes error. [Details](FRIENDLY-SUMMARY-V4.md).

## Account appearance v5

GET `/auth/me` additionally returns `theme: "light" | "dark"`. PUT `/auth/preferences` accepts `{theme,userId}` and returns `{theme}` after a successful own-account update. It requires authentication and the usual CSRF header. Invalid themes return 400 (code theme); a mismatched optional userId returns 409 (session_changed). It does not edit display name/email options or rotate the security stamp. Legacy `/auth/profile` calls leave appearance intact.

Migration 20261026000100_UserAppearance adds AspNetUsers.Theme with a light default and database check constraint; the value is independent of session cookies and browser storage. [UI behavior and deployment](GROUPS-AND-THEMES-V5.md).
