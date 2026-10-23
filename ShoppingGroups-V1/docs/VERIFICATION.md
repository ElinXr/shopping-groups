# Verification record — 22 October 2026

## Executed successfully

Final local total: **46 passing tests** (29 domain + 17 frontend). This total excludes every blocked integration scenario.

| Check | Actual result |
| --- | --- |
| `dotnet run --project tests/ShoppingGroups.Domain.Tests -c Release` | 29 passed; real .NET 10 execution |
| `npm test --prefix web` | 17 passed, 0 failed; Node’s test runner |
| `npm run build --prefix web` | TypeScript check and Vite production bundle succeeded |
| `dotnet build tests/ShoppingGroups.Api.Tests` | Integration runner compiled with zero errors/warnings; scenarios not executed against an API |
| `docker compose --env-file .env.example config --quiet` | Development configuration parsed |
| `docker compose --env-file .env.example -f compose.yaml -f compose.production.yaml config --quiet` | Production configuration parsed |
| `docker compose -f compose.tests.yaml config --quiet` | Isolated integration-test configuration parsed |

Frontend dependencies were installed from the existing npm cache after the live registry lookup failed. The generated lockfile records the exact versions and integrities. PWA PNG icons were rendered from the project’s SVG icon. No successful vulnerability audit or external SMTP delivery is claimed.

## Blocked or not completed

| Check | Evidence / limit |
| --- | --- |
| Full production API restore/build | `NU1301`: `api.nuget.org` could not be resolved. Required Identity EF 10/Npgsql 10 packages are not locally cached. Production dependencies remain version 10. |
| Docker image build and PostgreSQL integration execution | Docker CLI exists, but `docker info` cannot reach `dockerDesktopLinuxEngine`. Launching the installed Docker Desktop did not produce an available engine. The actual `docker compose -f compose.tests.yaml up --build --abort-on-container-exit --exit-code-from tests` attempt failed at engine connection, before any test scenarios ran. |
| Local browser/mobile visual QA | Browser origin approval rejected `http://127.0.0.1:5173`; no workaround was attempted. Local HTTP readiness access was also denied by the environment. Vite’s “ready” output alone does not verify a browser session. |
| Public deployment | No connected hosting capable of this backend, no deployment credentials or repository remote. No public app URL exists. |
| Production email, background job execution, real DB migrations and PWA installation | Require the functioning complete stack; not verified in this environment. |

An additional **compile-only diagnostic** used locally cached EF 8 assemblies and a deliberately nonfunctional Identity type stub in ignored `.tools/compile`. It reported zero C#/ASP.NET compile errors. This does **not** prove that the real EF/Identity 10 application builds or runs and is not counted as a passing backend test. No stub or EF 8 dependency is included in the production project, Docker build or CI workflow.

## Coverage available for the next full run

After the user reported Railway unavailable, Render deployment templates were added (`render.yaml` and `deploy/render-postgres.yaml`). Their YAML syntax was checked locally. No provider-side validation or provisioning is claimed; the paid resource choices require review before application. The PostgreSQL template is separate so the web deployment can receive its real Npgsql connection secret before starting. The prior full-stack verification blockers remain unchanged.

The package-free domain runner exercises validation, normalization, empty/completed/reactivated lists, exact 90-day restore boundaries, stale version rejection and summarized-change filtering/counting.

Frontend tests cover duplicate detection, progress, invitation-token validation, SignalR JSON framing and service-worker privacy/offline/cache lifecycle behavior. The production build injects current hashed JS/CSS files into the shell precache.

The HTTP integration runner targets a real PostgreSQL-backed API and Mailpit. Scenarios include independent cookie jars and CSRF; public registration and admin boundaries; private-group access; owner-only actions; invitation use limits/revocation/email verification; several lists; authenticated SignalR handshake and invalidation delivery; cross-member edits/deletes/purchases; explicit duplicates; stale and parallel writes; simultaneous shoppers; one start/completion notification per run; manual summary cursors; reactivation; personal archives; soft-delete restoration; cross-group IDs; activity actors; notification ownership; administration/audit; removal; password reset; user suspension; and logout.

The Docker test stack provisions a disposable database and configured test administrator. The CI workflow and `scripts/check.ps1` / `scripts/check.sh` run the complete suite on a machine with the required access. Nothing in this record treats written tests as executed tests.
