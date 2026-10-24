# Public deployment

**Current requested option (23 October): zero budget, no fixed trial end.** Use [FREE-HOSTING.md](FREE-HOSTING.md) and the current root `render.yaml` for Render Free + Neon Free. The paid Render example below is retained only as an optional alternative in `deploy/render-paid.yaml`; it is not the selected deployment.

## Current status and missing access

No deployment has succeeded or been claimed. This workspace initially had no files, repository remote, hosting manifest or provider credentials. Only environment variable **names** were inspected for provider access; secret values were not printed. Azure/AWS/Fly/Railway/Render CLIs were not available. The connected Sites service runs Cloudflare Workers and cannot execute this ASP.NET Core/PostgreSQL backend. A Railway integration was discovered but was not installed/connected during this run.

To finish a public release, provide **one** supported route:

1. A Linux VPS with SSH/deployment access, Docker/Compose, a domain whose DNS can point to it, and ports 80/443 available; or
2. A Render account/workspace with permission to deploy a Docker web service, provision PostgreSQL and attach a persistent disk; or
3. A connected Railway account/project with equivalent access, when that platform is available. A repository connection or source-upload mechanism is also needed for either managed provider.

The user reported that Railway itself is currently unavailable. `render.yaml` is now prepared as an alternative; this is not a claim of a verified Railway outage. Render is not connected in this session, and no Render resources have been created.

Both routes additionally need a real outbound SMTP service, sender identity/domain and its credentials if authentication is required. Store credentials as provider secrets or a host-local `.env`, never in tracked source. A production administrator email and strong initial password are needed for bootstrap. The example passwords are deliberately rejected in production.

Dependency verification must first run in an environment with npm/NuGet/container registry access and an operational Docker engine. The full API and integration suite are **not yet verified** here; do not promote this build on the strength of the passing frontend/domain tests alone.

## VPS with automatic HTTPS

1. Copy the project or clone its eventual repository on the host.
2. Copy `.env.example` to `.env`. Set a strong `POSTGRES_PASSWORD`, `DOMAIN`, administrator credentials and **all applicable SMTP values**. `PUBLIC_URL` may also be set to the HTTPS domain; the production override explicitly uses `https://${DOMAIN}`.
3. Point the domain’s A/AAAA records at the server and open inbound TCP 80/443 and UDP 443 if HTTP/3 is desired. Keep database and Mailpit ports private.
4. Run the check scripts/CI and the isolated integration suite.
5. Start:

   ```sh
   docker compose -f compose.yaml -f compose.production.yaml up --build -d
   docker compose -f compose.yaml -f compose.production.yaml logs -f api caddy
   ```

6. Verify `https://YOUR_DOMAIN/health/ready`, registration, a real confirmation email, a second user joining by QR/link, simultaneous shopping in two sessions, automatic completion notifications and restore/archive behavior. Install the PWA from a mobile browser using HTTPS. Confirm that the browser opens a `wss` connection to `/hubs/events`.

Caddy provisions and renews TLS. Its container has the explicit trusted proxy address `172.30.42.10`; if that network conflicts with the host, change the subnet, Caddy IP and `TrustedProxies` together. The API’s development host mapping stays loopback-only; public traffic goes through Caddy. Production disables the Mailpit service: setting a real `SMTP_HOST` is required for outgoing email. Cookies are always Secure in Production, so a plain-HTTP public origin is unsupported.

After the administrator has been created, remove `ADMIN_PASSWORD` and `ADMIN_EMAIL` from the host environment if bootstrap is no longer desired. The existing Identity role remains in PostgreSQL. Use the account reset flow to change the initial password.

## Render alternative

The optional `deploy/render-paid.yaml` describes one Docker web service in Frankfurt, a 1 GB disk for Identity keys and the existing readiness endpoint. `deploy/render-postgres.yaml` separately describes PostgreSQL 17 on the private network. These use **paid** service/database plans and a persistent disk. They are not selected for the user's zero-budget deployment. Review charges before applying either paid example; no purchase or provisioning has been performed.

