---
title: Runbook - API Integration Lab
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Runbook: API Integration Lab

## Service Overview

The local stack contains a .NET 8 API, an OpenTelemetry Collector, and Grafana LGTM. The API calls
JSONPlaceholder, Postman Echo, GitHub REST, Microsoft Entra ID, and Microsoft Graph when the related
routes are exercised. It has no database, queue, durable event store, or production deployment.

| Component | Local endpoint | Health evidence |
| --- | --- | --- |
| API | `http://localhost:8080` | `GET /health` returns `200` |
| Swagger | `http://localhost:8080/swagger` | UI and `/swagger/v1/swagger.json` return `200` |
| Grafana LGTM | `http://localhost:3000` | `GET /api/health` returns `200` |
| Collector | Compose network only | Container running and health extension on internal `:13133` |

## Scope and Ownership

- Owner: Amit Singh.
- Operating scope: personal localhost learning/portfolio lab.
- On-call, support window, response SLA, RTO, and RPO: not defined.
- Repository owns local request validation, credential placement, normalization, resilience,
  errors, and telemetry configuration.
- External providers own their identity systems, data, limits, contracts, and availability.

## Health Checks

```bash
docker compose ps
curl --fail --silent http://localhost:8080/health
curl --fail --silent http://localhost:8080/swagger/v1/swagger.json >/dev/null
curl --fail --silent http://localhost:3000/api/health
```

API health proves process liveness only. It intentionally excludes provider credentials and remote
availability; use the smoke test or route-specific check for integration health.

## Start, Stop, and Restart

First start:

```bash
cp .env.example .env
docker compose --env-file .env up --build --detach
docker compose ps
```

Stop while preserving the current containers and their disposable writable layers:

```bash
docker compose stop
```

Resume stopped containers:

```bash
docker compose start
```

Restart only the API after changing `.env` credentials (recreation is required to load a changed
environment):

```bash
docker compose --env-file .env up --detach --force-recreate api
```

Remove the stack:

```bash
docker compose down
```

No persistent volume is declared. Removing/recreating LGTM can discard local telemetry. Use
`docker compose stop` if the current disposable data is still useful.

## Common Failure Modes

| Symptom | Likely cause | First evidence | Recovery |
| --- | --- | --- | --- |
| API unhealthy/not running | Build, startup, port, or dependency issue | `docker compose ps`; API logs | Resolve first error, rebuild/recreate API |
| Restore `NU1301` | DNS/proxy/corporate CA trust | Build log certificate/feed error | Use corporate CA procedure; never disable TLS |
| Provider `401` | Missing/expired/revoked credential | Safe Problem Details category | Reconfigure minimum-scope credential and recreate API |
| Graph `403` | Missing/wrong permission or consent | Graph route Problem Details | Add correct delegated/app permission; grant admin consent where required |
| GitHub `429` or throttled `403` | Provider rate budget exhausted | `Retry-After`/rate-limit response | Wait until reset; do not loop |
| Graph `/me` `401` | No/expired local session or token acquisition | Route status and trace ID | Complete browser login again |
| HMAC `401` | Exact bytes, format, clock, secret, or replay | Failure detail; no body logging | Generate fresh timestamp/signature over transmitted bytes |
| Collector export failure | LGTM unavailable or endpoint mismatch | Collector logs | Start LGTM; verify `lgtm:4317` |
| Grafana empty | No recent traffic or broken signal path | API → Collector → LGTM logs | Generate traffic and diagnose in order |
| Port `8080`/`3000` conflict | Another local process/container | Compose bind error | Stop conflicting process or intentionally remap Compose port |

## Provider Diagnosis

1. Check API health first; do not blame a provider for a local process failure.
2. Call the single provider route, not `/api/demo`, to isolate the boundary.
3. Inspect safe status/category/trace ID and response headers.
4. Check API logs by trace ID; do not enable body/header logging.
5. Confirm configuration variable names and minimum provider permissions.
6. Respect `Retry-After`; do not repeatedly test a throttled account.

Credential-free checks:

