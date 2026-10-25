# Architecture and operations

The production Docker image builds React and serves its generated assets from ASP.NET Core. Browser requests, Identity cookies, CSRF requests and the SignalR WebSocket share one origin. Vite proxies `/api` and `/hubs` to port 5080 for development.

`ShoppingGroups.Domain` contains validation, duplicate normalization, completion, restoration and manual-summary rules. The API owns authorization, Identity, EF persistence and delivery. The two console test executables return a nonzero exit code on failure, so no external unit-test runner is required. `dotnet test` is not the test entry point; use the documented `dotnet run` commands or the check scripts.

## Data model

- Identity user/role/login/claim/token tables; `AppUser` adds a display name, notification preference and suspension state.
- `Groups` and `Members`: composite membership key and owner/member role.
- `Lists`, `Products`: several lists per group, free text, buyer identity/time, soft deletion and completion state.
- `Archives`: `(ListId, UserId)` personal preference.
- `Sessions`: unique active `(ListId, UserId)` using a PostgreSQL partial index.
- `Invitations`: hashed token, recipient, expiry, use limit and revocation.
- `Activities`: append-only user activity with cursor pagination; no edit/delete API.
- `Notifications`: durable per-user inbox with independent read receipts.
- `Emails`: transactional outbox, retries, status and eventual sent-message cleanup.
- `AdminAudits`: moderation and retry records; no edit/delete API.

The initial PostgreSQL schema is an embedded, hand-authored EF migration (`20261022000100_InitialSchema`), not `EnsureCreated`. It includes the Identity schema version 1 used by this app. There is intentionally no scaffolded EF model snapshot: subsequent migrations must be explicit SQL/`MigrationBuilder` migrations with their own IDs. Do not run `dotnet ef migrations add` as if a baseline snapshot existed. If adopting automatic scaffolding, first generate/review a baseline snapshot against an empty disposable database and verify it matches the existing schema. The runtime logs EF’s missing-snapshot warning; the migration itself is still applied and recorded normally. `Products.NormalizedText` stores a SHA-256 digest of the canonical duplicate key, keeping the database index bounded even for Unicode normalization expansion.

## Transactions and realtime

Each group mutation loads the group concurrency version. Products also carry a client-visible version for stale-edit protection. EF commits the business changes, group version, activity, inbox entries and emails in one `SaveChanges` transaction. A concurrent writer loses with 409 and none of its effects commit. Broad group-level serialization deliberately favors correctness over peak throughput for household groups.

SignalR has no client-invokable join method. Connections belong to a server-assigned user channel; the API resolves the current membership for every broadcast. Removed/suspended users receive no subsequent group invalidations. Events contain only a group ID, and the client reloads through authorized API routes. Thirty-second polling and window-focus refresh cover disconnections and a missed invalidation. The lightweight browser transport speaks SignalR’s JSON Hub Protocol over WebSocket with reconnection and keepalive. No long-polling transport is included; polling the data remains the fallback.

The SMTP worker holds a PostgreSQL advisory lock across its batch, uses bounded exponential retry, and stops automatic attempts after 10 failures. Admin can retry. SMTP delivery is **at least once**: a crash after SMTP accepts a message but before `SentAt` commits can duplicate an email. In-app notifications remain transactional. Sent email bodies are removed after 7 days. Queued email bodies necessarily contain links/tokens until delivered; restrict database and backup access.

## Security and operational limits

Identity session cookies are HttpOnly, SameSite=Strict and Secure in production. Mutating API requests, including login/register, require an antiforgery cookie/header pair. Every API request validates the Identity security stamp; suspension/reset revokes old sessions. Rate limiting and Identity lockout limit login abuse. Only explicitly trusted proxies may supply forwarded headers. CSP and referrer policy protect invitation/account URLs; the frontend removes tokens from its visible URL and only keeps a pending invitation in tab session storage until accepted/dismissed.