1. Push the project to a repository that your Render workspace can access. No repository remote is connected here yet.
2. Create the database first in Render's dashboard using `deploy/render-postgres.yaml` as the settings reference (name `shopping-groups-db`, Frankfurt, PostgreSQL 17, private access only). The service deliberately asks for an explicit connection secret rather than assuming Render's URL is accepted by Npgsql.
3. Construct `ConnectionStrings__Database` from the database's internal host, database, user and password using Npgsql syntax: `Host=INTERNAL_HOST;Port=5432;Database=shopping;Username=shopping;Password=SECRET`. Use a connection-string builder or quote/escape special characters in provider-generated passwords correctly. Keep this value only in Render's secret environment settings.
4. For this paid alternative only, create a web-service Blueprint from `deploy/render-paid.yaml`. It does not create another database. Supply the connection string from step 3, bootstrap administrator and SMTP settings requested by the Blueprint. Use the SMTP provider's actual port/TLS settings if they differ from 587/STARTTLS.
5. Confirm `/app/keys` is writable by the Docker image's `app` user (UID 1654). The disk must retain these keys across releases. Check this in the hosted environment; local Docker cannot verify it here.
6. `PublicUrl` references Render's own generated HTTPS URL. For a custom domain, replace it with the verified HTTPS origin before sending invitations. Run the complete release gate below and then perform the first manual deployment.

The initial Blueprint creation can start a build/deployment even with automatic redeployment disabled. Do not apply the Blueprint before reviewing its resources, costs and secrets. No Render account login, external validation, actual image build or public deployment has been completed in this session.

Provider references: [Blueprint fields](https://render.com/docs/blueprint-spec), [persistent disks](https://render.com/docs/disks), [generated public URL](https://render.com/docs/environment-variables), [compute plans](https://render.com/docs/compute-plans).

## Railway (when available)

`railway.toml` selects the root Dockerfile and `/health/ready` health check. Configure these service variables in the provider:

| Variable | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` (public service target port must also be 8080) |
| `PublicUrl` | Full public HTTPS origin, with no path/query |
| `ConnectionStrings__Database` | Npgsql string `Host=…;Port=5432;Database=…;Username=…;Password=…;SSL Mode=Require` using the attached PostgreSQL service values; use provider-recommended TLS verification for public database connections |
| `DataProtection__Path` | `/app/keys` backed by a persistent volume writable by the image’s `app` user (UID 1654) |
| `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` | Initial owner-operated administrator credentials |
| `Smtp__Host`, `Smtp__Port`, `Smtp__From` | Outbound mail server and verified sender |
| `Smtp__Username`, `Smtp__Password`, `Smtp__EnableSsl` | Mail provider credentials and STARTTLS flag; port 587 with `true` for typical submission |

You can now use a PostgreSQL URL in `DATABASE_URL` (the adapter converts it to Npgsql syntax), or supply a native `ConnectionStrings__Database`, which takes precedence. Keep one API replica. If the platform requires a dynamic `PORT`, set the service's public target to 8080 or configure `ASPNETCORE_HTTP_PORTS` to that numeric value. ASP.NET does not automatically interpret a provider's unrelated `PORT` variable.

No source was pushed to a remote or deployed to Railway in this run. The integration must first be connected and the target project/service identified before those actions can be performed.

## Persistence and backups

Keep both `postgres_data` and `protection_keys` volumes. `docker compose down` preserves them; `down --volumes` destroys them and is intended only for the isolated **test** stack. Do not run that command against the normal deployment without a deliberate recovery plan.

Example backup on a POSIX deployment host:

```sh
mkdir -p backups
docker compose exec -T db pg_dump -U shopping -d shopping -Fc > backups/shopping.dump
docker run --rm -v shopping-groups_protection_keys:/keys:ro -v "$PWD/backups:/backup" alpine:3.22 tar -czf /backup/protection-keys.tar.gz -C /keys .
```

Restrict backup permissions and test a restore in a separate deployment. Do not restore over a running production database. Backups can retain deleted content longer than the application’s 90-day restore window; define an operator retention policy.

## Release gate

Before declaring a release complete, record the successful API build, full integration-test exit code, browser/mobile test results, public HTTPS URL, readiness response and real SMTP confirmation. A passing configuration parse or frontend build is not evidence that hosting, PostgreSQL or email delivery succeeded.
