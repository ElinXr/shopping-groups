# Verification record — updated 24 October 2026

## Executed successfully

Latest local total: **72 passing tests** (52 domain + 17 frontend + 3 local Neon environment tests), executed on 24 October. This total excludes every blocked integration scenario.

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

On 24 October the user supplied an existing Neon organization/project and requested CLI setup of its production branch. Both `npm i -g neon@latest --prefix .tools/neon-cli` (workspace-local installation prefix) and the requested fallback `npx --yes neon@latest init -y ... --branch production --services postgres` failed before the CLI could run: `getaddrinfo ENOTFOUND registry.npmjs.org`. No browser login was opened, no installed Neon skills were found, no `neon.ts`/`.neon` or credential file was generated, and `neon deploy` could not run. The project supplied by the user has not been authenticated or inspected from this environment.

The API already uses Npgsql/EF Core and now prefers `DATABASE_URL_UNPOOLED` when Neon supplies it, retaining the explicit `ConnectionStrings__Database` override. `npm run dev:api` loads the CLI's private `.env` or `.env.local` into the server process only. With no credentials pulled, the launch correctly failed with `No database connection configured`. A separate real `dotnet run --project src/ShoppingGroups.Api --no-restore` failed with `NU1301` / `No such host is known. (api.nuget.org:443)` before server startup. No Neon connection, migration, CRUD, session lock, data-protection storage or end-to-end test was verified. The frontend build and isolated tests do not establish that the app runs against Neon.

| Check | Evidence / limit |
| --- | --- |
| Full production API restore/build | `NU1301`: `api.nuget.org` could not be resolved. Required Identity EF 10/Npgsql 10 packages are not locally cached. Production dependencies remain version 10. |
| Docker image build and PostgreSQL integration execution | Docker CLI exists, but `docker info` cannot reach `dockerDesktopLinuxEngine`. Launching the installed Docker Desktop did not produce an available engine. The actual `docker compose -f compose.tests.yaml up --build --abort-on-container-exit --exit-code-from tests` attempt failed at engine connection, before any test scenarios ran. |
| Local browser/mobile visual QA | Browser origin approval rejected `http://127.0.0.1:5173`; no workaround was attempted. Local HTTP readiness access was also denied by the environment. Vite’s “ready” output alone does not verify a browser session. |
| Public deployment | No connected hosting capable of this backend, no deployment credentials or repository remote. No public app URL exists. |
| Production email, background job execution, real DB migrations and PWA installation | Require the functioning complete stack; not verified in this environment. |

An additional **compile-only diagnostic** used locally cached EF 8 assemblies and a deliberately nonfunctional Identity type stub in ignored `.tools/compile`. It reported zero C#/ASP.NET compile errors. This does **not** prove that the real EF/Identity 10 application builds or runs and is not counted as a passing backend test. No stub or EF 8 dependency is included in the production project, Docker build or CI workflow.

## Coverage available for the next full run

The selected zero-budget profile is now Render Free + Neon Free. New code converts `DATABASE_URL` safely, adds a PostgreSQL-backed data-protection key repository and a separate versioned migration, and permits a longer maintenance interval. The code passed the same limited offline compatibility compilation described above. The PostgreSQL key repository and migration still require the real integration run. The integration configuration now exercises database key storage and a second API process validating an existing cookie; those tests are written/compiled, not reported as executed. The free Blueprint's YAML is parsed locally; it has not been applied or validated against an authenticated Render account.

After the user reported Railway unavailable, Render deployment templates were added (`render.yaml` and `deploy/render-postgres.yaml`). Their YAML syntax was checked locally. No provider-side validation or provisioning is claimed; the paid resource choices require review before application. The PostgreSQL template is separate so the web deployment can receive its real Npgsql connection secret before starting. The prior full-stack verification blockers remain unchanged.

The package-free domain runner exercises validation, normalization, empty/completed/reactivated lists, exact 90-day restore boundaries, stale version rejection and summarized-change filtering/counting.

Frontend tests cover duplicate detection, progress, invitation-token validation, SignalR JSON framing and service-worker privacy/offline/cache lifecycle behavior. The production build injects current hashed JS/CSS files into the shell precache.

The HTTP integration runner targets a real PostgreSQL-backed API and Mailpit. Scenarios include independent cookie jars and CSRF; public registration and admin boundaries; private-group access; owner-only actions; invitation use limits/revocation/email verification; several lists; authenticated SignalR handshake and invalidation delivery; cross-member edits/deletes/purchases; explicit duplicates; stale and parallel writes; simultaneous shoppers; one start/completion notification per run; manual summary cursors; reactivation; personal archives; soft-delete restoration; cross-group IDs; activity actors; notification ownership; administration/audit; removal; password reset; user suspension; and logout.

The Docker test stack provisions a disposable database and configured test administrator. The CI workflow and `scripts/check.ps1` / `scripts/check.sh` run the complete suite on a machine with the required access. Nothing in this record treats written tests as executed tests.