Run a **single API replica** with the included deployment. Multi-replica operation would require shared data-protection keys, distributed rate limiting and a SignalR backplane. The SQL advisory lock alone does not provide a realtime backplane. In the default Docker/VPS profile, keep both the database backup and the data-protection volume. The free Render/Neon profile instead selects `DataProtection__Storage=Database`: migration `20261023000100_DatabaseProtectionKeys` creates the `ProtectionKeys` table, and an `IXmlRepository` stores/reloads the key-ring XML through independent EF scopes. Those keys are not additionally wrapped by an application-level encryption key; restrict database/backup access and use the provider's encrypted storage. Destroying keys invalidates sessions and outstanding account tokens. A second test-only API instance in the integration stack checks that an existing login cookie is readable by another process using the same database key ring; it is not a multi-replica production configuration.

Database configuration accepts either a native Npgsql connection string or a PostgreSQL `DATABASE_URL`. The native value takes precedence. URL conversion decodes credentials once and quotes them with `DbConnectionStringBuilder` so a semicolon in a password cannot inject another setting. Supported URI query options are explicit; unsupported options fail without printing the secret URL. The free profile requires a **direct** Neon connection because maintenance uses session-level advisory locking. It also uses liveness-only platform probes and a longer background interval so idle infrastructure need not continuously query the database.

Automatic migrations on startup suit a single V1 instance. For stricter operations, apply migrations with a privileged release job and set `Database__MigrateOnStartup=false` for the application’s lower-privilege database role. The readiness endpoint checks database connectivity; a release smoke test must additionally run application scenarios.

Product and activity pagination are intentionally different: products load the selected household list as a whole, activity/inbox/admin endpoints return bounded pages. The UI unread badge counts unread notices in its currently loaded page, not an unbounded global total. There are no bulk imports or file uploads.

Reference contracts checked during implementation: [ASP.NET Core 10 Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0), [Npgsql EF 10](https://www.npgsql.org/efcore/release-notes/10.0.html), [Identity schema defaults](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Identity/EntityFrameworkCore/src/IdentityUserContext.cs), [EF migrator snapshot handling](https://github.com/dotnet/efcore/blob/v10.0.0/src/EFCore.Relational/Migrations/Internal/Migrator.cs).
## Device push delivery

Migration 20261025000100_DevicePushNotifications adds the application VAPID key pair, per-device subscriptions and a transactional delivery outbox. Migration 20261025000200_DetailedDevicePush adds P256dh and Auth for each browser subscription; old clients must refresh their subscription keys through the updated frontend. Keys are generated once with P-256 and retained in PostgreSQL across ephemeral container restarts. Only the public key reaches authenticated clients. Each device is associated with its user, cookie expiry and Identity security stamp; group membership is rechecked before delivery. Endpoint allowlisting and disabled redirects prevent arbitrary server-side requests.

Collaboration.Notify attaches deliveries to new inbox notifications in the same SaveChanges transaction. After commit, PushSignal wakes a separate PushWorker immediately. A distinct PostgreSQL session advisory lock protects batches across processes. Provider acceptance removes a delivery and updates device status; 404/410 revokes the device; bounded retries respect Retry-After. The slow default retry interval avoids continuous Neon activity. Delivery is at least once; provider Topic and browser notification tag coalesce device notices.

Detailed RFC 8291 aes128gcm payloads use the built-in .NET P-256 ECDH, HKDF-SHA256 and AES-GCM primitives with fresh ephemeral keys and salt. An exact RFC Appendix A interoperability vector verifies encryption output. RFC 8292 VAPID uses ECDsa ES256 with the provider origin as audience; there is no new package dependency. JSON is bounded to 3000 UTF-8 bytes before encryption. The service worker displays the decrypted author/time text and opens/focuses the inbox on click; missing or malformed payloads retain a visible generic fallback. Activity summaries preserve the original actor snapshot and action time (product additions use CreatedAt), separately from the notifying user. Display times use Europe/Sofia with daylight saving; stored timestamps remain UTC. Logout revokes only the current installation, whose local subscription is then canceled. [Details and verification boundaries](PUSH-NOTIFICATIONS.md).
