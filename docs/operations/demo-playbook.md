---
title: Demo Playbook - API Integration Lab
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Demo Playbook: API Integration Lab

## Objective and Audience

Demonstrate to a manager or technical reviewer that API integration quality comes from explicit
identity, bounded work, safe degradation, and correlated telemetry—not merely successful HTTP calls.
The target length is ten minutes with questions afterward.

## Pre-demo Checklist

- [ ] Build/tests pass on the selected Git revision.
- [ ] `.env` contains only the integrations planned for the demo.
- [ ] GitHub PAT is minimum-scope and not near rate exhaustion.
- [ ] Entra redirect URI and both permission types are confirmed.
- [ ] Browser session can complete login, or the documented fallback is ready.
- [ ] API, Collector, and LGTM containers are healthy.
- [ ] Swagger and Grafana tabs are open; no secret/configuration screen is shared.
- [ ] One expected failure is rehearsed without relying on a surprise provider outage.

## Credential Safety

- Never screen-share `.env`, shell history, browser developer Authorization/Cookie headers, or Entra
  secret pages.
- Keep shell tracing disabled and avoid verbose `curl`.
- Use Swagger to call server-configured integrations; do not paste credentials into it.
- Use synthetic Basic/HMAC values and a minimum-scope demo PAT.
- Show normalized responses only; avoid personal directory/provider payloads where possible.
- Clear/rotate temporary credentials after the demo if exposure is suspected.

## Start and Verify

```bash
docker compose --env-file .env up --build --detach
docker compose ps
curl --fail --silent http://localhost:8080/health
curl --fail --silent http://localhost:3000/api/health
```

Then run the safe smoke test from a shell with the same optional variables exported:

```bash
set -a
source .env
set +a
scripts/smoke-test.sh
```

## Ten-minute Narrative

| Time | Evidence | Message |
| ---: | --- | --- |
| 0:00–1:00 | Architecture + Swagger | One API demonstrates six identity patterns and explicit boundaries |
| 1:00–2:00 | Health + public posts | Startup/liveness do not depend on optional secrets |
| 2:00–3:00 | Basic + GitHub profile | Credentials are server-owned and attached by handlers |
| 3:00–4:00 | GitHub repos/rate limit | Pagination and provider budgets are normalized and bounded |
| 4:00–6:00 | Microsoft `/me` vs `/users` | User identity and workload identity require different OAuth flows |
| 6:00–7:00 | HMAC valid/replay/invalid | Integrity, freshness, and replay prevention are distinct |
| 7:00–8:30 | `/api/demo` | Concurrent independent calls preserve partial success |
| 8:30–10:00 | Problem trace ID + Grafana | Caller failure correlates to logs, metrics, and traces |

## Demonstration Steps

| Step | Action | Expected | Lesson |
| ---: | --- | --- | --- |
| 1 | Open `/swagger`; call `GET /health` | `200` | Learning surface and liveness work with empty optional credentials |
| 2 | Call `GET /api/public/posts?limit=3` | `200` | No-auth still validates, normalizes, times out, and emits telemetry |
| 3 | Call Basic profile | `200` configured or rehearsed `503` | External credential remains in server handler |
| 4 | Call GitHub profile | `200` configured | PAT becomes a server-side bearer header, never response data |
| 5 | Call repos with `perPage=5&maxPages=2`, then rate limit | `200` | Authoritative Link traversal, hard cap, normalized provider budget |
| 6 | Open login, then call `/me` | `302`, then `200` | Authorization Code represents the signed-in person |
| 7 | Call `/users?top=5` | `200` | Client Credentials represents the workload, independent of cookie |
| 8 | Send freshly signed webhook | `202` | Exact bytes authenticated before parsing |
| 9 | Resend exact webhook; send invalid digest | `401`, `401` | Freshness does not replace one-time replay/MAC validation |
| 10 | Call `/api/demo` with one integration intentionally failing | `200` envelopes | Partial failure does not erase healthy results |
| 11 | Trigger safe failure and use `traceId` in Grafana | Matching log/trace + metrics | Three signals share bounded correlation context |

Use commands and response shapes from the [API reference](../api/api-reference.md). Login/logout are
browser redirect flows.

## Failure Demonstrations

Use controlled failures only:

| Action | Expected | Explain |
| --- | --- | --- |
| Basic values omitted | `503 configuration` | Local configuration differs from upstream failure |
| GitHub PAT omitted | `401 authentication` | Provider rejects missing identity; API maps it safely |
| `maxPages=11` or `top=51` | `400` | Caller bounds fail before network I/O |
| Graph app permission absent | `403 authorization` | Authentication success does not imply permission |
| Modify signed webhook byte | `401` | MAC protects exact wire content |
| Replay accepted signature | `401` | Freshness and one-time replay storage solve different threats |
| One `/api/demo` provider fails | `200` with one failed envelope | Graceful partial success |

Do not deliberately exhaust provider limits or expose/revoke a real credential during the demo.

## Observability Evidence

1. Trigger a safe Basic `503` or GitHub `401`.
2. Copy only Problem Details `traceId`.
3. Search Loki:

   ```logql
   {service_name="api-integration-lab"} |= "<trace-id-from-problem-details>"
   ```

4. Open the corresponding Tempo trace for `service.name=api-integration-lab`.
5. Show aggregate operation outcomes:

   ```promql
   sum by (provider, operation, outcome) (rate(api_client_requests_total[5m]))
   ```

6. Point out that trace IDs, user IDs, bodies, and tokens are not metric labels; the custom schema
   has a computed upper bound of 90 attribute combinations before histogram expansion.

## Questions to Be Ready For

| Question | Short answer | Deep link |
| --- | --- | --- |
| Why one service? | Breadth lab; feature folders preserve provider boundaries without deployment overhead | [ADR 001](../adrs/adr-001-single-service-feature-folders.md) |
| Why two OAuth flows? | They represent different principals: person vs workload | [Authentication](../authentication.md) |
| Why not return the access token? | It expands browser/log/Swagger exposure and is unnecessary | [Security](../../SECURITY.md) |
| What stops retry storms? | Two retries with jitter inside a 15-second total budget and standard circuit/limiter | [Resilience](../resilience-patterns.md) |
| Can it scale horizontally? | Not unchanged; cookie keys/token cache/replay state are local memory | [Architecture](../../ARCHITECTURE.md#scaling-model) |
| Is telemetry durable? | No; local LGTM retention/storage is not guaranteed | [Observability](../observability.md#retention-and-sampling) |

## Cleanup

Preserve stopped containers temporarily:

```bash
docker compose stop
```

Remove containers/network and accept loss of disposable local telemetry:

```bash
docker compose down
```

Clear browser sessions, remove temporary local `.env` values, and rotate a credential if its value
may have appeared on screen. Do not commit `.env` or captured identity/provider payloads.

## Fallback Plan

If internet or an external provider is unavailable:

1. show the architecture/authentication diagrams and runtime Swagger contract;
2. run the credential-free unit/integration suites;
3. show handler/client tests that capture outgoing headers without real credentials;
4. show OAuth routing tests and explain the browser-only manual boundary;
5. show webhook tests for valid, invalid, stale, and replayed signatures;
6. use `/health`, local validation failures, and unconfigured `503` for live HTTP evidence;
7. show source-backed metric schema and Collector validation rather than recorded secrets or personal
   provider responses.

```bash
dotnet test ApiIntegrationLab.sln --configuration Release --no-build --verbosity minimal
```

The fallback still proves contracts and safety boundaries; it explicitly does not claim a live
external end-to-end result.
