#!/usr/bin/env bash
# Runs on the VPS in /opt/modmove (called by the CD workflows over SSH, or by hand):
# back up the database, pull the newest images, restart what changed. The API migrates on start.
# Pulling uses the ghcr.io login made once on the VPS (deploy/README.md, first setup).
set -euo pipefail
# Backups hold password hashes, phone numbers and bank accounts: readable by this user only.
umask 077
cd "$(dirname "$0")"

mkdir -p backups
if [ -n "$(docker compose ps --status running -q mysql 2>/dev/null)" ]; then
  backup="backups/modmove-$(date +%Y%m%d-%H%M%S).sql.gz"
  docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" exec mysqldump -uroot --single-transaction --routines --triggers modmove' \
    | gzip > "$backup.part"
  mv "$backup.part" "$backup"
  echo "Backup: $backup"
fi

docker compose pull
docker compose up -d --remove-orphans
# A changed Caddyfile is a bind mount: `up -d` does not restart Caddy for it.
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile

# Keep the 14 newest backups and drop images no container uses.
rm -f backups/*.part
ls -1t backups/*.sql.gz 2>/dev/null | tail -n +15 | xargs -r rm --
docker image prune -f > /dev/null
