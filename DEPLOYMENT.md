# AirSMS single-host production deployment

## Target

Use one Ubuntu LTS VPS with Docker Engine and Docker Compose, at least 2 vCPU
and 8 GB RAM, plus a domain whose A/AAAA records point to the server. This is
the simplest suitable target for the current portfolio workload. It is not
high-availability: PostgreSQL, Kafka, and the application share one failure
domain.

Only SSH, TCP 80, TCP 443, and UDP 443 should be open at the host firewall.
PostgreSQL, Kafka, and application ports remain internal to Docker.

## Build immutable application images

Build on the target host, or build elsewhere and push the same tags to a
private registry:

~~~sh
export IMAGE_TAG="$(git rev-parse --short=12 HEAD)"
docker build -f src/AirSms.Api/Dockerfile -t "airsms-api:$IMAGE_TAG" .
docker build -f src/AirSms.Notifications/Dockerfile -t "airsms-notifications:$IMAGE_TAG" .
docker build -f src/AirSms.Projections.Worker/Dockerfile -t "airsms-projections-worker:$IMAGE_TAG" .
docker build -f src/AirSms.Web/Dockerfile -t "airsms-web:$IMAGE_TAG" src/AirSms.Web
~~~

Copy the production template, replace every placeholder, use the immutable
image tag above, and protect the file:

~~~sh
cp deploy/production.env.example .env.production
chmod 600 .env.production
openssl rand -base64 48
docker run --rm --entrypoint /opt/kafka/bin/kafka-storage.sh apache/kafka:4.1.0 random-uuid
~~~

Use the first command's output for `JWT_SIGNING_KEY` and the second command's
output for `KAFKA_CLUSTER_ID`. Never commit `.env.production`.

## Database migrations and first administrator

Review every committed migration and take a backup before upgrading an
existing installation. Runtime services have automatic migrations disabled.
Apply both migration sets explicitly:

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml run --rm airsms-api-migrate
docker compose --env-file .env.production -f compose.prod.yaml run --rm airsms-notifications-migrate
~~~

For the first deployment only, set the bootstrap password in the current
shell and create the administrator. This is idempotent and fails if the email
already belongs to a non-administrator:

~~~sh
export BOOTSTRAP_ADMIN_PASSWORD='replace-with-a-unique-password'
docker compose --env-file .env.production -f compose.prod.yaml run --rm airsms-bootstrap-admin
unset BOOTSTRAP_ADMIN_PASSWORD
~~~

Leave `BOOTSTRAP_ADMIN_PASSWORD` blank in `.env.production`. Public
registration is disabled in production.

If PostgreSQL's volume already existed before the notifications database was
introduced, create that database manually before running its migrations. Init
scripts run only when PostgreSQL first initializes an empty volume.

## Start and verify

Caddy obtains and renews HTTPS certificates automatically once DNS points to
the server and ports 80/443 are reachable.

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml up -d
docker compose --env-file .env.production -f compose.prod.yaml ps

set -a
. ./.env.production
set +a
curl --fail --show-error "https://$APP_DOMAIN/"
curl --fail --show-error "https://$APP_DOMAIN/health/api"
curl --fail --show-error "https://$APP_DOMAIN/health/notifications"
~~~

Sign in with the bootstrapped administrator and verify incident creation,
assignment, start, resolution, closure, audit projection, notification
delivery, and receipt through the configured SMTP provider.

## Logs and backups

Container logs are limited to five 10 MB files:

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml logs --tail=200
~~~

Back up both databases daily, encrypt the files, copy them off-host, and test
restores regularly:

~~~sh
set -a
. ./.env.production
set +a
mkdir -p backups
chmod 700 backups
docker compose --env-file .env.production -f compose.prod.yaml exec -T postgres \
  pg_dump -Fc -U "$POSTGRES_USER" "$POSTGRES_DB" > "backups/airsms-$(date -u +%Y%m%dT%H%M%SZ).dump"
docker compose --env-file .env.production -f compose.prod.yaml exec -T postgres \
  pg_dump -Fc -U "$POSTGRES_USER" "$AIRSMS_NOTIFICATIONS_DATABASE" > "backups/notifications-$(date -u +%Y%m%dT%H%M%SZ).dump"
chmod 600 backups/*.dump
~~~

Restore only into an empty recovery environment first. Stop application
services before an approved production restore, then use `pg_restore` with
the matching database name. Kafka persistence supports restart recovery but
does not replace database backups.

## Upgrade and rollback

Build or pull a new immutable image tag, take backups, review and run
migrations, update the four image variables, then run `docker compose up -d`.
Rollback application images by restoring the prior tags. Database migrations
are not automatically reversible; restore the pre-upgrade backups when a
schema change is incompatible.
