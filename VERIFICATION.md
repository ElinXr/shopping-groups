# Verification record — updated 26 October 2026

## Groups and durable appearance v5 — latest local evidence

The user requested explicit group context in My Lists, a prominent nicer group switcher, light/dark preferences that outlive sessions, one consistent logo and relief from mobile login focus zoom. Group selection now sits in the global header. A reusable GroupContext identifies the group in the list workspace, list detail and create-list dialog. Brand/LogoMark share the existing icon.svg, matching the existing PNG exports used by PWA/push. Small-screen auth inputs are at least 16px and manual zoom remains enabled.

Appearance is persisted on AppUser.Theme through migration 20261026000100_UserAppearance. Auth me includes theme; authenticated CSRF-protected PUT auth/preferences validates light/dark and the expected account. It does not change security stamps or overwrite name/email settings. Per-account persistent browser hints improve initial rendering, while the database remains authoritative after authentication/focus. A semantic palette covers both themes, including controls, forms, errors, modals, navigation and notifications.

**156 local tests pass:** 82 domain/protocol, 71 frontend/client/worker/appearance/React-markup and 3 environment. The frontend production build succeeds. New cases verify preference validation/isolation, blocked storage, early bootstrap, semantic palette completeness, contrast >=4.5 for the tested text-role/surface pairs, focus contrast, common brand markup, full group context and native zoom availability. One initial light-palette contrast failure (muted text on auth surface 4.43) was corrected rather than weakening the check.

The integration runner compiles with zero warnings/errors and adds five independent HTTP/PostgreSQL cases for initial appearance, durable updates, new cookie-session/device login, profile preservation and wrong-account/anonymous/invalid requests. These have not executed locally. The full API 10 build again fails NU1301 (api.nuget.org DNS), and Docker reports a missing dockerDesktopLinuxEngine pipe. CompileOnly succeeds with cached EF 8/Identity diagnostic stubs and remains separate from production verification.

React markup tests run without a browser or network. No denied browser route was retried; no browser/device visual success or v5 upload/deployment is claimed. The cumulative archive includes earlier updates and requires the existing GitHub API/Docker checks before Render publishing. [Behavior and rollout checks](GROUPS-AND-THEMES-V5.md).


## Friendly summary v4 — latest local evidence

The user supplied a screenshot of the older raw-text summary and requested a better format. The new component presents action cards with product/amount separated from actor/time, correct singular/plural copy, recipient group context and an explicit send action. GET summary adds typed entries; POST notify accepts an optional through cursor so later unseen changes remain pending.

**139 local tests pass:** 80 domain/protocol, 56 frontend/client/history/group-selection/worker/summary-preview and 3 environment. The production frontend build succeeds. New tests cover metadata-preserving preview conversion, previous/current product details, legacy text, quantity display, Bulgarian copy, Sofia relative time/DST/midnight and bounded-send paths. The updated API integration runner compiles with zero warnings/errors and includes a new HTTP/PostgreSQL snapshot-boundary scenario; it is not executed locally.

The full API 10 build and Docker execution remain blocked by the previously recorded NuGet DNS and missing Linux engine. CompileOnly succeeds with EF 8/Identity diagnostic stubs and is not counted as API 10 validation. No browser-origin access was retried after the explicit refusal. No visual, physical-device or live-deployment success is claimed for v4. The complete update archive includes v3 and adds no external dependency. [Usage and deployment](FRIENDLY-SUMMARY-V4.md).


## Product experience v3 — latest local evidence

The user reports that the preceding push change now works and requests clearer notifications, structured quantities, logical fixes and a friendlier group picker. This is user-reported prior-release success, not an independent test of v3.

**128 tests pass locally:** 76 domain/protocol, 49 frontend/API-client/history/group-selection/worker/device, and 3 environment tests. The production frontend build succeeds. New cases cover decimal validation, quantity payloads, duplicate races/cancellation, preserved history/read state, bounded safe CSRF refresh, per-account group preferences, inaccessible-group fallback and readable notification titles/sentences. The API integration runner compiles with zero errors/warnings and adds 10 isolated PostgreSQL/HTTP regression scenarios, which have not executed locally.

