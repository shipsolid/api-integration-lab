---
title: Non-Functional Requirements
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Non-Functional Requirements

## Scope and Measurement Status

These are implemented engineering constraints for a single-instance localhost learning lab. They
are not production objectives or service-level indicators. “Verified” means the repository contains
source, configuration, or an automated test that demonstrates the behavior.

## Request and Payload Bounds

| Boundary | Implemented requirement | Enforcement | Why |
| --- | --- | --- | --- |
| Public posts | `limit` 1–100 | Controller annotation and client guard | Bounds response work and memory |
| GitHub page size | `perPage` 1–100 | Controller annotation and client guard | Matches provider range |
| GitHub traversal | `maxPages` 1–10 | Controller annotation and client loop | Caps calls, latency, and rate-budget use |
| Graph users | `top` 1–50 | Controller annotation and client guard | Bounds directory data and response work |
| Webhook body | Maximum 65,536 bytes | Content-Length check plus bounded streaming read | Prevents unbounded buffering before authentication |
| Webhook freshness | Signed time within ±5 minutes | Signature verifier | Limits replay opportunity |
| Webhook replay | One acceptance per digest until expiry | Atomic in-memory cache | Rejects identical delivery inside freshness window |

The webhook reads at most 65,537 bytes so a chunked request without `Content-Length` cannot bypass
the size limit.

## Resilience Budgets

| Control | Exact value | Scope |
| --- | ---: | --- |
| Attempt timeout | 5 seconds | Each outbound attempt |
| Retry count | 2 retries after the initial attempt | Standard transient predicate |
| Backoff | 250 ms base, exponential, jitter enabled | Between retries |
| Total timeout | 15 seconds | Entire outbound resilience pipeline |
| Circuit breaker | Standard handler defaults | Each registered typed-client pipeline |
| Local concurrency/rate limiter | Standard handler defaults | Each registered typed-client pipeline |

The standard transient predicate covers transport failures, HTTP `408`, `429`, and `5xx` outcomes.
Authentication, authorization, configuration, and validation failures are not made retryable by the
application. Retry work must finish inside the total budget, and caller cancellation terminates work
without being reclassified as an upstream timeout.

## Security Baseline

- Basic, GitHub, and Microsoft Graph clients require an HTTPS base URL before attaching credentials.
- Provider credentials and OAuth tokens remain server-side and are excluded from response models,
  logs, metric tags, and traces.
- Protected API endpoints return `401`/`403`, not browser login redirects.
- OIDC state/nonce validation, code redemption, and token caching are delegated to
  Microsoft.Identity.Web.
- Webhook MAC comparison is constant-time and happens over exact bytes before JSON parsing.
- HMAC freshness and one-time replay rejection are separate controls.
- Integration exceptions expose safe messages and categories, never upstream bodies or stack traces.
- The container runs the API as the .NET image's unprivileged `$APP_UID` user.
- `.env` files are ignored; only an empty `.env.example` template is committed.

See [SECURITY.md](../../SECURITY.md) for the threat model and known local-lab limitations.

## Observability and Cardinality

Custom metric dimensions are closed sets:

- seven provider/operation pairs;
- request outcome: `success`, `error`, `cancelled`;
- error type: six values, used only for failed requests;
- token flow: `delegated`, `application`;
- token outcome: `success`, `error`, `cancelled`.

The conservative pre-histogram upper bound is:

```text
requests:      7 pairs × 3 outcomes = 21
duration:      7 pairs × 3 outcomes = 21
errors:        7 pairs × 6 types    = 42
token requests: 2 flows × 3 outcomes = 6
total custom streams                  = 90
```

Histogram backends create additional bucket/time-series expansion; 90 is the custom attribute
combination count, not the final Prometheus series count. Request/trace IDs, URLs, timestamps, user
IDs, repository names, identities, tokens, bodies, signatures, and raw exception text are forbidden
as metric labels.

## Reproducibility

- SDK selection is pinned to `8.0.131` with explicit .NET 8 `latestFeature` roll-forward.
- Application/test NuGet direct versions are explicit in project files.
- Collector `0.160.0` and LGTM `0.33.0` image tags are pinned.
- The Dockerfile uses explicit .NET `8.0` SDK/runtime families and a multi-stage publish.
- Configuration names and safe examples live in `.env.example`; no real credential is required for
  build or automated tests.
- Swagger/OpenAPI is generated from controllers and XML comments at runtime.

The .NET `8.0` image family tags can resolve newer servicing images; reproducibility therefore also
requires recording resolved digests when a particular demo build must be preserved.

## Testability

- Provider clients depend on typed `HttpClient` interfaces/handlers and can use controlled message
  handlers in unit tests.
- Time-dependent webhook verification accepts an explicit `now` and production time is abstracted
  behind `ISystemClock`.
- `WebApplicationFactory<Program>` verifies routing, middleware, DI, status, and response contracts
  without calling live providers.
- Optional credentials can remain empty so health, Swagger, validation, and failure contracts are
  testable on a fresh clone.
- Telemetry tests observe emitted instruments/attributes and verify sensitive tag exclusions.

## Explicitly Undefined Production Targets

The repository defines no current target or guarantee for:

- availability or error budget;
- p50/p95/p99 latency;
- throughput, concurrency, saturation, or capacity;
- durability or delivery guarantee;
- recovery time objective (RTO) or recovery point objective (RPO);
- SLA, support hours, escalation, or on-call response;
- telemetry retention, sampling, alert latency, or dashboard freshness;
- multi-region, multi-zone, or multi-replica behavior.

These values require a real workload, production architecture, ownership model, measurement period,
and business consequence. Inventing targets for a localhost lab would not make them evidence-based.

## Verification Matrix

| Requirement | Evidence | Validation |
| --- | --- | --- |
| Query bounds | Controllers and client guards | Endpoint and client tests |
| Webhook size/freshness/replay | `WebhookController`, `WebhookSignatureVerifier` | Webhook unit/integration tests |
| Retry/timeouts | `HttpClientRegistrationExtensions` | Registration tests and source inspection |
| Safe status mapping | `IntegrationException` and handler | Problem Details tests |
| Optional configuration | Conditional auth registration/options validation | Optional-configuration and auth-routing tests |
| HTTPS credential boundary | Auth handlers/Graph client | Handler/client tests |
| Partial success | `DemoAggregator` | Aggregator and endpoint tests |
| Bounded telemetry | `ApiTelemetry` allowlists | Telemetry unit/safety tests |
| SDK/package/image versions | `global.json`, project files, Compose | Restore/build and configuration review |
| Non-root runtime | Dockerfile `USER $APP_UID` | Image inspection/runtime smoke test |

Standard local gate:

```bash
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
```
