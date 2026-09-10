#!/bin/sh
set -eu

if [ "$AIRSMS_NOTIFICATIONS_DATABASE" = "$POSTGRES_DB" ]; then
  exit 0
fi

psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set=database="$AIRSMS_NOTIFICATIONS_DATABASE" <<'EOSQL'
SELECT format('CREATE DATABASE %I', :'database')
WHERE NOT EXISTS (
  SELECT FROM pg_database WHERE datname = :'database'
) \gexec
EOSQL