The production API build still fails NU1301 (api.nuget.org DNS); Docker has no running dockerDesktopLinuxEngine. A compatibility compile under ignored .tools/compile succeeds with cached EF 8 and a nonfunctional Identity stub; it does not verify API 10 or runtime behavior. The local fixture preview server started, but the browser-origin approval gate explicitly refused http://127.0.0.1:5189; no alternate browser, native UI or network route was attempted. The server was stopped. No visual browser or physical-device check is claimed for v3.

The additive migration retains old data and VAPID keys. No new dependency was added. Full API/Compose checks must pass in the existing GitHub workflow before Render deployment. No v3 upload/deployment is claimed. [Behavior, fixed cases and publishing steps](PRODUCT-IMPROVEMENTS-V3.md).


## Pending detailed push update v2 — latest local evidence

The user reports uploading the earlier push code and seeing no device-notification section in Profile. This establishes that their browser is not showing the updated interface; it does not distinguish an undeployed commit, a failed check, a nested upload path or a stale client cache. No live inspection was attempted after the prior browser-origin rejection.

The current source adds encrypted device messages containing the original action author and time, independently from the user/time sending the summary. The latest action is first in the phone preview. Bulgarian display times use Europe/Sofia including daylight saving. A second additive migration stores browser P256dh/Auth keys; existing VAPID keys and inbox data are retained. `/health/live` now returns `release: push-details-v2` for the user to check after publishing.

**98 local tests pass:** 69 domain/protocol, 26 frontend/service-worker/device and 3 environment checks. The production frontend build succeeds. New checks cover an exact RFC 8291 encryption vector, valid/invalid browser keys, fresh cryptographic randomness, payload/header limits, original author/time versus notifier, winter/summer time, Unicode truncation and detailed visible service-worker notices with safe fallbacks.

The updated integration runner compiles with zero warnings/errors; its independent fake provider verifies VAPID and decrypts aes128gcm. Integration scenarios assert summary metadata and decrypted device content. These scenarios have **not run locally**. The actual API 10 restore/build remains blocked by NuGet DNS, and Docker has no running Linux engine. The ignored compatibility compiler still uses cached EF 8 and a deliberately nonfunctional Identity stub: its success catches C# errors only and is not API 10 or runtime evidence.

New GitHub CI, Render deployment, real-provider acceptance and operating-system display remain unverified for this update. The passing baseline screenshot below predates both push updates. An update-only archive is prepared without extra workflow files; upload into the existing ShoppingGroups-V1 folder, wait for green CI and the same commit to be Live in Render, then enable and test push on each device. See [deployment and device checks](PUSH-NOTIFICATIONS.md).

## Earlier push v1 local evidence — before detailed payloads

The first push version was implemented locally and packaged for user upload; no live deployment was independently confirmed. Its local total was **89 passing tests** (62 domain/protocol, 24 frontend/worker/device, 3 server-environment). The production frontend build succeeds. New protocol tests validate P-256 key generation, independent ES256 signature verification, audience/expiry, empty payload headers and endpoint SSRF restrictions. New worker tests validate visible push display and same-origin inbox navigation. Device tests cover ID rotation and restricted browser storage.

The updated integration runner compiles with zero errors/warnings. Its isolated HTTP listener verifies VAPID signatures against the configured public application key. New API scenarios cover authentication, private subscription ownership, endpoint validation, provider acceptance/410, actual shopping events, membership removal and per-device logout. Compose syntax parses. These scenarios are **written/compiled, not executed** locally: docker info still reports a missing dockerDesktopLinuxEngine pipe, and the real API build attempt fails NU1301 (api.nuget.org DNS). A repeated compatibility compilation under ignored .tools/compile succeeds with zero errors, but uses the existing nonfunctional Identity/EF 8 diagnostic setup and is not counted as an API 10 build.

No live browser/push-provider test, physical-device delivery or new post-update CI success is claimed. The earlier successful CI/deployment screenshots below concern the baseline before this change. Full source and an update-only archive are prepared for user upload; see [PUSH-NOTIFICATIONS.md](PUSH-NOTIFICATIONS.md).

## Latest GitHub Actions evidence supplied by the user

