---
title: Test Strategy
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Test Strategy

## Quality Goals

Tests protect integration boundaries that are easy to get subtly wrong:

- request bounds and normalized contracts;
- credential placement and HTTPS/origin restrictions;
- provider response/error mapping;
- pagination, rate limits, timeouts, cancellation, and partial success;
- exact-byte HMAC, freshness, replay, and bounded bodies;
- authentication routing, Problem Details, and trace correlation;
- telemetry dimension cardinality and sensitive-data exclusion.

## Test Pyramid

| Layer | Current snapshot | Purpose | External dependencies |
| --- | ---: | --- | --- |
| Unit | 82 tests | Client/handler/parser/verifier/policy/aggregator/telemetry behavior | None; controlled handlers/time/users |
| Integration | 28 tests | In-process routing, middleware, DI, auth, status, bodies, logs | None; `WebApplicationFactory` and fakes |
| Smoke | One Bash workflow | Running API, Swagger, public provider, optional credentials, HMAC, login readiness, Grafana | Docker; internet for public/provider checks |
| Manual | Checklist | Browser OIDC/consent/logout and real provider behavior | Real accounts/credentials and internet |

Counts are a 2026-09-17 snapshot, not a gate encoded in source. `dotnet test` discovery/result is
authoritative and this table must be updated when legitimate tests change.

## Unit Test Scope

The unit project uses:

- `StubHttpMessageHandler` to return controlled HTTP status, JSON, and headers without live calls;
- handler capture to assert header attachment/omission and safe HTTPS behavior;
- explicit fake time for HMAC freshness/replay cases;
- fake typed clients for aggregator ordering, concurrency, cancellation, and envelope behavior;
- `MeterListener`/`ActivityListener` to inspect custom telemetry;
- pure parser/options/exception tests for boundary semantics.

Unit tests should pair at least one happy path with expected failures and verify behavior rather than
private implementation structure.

## Integration Test Scope

`WebApplicationFactory<Program>` starts the actual ASP.NET Core pipeline in process. Integration
tests cover:

- `/health`, Swagger/OpenAPI, and startup with optional configuration absent;
- controller routes, model validation, status codes, and response shapes;
- Basic/GitHub/public/Graph/demo endpoints with controlled injected clients;
- cookie versus OpenID Connect challenge routing;
- webhook body/header handling and accepted/rejected results;
- RFC 7807 fields and safe trace ID correlation;
- structured log exclusion of webhook body, signature, and secret.

They deliberately replace live provider boundaries so results are deterministic and secrets are not
required.

## Smoke Test Scope

[`scripts/smoke-test.sh`](../../scripts/smoke-test.sh) checks a running Compose stack:

- API health and generated OpenAPI;
- live JSONPlaceholder request;
- Basic when both environment values are exported;
- GitHub `200` when a PAT is exported, otherwise expected `401`;
- valid/replayed/invalid HMAC when its secret is exported;
- Microsoft login redirect readiness when all three Entra values are exported;
- Grafana health.

It prints check names/status, not response bodies, and keeps shell tracing disabled. “Credential
free” does not mean “offline”: the public route still requires internet/provider availability.

## Credentialed Manual Checks

Real OAuth/browser and provider checks stay manual because they require tenant/account state,
consent, cookies, rate limits, and secrets that should not enter automated test infrastructure.

Use [tests/manual/README.md](../../tests/manual/README.md) and verify:

- Microsoft login/callback, delegated `/me`, app-only `/users`, and logout;
- correct permission distinction and admin consent;
- real GitHub pagination/rate-limit semantics with a minimum-scope PAT;
- Basic and HMAC with synthetic local credentials;
- Grafana receipt of all three signals and trace-ID correlation.

Record skipped checks and reason; do not record tokens or personal provider payloads.

## Test Data and Secret Strategy

- Use fabricated identities, URLs, JSON, timestamps, and marker tokens.
- Keep secrets in local environment only; `.env.example` remains empty.
- Never snapshot Authorization/Cookie headers or real provider bodies.
- Assert sensitive markers are absent from logs/spans, not merely that current logging looks safe.
- Tests must run with no real GitHub, Basic, Microsoft, or webhook values.
- Avoid wall-clock dependence by injecting explicit time where behavior depends on freshness.

## Resilience and Security Coverage

| Boundary | Current evidence |
| --- | --- |
| Standard resilience registration | Timeout/retry configuration tests |
| Provider status mapping | Client tests for auth, throttling, timeout, upstream payloads |
| Caller cancellation | Client and aggregator tests |
| Pagination/origin cap | GitHub pagination/link tests |
| Credential placement | Basic/GitHub handler tests |
| OAuth scheme behavior | Microsoft auth routing and Graph endpoint tests |
| HMAC integrity/freshness/replay/body | Verifier and endpoint tests |
| Safe errors | Integration exception and Problem Details tests |
| Partial success | Aggregator and demo endpoint tests |

Live circuit-breaker state transitions and high-concurrency limiter behavior are not exhaustively
load-tested; client mapping of their exception types is implemented/source-reviewed.

## Observability Coverage

- custom metric tag keys are limited to the documented schema;
- unknown provider or provider/operation pairs are rejected;
- cancellation records `cancelled` without an error type;
- custom activity contains bounded context but excludes PAT/Authorization data;
- incoming W3C trace ID appears in Problem Details;
- webhook secret/body/signature are absent from captured structured logs;
- registration confirms an active OpenTelemetry tracer provider.

Automated tests do not launch Collector/LGTM. Container config validation and live signal arrival are
separate local quality/manual checks.

## Compatibility Matrix

| Dimension | Automated coverage | Manual/config coverage |
| --- | --- | --- |
| .NET | `net8.0`, SDK selected by `global.json` | No multi-SDK matrix |
| OS | Current developer/runner host | Linux container runtime |
| API host | In-process TestServer | Kestrel in Docker |
| Providers | Stubs/fakes | Current live provider APIs when credentials exist |
| Identity | Fake/test schemes and routing | Real Entra browser/tenant consent |
| Telemetry | In-process Meter/Activity/log listeners | Pinned Collector `0.160.0` + LGTM `0.33.0` |

No cross-browser, multi-architecture, Windows-container, or multiple-provider-version matrix exists.

## Local Quality Gates

```bash
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build --verbosity minimal
dotnet format ApiIntegrationLab.sln --verify-no-changes --no-restore
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
docker compose --env-file .env.example config --quiet
git diff --check
```

Run the pinned Collector validation and appropriate manual checks for telemetry/identity changes.
See [CONTRIBUTING.md](../../CONTRIBUTING.md) for review expectations.

## Coverage Gaps

- No load, soak, capacity, or fault-injection suite.
- No browser automation for Entra login, consent, cookie, or logout.
- No mutation testing or fuzz/property suite.
- No CI workflow or enforced merge gate.
- No automated live external-provider contract tests.
- No automated Collector/LGTM end-to-end signal test.
- No container vulnerability scan, SBOM, signing, or provenance verification.
- No multi-replica token/data-protection/replay test because that architecture is not implemented.
- No quantitative code-coverage threshold; behavior/risk coverage is the current criterion.

These are explicit scope boundaries, not evidence that the corresponding production risk is absent.