```bash
curl --fail --silent 'http://localhost:8080/api/public/posts?limit=1'
curl --silent --show-error http://localhost:8080/api/basic/profile
curl --silent --show-error http://localhost:8080/api/github/profile
```

Expected: public `200` when internet is available, Basic `503` when unconfigured, and GitHub `401`
when no PAT is configured.

## Authentication Diagnosis

| Pattern | Check |
| --- | --- |
| Basic | Both values set, synthetic only, HTTPS base URL |
| GitHub PAT | Token not expired/revoked, minimum read scope, API recreated after `.env` change |
| Microsoft login | All three settings at startup; exact redirect URI; secret **value**, not object ID |
| Delegated `/me` | Browser completed login; delegated `User.Read` consent; cookie present |
| App-only `/users` | Application `User.Read.All`; tenant admin consent; `top` 1–50 |
| HMAC | Same secret; Unix seconds; `sha256=` + lowercase hex; exact body; ±5 minutes; not replayed |

See [authentication.md](../authentication.md) for lifecycle details.

## Telemetry Pipeline Diagnosis

Diagnose from producer to backend:

```bash
docker compose logs --tail=200 api
docker compose logs --tail=200 otel-collector
docker compose logs --tail=200 lgtm
```

Validate the checked-in Collector config independently:

```bash
docker run --rm \
  --volume "$PWD/otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro" \
  otel/opentelemetry-collector-contrib:0.160.0 \
  validate --config=/etc/otelcol-contrib/config.yaml
```

Generate a fresh request after every restart, then follow the correlation workflow in
[observability.md](../observability.md). A healthy API does not prove telemetry export health.

## Corporate CA Recovery

For a Docker restore failure caused by trusted corporate TLS interception:

```bash
docker build \
  --secret id=ca_bundle,src=/etc/ssl/certs/ca-certificates.crt \
  --tag api-integration-lab:local .

docker compose \
  --file docker-compose.yml \
  --file docker-compose.corporate-ca.yml \
  --env-file .env \
  up --detach --no-build
```

The CA is mounted as an ephemeral BuildKit secret during restore and through the local override
where needed. Never commit a corporate bundle or disable certificate verification.

## Maintenance Procedures

Release validation:

```bash
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build --verbosity minimal
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
```

Rebuild and smoke-test images:

```bash
docker compose --env-file .env build
docker compose --env-file .env up --detach
scripts/smoke-test.sh
```

Run the smoke test from a shell that exports the same optional variables as the running API. It
skips Basic, HMAC, and Microsoft checks without variables and expects GitHub `401` without a token.

Review dependency upgrades with [DEPENDENCIES.md](../../DEPENDENCIES.md); keep Collector/LGTM tags
pinned and rerun all three-signal checks after telemetry upgrades.

## Local Rollback

This is source/image rollback, not production traffic rollback. Preserve current work and build the
known revision in a separate worktree:

```bash
git worktree add ../api-integration-lab-known-good <known-good-revision>
cd ../api-integration-lab-known-good
cp .env.example .env
docker compose --env-file .env up --build --detach
```

Use a real commit hash or tag in place of `<known-good-revision>`. The placeholder is an operator
input, not a repository default. Stop the currently conflicting stack before binding the same local
ports. Do not force-reset a working tree as a rollback mechanism.

## Local Data Recovery

There is no supported application-data recovery because the API stores no durable business data.
The Microsoft token cache and webhook replay state are lost on every API process restart. A browser
cookie may survive when the same container retains its local data-protection keys, but the lost token
cache can still require a new sign-in; recreating the container may invalidate the cookie as well.
LGTM has no repository-declared persistent volume; telemetry removed with its container is not
recoverable through this project. Reproduce the request to generate new local evidence.

## Escalation Boundary

There is no on-call escalation path. For a repository defect, collect redacted command output,
status/category/trace ID, and the smallest reproduction, then contact Amit Singh privately. For
provider availability, identity issuance, consent, or rate-limit policy, use that provider's
official support/documentation; do not expose credentials in a public issue.

Related: [development guide](../../DEVELOPMENT.md), [security](../../SECURITY.md), and
[resilience patterns](../resilience-patterns.md).
