#!/bin/sh
# Periodic pg_dump of both AirSms databases with age-based pruning.
# Runs inside the stock postgres image, so pg_dump matches the server version.
set -eu

: "${PGHOST:=postgres}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"
: "${BACKUP_DATABASES:?BACKUP_DATABASES is required (space separated)}"
: "${BACKUP_DIR:=/backups}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"
: "${BACKUP_RETENTION_DAYS:=14}"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

run_backup() {
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"
  for database in $BACKUP_DATABASES; do
    target="$BACKUP_DIR/$database-$stamp.dump"
    tmp="$target.partial"
    # Custom format is compressed and restorable per-table with pg_restore.
    if pg_dump -Fc -h "$PGHOST" -U "$PGUSER" "$database" > "$tmp"; then
      mv "$tmp" "$target"
      chmod 600 "$target"
      echo "backup: wrote $target ($(du -h "$target" | cut -f1))"
    else
      rm -f "$tmp"
      echo "backup: FAILED for $database" >&2
    fi
  done

  find "$BACKUP_DIR" -name '*.dump' -type f -mtime "+$BACKUP_RETENTION_DAYS" -print -delete \
    | sed 's/^/backup: pruned /'
}

if [ "${1:-}" = "--once" ]; then
  run_backup
  exit 0
fi

# ponytail: sleep loop instead of cron; swap for crond if you need calendar schedules.
while true; do
  run_backup
  sleep "$BACKUP_INTERVAL_SECONDS"
done
