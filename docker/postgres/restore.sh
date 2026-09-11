#!/bin/sh
# Restore one custom-format dump into a database, replacing its contents.
# Usage: restore.sh <database> <dump-file>
# Stop the application services before running this against production.
set -eu

: "${PGHOST:=postgres}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"

database="${1:?database name is required}"
dump="${2:?dump file path is required}"

[ -f "$dump" ] || { echo "restore: $dump not found" >&2; exit 1; }

echo "restore: $dump -> $database on $PGHOST"
pg_restore \
  --clean --if-exists --no-owner --no-privileges \
  -h "$PGHOST" -U "$PGUSER" -d "$database" \
  "$dump"
echo "restore: done"
