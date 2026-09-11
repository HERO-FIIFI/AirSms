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

Further accounts are created and managed by an administrator in the web UI
under **Users**: promote or demote roles, deactivate or reactivate accounts,
and reset passwords. The last active administrator cannot be demoted or
deactivated, and administrators cannot change their own role or access.

For local development only, `appsettings.Development.json` seeds
`supervisor@airsms.com` / `Supervisor123!` and `admin@airsms.com` /
`Admin123!` on startup. The same mechanism works elsewhere with
`SeedUsers__Enabled=true` and a `SeedUsers__Accounts__N__*` block per account,
or with the `--seed-users` argument.

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

## Logs

Container logs are limited to five 10 MB files:

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml logs --tail=200
~~~

## Backups

The `postgres-backup` service runs with the stack and writes a custom-format
`pg_dump` of both databases into the `postgres-backups` volume every
`BACKUP_INTERVAL_SECONDS` (default daily), pruning dumps older than
`BACKUP_RETENTION_DAYS` (default 14). Its health check turns unhealthy when
no dump younger than two intervals exists, so a stalled backup shows up in
`docker compose ps` and in monitoring.

Take an immediate backup, list what exists, and copy dumps off-host:

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml run --rm postgres-backup-now
docker compose --env-file .env.production -f compose.prod.yaml exec postgres-backup ls -lh /backups
docker run --rm -v airsms_postgres-backups:/backups:ro -v "$PWD/offsite:/out" alpine \
  sh -c 'cp /backups/*.dump /out/'
~~~

Encrypt the copied files and store them outside this host. A backup that only
lives on the server it protects is not a backup.

Restore only into an empty recovery environment first. For an approved
production restore, stop the application services, then restore each database
from the dump you choose:

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml stop airsms-api airsms-notifications airsms-projections-worker
docker compose --env-file .env.production -f compose.prod.yaml run --rm postgres-restore airsms /backups/airsms-20260911T030000Z.dump
docker compose --env-file .env.production -f compose.prod.yaml run --rm postgres-restore airsms_notifications /backups/airsms_notifications-20260911T030000Z.dump
docker compose --env-file .env.production -f compose.prod.yaml start airsms-api airsms-notifications airsms-projections-worker
~~~

Kafka persistence supports restart recovery but does not replace database
backups.

## Monitoring

Every service publishes a Docker health check, so `docker compose ps` is the
first line of monitoring. The `monitoring` profile adds Uptime Kuma for
dashboards, response-time history, and alerting (email, Slack, Teams, webhook):

~~~sh
docker compose --env-file .env.production -f compose.prod.yaml --profile monitoring up -d
ssh -L 3001:127.0.0.1:3001 user@your-server
~~~

It listens only on the server's loopback interface; reach it through the SSH
tunnel above at http://localhost:3001. On first run create the admin account,
then add HTTP monitors for:

- `https://$APP_DOMAIN/health/api` (API and its database)
- `https://$APP_DOMAIN/health/notifications` (notification service and its database)
- `https://$APP_DOMAIN/` (web front end through Caddy)
- a Docker container monitor for `postgres-backup`, so a stalled backup pages you

Attach at least one notification channel to each monitor.

## Rate limiting

The API applies a fixed-window limit of `RATE_LIMIT_PER_MINUTE` requests per
client (per user when authenticated, per IP otherwise) and a separate
`AUTH_RATE_LIMIT_PER_MINUTE` budget per IP on `/api/auth/*`. Rejections
return `429` with a `Retry-After` header. Health endpoints are exempt. Because
the API only receives traffic from Caddy, it trusts `X-Forwarded-For` from the
proxy; do not publish the API port directly.

## Security testing

Every push runs the `Security` job in CI: a NuGet vulnerability audit
(including transitive packages), `npm audit` on production dependencies, and
a Trivy scan for vulnerable dependencies, leaked secrets, and Dockerfile
misconfiguration. Findings appear under the repository's Security tab.

The `Staging security test` workflow runs weekly and on demand against a
deployed environment. Set the `STAGING_URL` repository variable (or pass a
target when dispatching it). It confirms both health endpoints, checks the
security headers Caddy must send, verifies that unauthenticated API access is
refused and that login is rate limited, and then runs an OWASP ZAP baseline
scan, publishing the report as a workflow artifact and opening an issue for
new findings.

## Upgrade and rollback

Build or pull a new immutable image tag, take backups, review and run
migrations, update the four image variables, then run `docker compose up -d`.
Rollback application images by restoring the prior tags. Database migrations
are not automatically reversible; restore the pre-upgrade backups when a
schema change is incompatible.
