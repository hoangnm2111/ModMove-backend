# Production deploy

One VPS runs everything with Docker Compose. One domain serves the web app and the API:

```text
https://<DOMAIN>/          → web  (ModMove-web image, static files)
https://<DOMAIN>/api/...   → api  (this repo's image, port 8080, not published)
https://<DOMAIN>/hubs/...  → api  (SignalR WebSocket)
https://<DOMAIN>/health    → api  (Healthy / Unhealthy when MySQL is down)
        Caddy (ports 80/443, automatic HTTPS) ── mysql (volume mysql-data, not published)
```

Branch `production` in this repo and in ModMove-web is what runs. A push to it runs the CD workflow of that repo:
test/build, push the image to `ghcr.io/hoangnm2111/<repo>` (tags `latest` and `sha-<7 chars>`), then
`deploy.sh` on the VPS: database backup, `docker compose pull`, `docker compose up -d`. The API applies EF Core
migrations and seeds reference data on start (`Database__MigrateOnStartup`); demo accounts never run in Production.

## Files

| File | On the VPS | Content |
|---|---|---|
| `docker-compose.yml` | `/opt/modmove/` | The four services; copied by CD on every deploy |
| `Caddyfile` | `/opt/modmove/` | HTTPS and routing; copied by CD |
| `deploy.sh` | `/opt/modmove/` | Backup, pull, restart; copied by CD |
| `.env.example` | `/opt/modmove/.env` | Domain, MySQL passwords, image tags. Created by hand, `chmod 600` |
| `api.env.example` | `/opt/modmove/api.env` | API secrets. Created by hand, `chmod 600` |
| `docker-compose.local.yml`, `Caddyfile.local`, `local.env.example` | not used | Whole system on a developer machine, Development mode (main README) |

## First setup (once)

1. DNS: an `A` record for `<DOMAIN>` to the VPS IP. Firewall: open only 22, 80, 443.
2. VPS: install Docker Engine with the Compose plugin; create a deploy user in the `docker` group with an SSH key;
   `sudo mkdir -p /opt/modmove && sudo chown <user> /opt/modmove`.
3. Create `/opt/modmove/.env` and `/opt/modmove/api.env` from the examples, with new production values.
4. Image pulls: the two GHCR packages (`modmove-backend`, `modmove-web`) are private. Create a classic GitHub
   personal access token with only `read:packages`, from an account that can read both repos, and log in once
   as the deploy user: `docker login ghcr.io -u <github-user>` (paste the token). The VPS keeps this login; CD
   never logs in there, so a manual roll back works too. Renew it before the token expires.
5. GitHub, in **both** repos (Settings → Secrets and variables → Actions):
   secrets `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` (private key of the deploy user), `VPS_KNOWN_HOSTS`
   (output of `ssh-keyscan <VPS_HOST>`), variable `DOMAIN`.
6. Push `production` in ModMove-web first (the web image must exist), then in this repo. Watch the Actions run;
   its last step calls `https://<DOMAIN>/health`.
7. Sign in as the Seed Administrator, change the password, then delete `Seed__Admin__Password` from `api.env`
   (without a password the seed is skipped; `Email` and `PhoneNumber` may stay).
8. PayOS: set the webhook of the production channel to `https://<DOMAIN>/api/webhooks/payos`.

## Day to day

- Release: PR or merge into `production`; CD does the rest.
- Logs: `cd /opt/modmove && docker compose logs -f api`.
- Roll back: set `API_TAG=sha-<7 chars>` (or `WEB_TAG`) in `.env`, then `./deploy.sh`. Back to `latest` after the fix.
  A migration is not rolled back: restore the backup taken before it if the schema must go back.
- `20261111183627_RegistrationHeldUntilVerified` deletes every `users` row with `email_verified = 0` (DECISIONS D1, Q9).
  `deploy.sh` backs up first; check that a fresh backup is in `backups/` before the first deploy that carries it.
- Restore a backup: `gunzip -c backups/<file>.sql.gz | docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql -uroot modmove'`.
- Backups: `deploy.sh` keeps the 14 newest in `/opt/modmove/backups` (owner-only files). Copy them off the VPS too
  (one disk is not a backup).
- Only the `production` branch deploys: a manual run of the workflow from another branch does nothing.

## Limits

- One API instance only: SignalR, rate limits and the map cache live in memory, and migrations run on start.
- Same origin for web and API, so `Cors__AllowedOrigins__0` is only the domain itself.
- The API trusts `X-Forwarded-For` from any sender (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`); never publish port 8080.