On 25 October the user supplied a screenshot showing **Verify uploaded project #4**, commit **fa44122**, title **Create Dockerfile.dockerignore**, completed successfully in **1m 46s** on `main`. This supersedes the earlier failing online runs recorded below. The configured workflow includes the frontend/config/domain checks, the real .NET API build and the Docker Compose integration command. A successful complete pipeline is therefore supported by the screenshot and the previously supplied workflow, although the successful run's individual test logs and exact integration assertion count were not supplied or independently retrieved. It was executed on GitHub, not by the local environment.

The CI screenshot establishes the workflow outcome; it does not itself establish live hosting, use of the user's Neon database, a public smoke test or email delivery. The test stack uses its own disposable PostgreSQL database. Subsequent hosting evidence is recorded in the next section.

The earlier npm install log reported two high-severity findings. A fresh local `npm audit --prefix web --json` attempt on 25 October failed with `ENOTFOUND registry.npmjs.org`; the specific packages are still unknown. The passing workflow does not constitute a clean npm audit. No speculative dependency changes were made without that report.

## Latest Render deployment evidence supplied by the user

**Latest outcome:** after correcting the bootstrap password, the user supplied a screenshot of the login page loaded at **https://shopping-groups-test.onrender.com/** on 25 October. The public app URL is now known, and successful page loading is supported by the user's browser evidence. The agent's web opens of `/`, `/health/live` and `/health/ready` returned tool-internal access errors and did not establish HTTP or database readiness. A subsequent Browser Use attempt to open `/` was explicitly rejected by the origin approval gate (missing network-egress authority / user-declined permission). No alternate access route was attempted after that rejection. Live login, registration, group collaboration, public SignalR, health readiness and email delivery have not been independently tested by the agent.

The database management console for the user-supplied project is **https://console.neon.tech/app/projects/patient-math-95772841**. That is an owner-facing console link, not a shareable database credential. The previously supplied Render startup log supports an actual database connection; no direct database inspection was performed by the agent.

Earlier on 25 October the user supplied a successful Render Blueprint sync for commit `fa44122`, creating `shopping-groups-test` and setting `PublicUrl`. The next service log showed startup reaching EF migrations (`No migrations were applied. The database is already up to date.`), then failing with `Admin bootstrap failed: Passwords must have at least one non alphanumeric character` at `Workers.cs:100`. This supported successful database access during that attempt; the later login-page screenshot above supersedes this initial blocked startup as the latest deployment evidence.

The required fix is the private Render `Bootstrap__AdminPassword` value: Identity requires length >=10 plus uppercase, lowercase, a digit and a non-alphanumeric character. Setup instructions now spell out all requirements and recommend length >=14. The rejected password prevented initial admin creation; no backend code or password-policy weakening is required. Render's **Save and deploy** option applies the corrected value using the existing build.

The same screenshot contains a missing `libgssapi_krb5.so.2` message. [Npgsql's documentation](https://www.npgsql.org/doc/security.html#gss-session-encryption-gss-api) explicitly identifies this as a handled fallback when Kerberos is unavailable. Later migration/bootstrap log lines in the same process support that it did not prevent the database connection here. The screenshot also reports exit status 139; no independent runtime crash investigation or successful post-password-fix restart is claimed. No secret values from the conversation were copied into source or documentation.

## Executed successfully

The baseline had **72 local passing tests** on 24 October. The latest detailed push update has 98, as recorded above. Both totals exclude every blocked integration scenario.

| Check | Actual result |
| --- | --- |
| `dotnet run --project tests/ShoppingGroups.Domain.Tests -c Release --no-restore` | 52 passed on 24 October; real .NET 10 execution |
| `npm test --prefix web` | 17 passed, 0 failed; Node’s test runner |
| `npm run build --prefix web` | TypeScript check and Vite production bundle succeeded |
| `npm run build` from the project root | Frontend production build succeeded; `built in 651ms`. This command builds the frontend, not the .NET API. |
| `npm run test:config` | 3 passed: Neon dotenv file selection, quoted credentials and inherited server environment precedence |
| `dotnet build tests/ShoppingGroups.Api.Tests` | Integration runner compiled with zero errors/warnings; scenarios not executed against an API |
| `docker compose --env-file .env.example config --quiet` | Development configuration parsed |
| `docker compose --env-file .env.example -f compose.yaml -f compose.production.yaml config --quiet` | Production configuration parsed |
| `docker compose -f compose.tests.yaml config --quiet` | Isolated integration-test configuration parsed |

Frontend dependencies were installed from the existing npm cache after the live registry lookup failed. The generated lockfile records the exact versions and integrities. PWA PNG icons were rendered from the project’s SVG icon. No successful vulnerability audit or external SMTP delivery is claimed.

## Blocked or not completed

### Reported GitHub Actions failure and package correction

The user supplied the first online Actions log: the frontend build, local environment tests and domain runner completed, but the API restore failed with NU1902/NU1903 for transitive `System.Security.Cryptography.Pkcs` 9.0.0. This is a real vulnerability-audit failure, not a compiler diagnostic or a Neon connection failure. Both Microsoft advisories [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) and [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) list 10.0.13 as patched. Its .NET 10 asset has no further package dependencies, according to [NuGet](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.13).

The API project now directly references `System.Security.Cryptography.Pkcs` 10.0.13 so NuGet selects that patched version over the older transitive minimum. Other dependencies, EF migration code, `TreatWarningsAsErrors` and NuGet audit behavior remain unchanged. The subsequent successful pipeline supplied on 25 October is recorded above; at the time of this initial correction the online build was still pending.

Local post-fix validation parsed the project XML, confirmed exactly one Pkcs reference at 10.0.13 and confirmed warnings-as-errors remains enabled. The real `dotnet build src/ShoppingGroups.Api -c Release` was attempted again and failed with NU1301 / `No such host is known. (api.nuget.org:443)` before package resolution or compilation. Unchanged frontend/domain tests were not repeated for this package-only change.

### Reported Docker packaging failure and build-context correction

The next user-supplied CI log reached the Docker integration stage. Its API package restore succeeded and the image's frontend build plus all 17 frontend / 52 domain tests passed. `dotnet publish --no-restore` then failed with NETSDK1064 for `Microsoft.CodeAnalysis.Analyzers` 3.11.0. Containers and integration scenarios had not started. Reaching this stage also indicates the preceding standalone API build was no longer the failing step; it is not evidence of a working deployed application.

The log reports a 2-byte `.dockerignore` transfer and a 145.98 MB build context after the runner generated `obj`/`bin` and `node_modules`. The likely cause is host `project.assets.json` being copied over the container-generated file by `COPY src/ src/`, leaving NuGet cache paths pointing outside the container. [Microsoft's NETSDK1064 guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1064) specifically recommends ignoring `bin` and `obj` for Docker builds. The existing root ignore file evidently was not effective in that run; its exact remote contents have not been inspected.

Added a visible `Dockerfile.dockerignore` next to `Dockerfile`, excluding generated .NET/Node output and private dotenv files. [Docker's documented precedence](https://docs.docker.com/build/concepts/context/#filename-and-location) selects this Dockerfile-specific file over `.dockerignore`. This avoids relying on manual uploads preserving a hidden filename. The root `.dockerignore` was aligned with the same exclusions. No application or package versions changed for this fix. The user subsequently supplied the successful pipeline screenshot recorded above.

The actual local `docker compose -f compose.tests.yaml build --progress plain` attempt failed before a build began because Docker could not write `C:\Users\elinx\.docker\buildx\.lock` (`Access is denied`). A read-only `docker info` also confirmed the configured `dockerDesktopLinuxEngine` pipe is unavailable. Both ignore files were checked for matching rules and the required output/credential exclusions. No passing post-fix Docker build or integration execution is claimed. Unchanged application tests were not repeated for an ignore-file correction. The supplied npm install log also reports two high-severity findings without naming packages; they remain untriaged until a detailed audit is available, and no clean npm audit is claimed.

### Neon setup attempt

On 24 October the user supplied an existing Neon organization/project and requested CLI setup of its production branch. Both `npm i -g neon@latest --prefix .tools/neon-cli` (workspace-local installation prefix) and the requested fallback `npx --yes neon@latest init -y ... --branch production --services postgres` failed before the CLI could run: `getaddrinfo ENOTFOUND registry.npmjs.org`. No browser login was opened, no installed Neon skills were found, no `neon.ts`/`.neon` or credential file was generated, and `neon deploy` could not run. The project supplied by the user has not been authenticated or inspected from this environment.

The API already uses Npgsql/EF Core and now prefers `DATABASE_URL_UNPOOLED` when Neon supplies it, retaining the explicit `ConnectionStrings__Database` override. `npm run dev:api` loads the CLI's private `.env` or `.env.local` into the server process only. With no credentials pulled, the launch correctly failed with `No database connection configured`. A separate real `dotnet run --project src/ShoppingGroups.Api --no-restore` failed with `NU1301` / `No such host is known. (api.nuget.org:443)` before server startup. No Neon connection, migration, CRUD, session lock, data-protection storage or end-to-end test was verified. The frontend build and isolated tests do not establish that the app runs against Neon.

| Check | Evidence / limit |
| --- | --- |
| Full production API restore/build | `NU1301`: `api.nuget.org` could not be resolved. Required Identity EF 10/Npgsql 10 packages are not locally cached. Production dependencies remain version 10. |
| Docker image build and PostgreSQL integration execution | Docker CLI exists, but `docker info` cannot reach `dockerDesktopLinuxEngine`. Launching the installed Docker Desktop did not produce an available engine. The actual `docker compose -f compose.tests.yaml up --build --abort-on-container-exit --exit-code-from tests` attempt failed at engine connection, before any test scenarios ran. |
| Local browser/mobile visual QA | Browser origin approval rejected `http://127.0.0.1:5173`; no workaround was attempted. Local HTTP readiness access was also denied by the environment. Vite’s “ready” output alone does not verify a browser session. |
| Public deployment / agent smoke test | The user deployed via Render and supplied the public login-page screenshot. Agent access to the live domain was rejected by Browser Use authorization; login and public end-to-end behavior remain unverified by the agent. |
| Production email, background job execution, real DB migrations and PWA installation | Require the functioning complete stack; not verified in this environment. |

An additional **compile-only diagnostic** used locally cached EF 8 assemblies and a deliberately nonfunctional Identity type stub in ignored `.tools/compile`. It reported zero C#/ASP.NET compile errors. This does **not** prove that the real EF/Identity 10 application builds or runs and is not counted as a passing backend test. No stub or EF 8 dependency is included in the production project, Docker build or CI workflow.

## Coverage available for the next full run

The selected zero-budget profile is Render Free + Neon Free. The implementation converts `DATABASE_URL` safely, adds a PostgreSQL-backed data-protection key repository and a separate versioned migration, and permits a longer maintenance interval. The integration configuration exercises database key storage and a second API process validating an existing cookie. Its configured CI job now has a user-supplied successful outcome, as recorded above; per-scenario logs were not supplied. The user also applied the free Blueprint in their Render account and supplied startup and login-page evidence. Direct provider credentials and live app inspection remain unavailable to the agent.

After the user reported Railway unavailable, Render deployment templates were added (`render.yaml` and `deploy/render-postgres.yaml`). Their YAML syntax was checked locally. No provider-side validation or provisioning is claimed; the paid resource choices require review before application. The PostgreSQL template is separate so the web deployment can receive its real Npgsql connection secret before starting. The prior full-stack verification blockers remain unchanged.

The package-free domain runner exercises validation, normalization, empty/completed/reactivated lists, exact 90-day restore boundaries, stale version rejection and summarized-change filtering/counting.

Frontend tests cover duplicate detection, progress, invitation-token validation, SignalR JSON framing and service-worker privacy/offline/cache lifecycle behavior. The production build injects current hashed JS/CSS files into the shell precache.

The HTTP integration runner targets a real PostgreSQL-backed API and Mailpit. Scenarios include independent cookie jars and CSRF; public registration and admin boundaries; private-group access; owner-only actions; invitation use limits/revocation/email verification; several lists; authenticated SignalR handshake and invalidation delivery; cross-member edits/deletes/purchases; explicit duplicates; stale and parallel writes; simultaneous shoppers; one start/completion notification per run; manual summary cursors; reactivation; personal archives; soft-delete restoration; cross-group IDs; activity actors; notification ownership; administration/audit; removal; password reset; user suspension; and logout.

The Docker test stack provisions a disposable database and configured test administrator. The CI workflow and `scripts/check.ps1` / `scripts/check.sh` run the complete suite on a machine with the required access. Nothing in this record treats written tests as executed tests.
