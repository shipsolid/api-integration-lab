---
title: Learning Guide
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Learning Guide

This six-module path moves from a no-auth request to identity, replay defense, degradation, and
telemetry. Use the [development guide](../DEVELOPMENT.md) to build/start the stack and keep
[Swagger](http://localhost:8080/swagger) open beside the source. An empty `.env` is enough for the
public route, automated tests, validation, and most expected-failure exercises.

## Module 1: Anonymous HTTP and Normalization

**Learning objective:** Trace one request across HTTP validation, a typed client, provider DTO,
normalized model, safe failure mapping, and tests.

**Read in order:**

1. `PublicApiController.cs`
2. `IPublicApiClient.cs`
3. `PublicApiClient.cs`
4. `PublicPostDto.cs`, then `PublicPost.cs`
5. `PublicApiClientTests.cs`, then `PublicApiEndpointTests.cs`

**Exercise:**

```bash
curl --fail --silent 'http://localhost:8080/api/public/posts?limit=2'
curl --silent --show-error 'http://localhost:8080/api/public/posts?limit=0'
```

**Expected observation:** The first request returns normalized posts with no outbound
`Authorization` header. The second fails with HTTP `400` before provider I/O.

**Explain:** Why keep the wire DTO internal? Why validate in both the controller and client? Why is
no-auth still an integration security/resilience concern?

**Completion check:** Point to the exact source and test that enforce the 1–100 limit and required
provider fields.

## Module 2: Outbound Basic and Bearer Header Handlers

**Learning objective:** Distinguish caller identity from server-owned provider credentials and
centralize credential attachment outside controllers.

**Read in order:**

1. `BasicAuthenticationHandler.cs` and `BasicAuthOptions.cs`
2. `GitHubAuthenticationHandler.cs` and `GitHubOptions.cs`
3. `BasicAuthClient.cs` and `GitHubClient.GetProfileAsync`
4. Both authentication-handler test classes
5. [Authentication guide](authentication.md#basic-authentication)

**Exercise:** Call Basic and GitHub profile endpoints with an empty `.env`, then configure only
synthetic Basic values and a minimum-scope GitHub PAT and repeat.

```bash
curl --silent --show-error http://localhost:8080/api/basic/profile
curl --silent --show-error http://localhost:8080/api/github/profile
```

**Expected observation:** Missing Basic values produce local `503 configuration`; missing GitHub PAT
allows GitHub to demonstrate provider `401`. Configured calls return normalized models and never
return either credential.

**Explain:** Why is Base64 not protection? Why must HTTPS be checked before adding a header? Why
does the server own the provider credentials rather than Swagger?

**Completion check:** Show the delegating-handler registration in `Program.cs` and tests proving
credentials do not cross an unsafe base URL.

## Module 3: GitHub Pagination and Rate Limits

**Learning objective:** Treat continuation links and rate-limit metadata as bounded protocol
contracts rather than incidental headers.

**Read in order:**

1. `GitHubClient.GetRepositoriesAsync`
2. `GitHubLinkParser.cs`
3. `RetryAfterParser.cs`
4. `GitHubPaginationTests.cs` and `GitHubRateLimitTests.cs`
5. [API pagination semantics](api/api-reference.md#pagination-and-rate-limit-semantics)

**Exercise:**

```bash
curl --silent --show-error \
  'http://localhost:8080/api/github/repos?perPage=5&maxPages=1'
curl --silent --show-error \
  'http://localhost:8080/api/github/repos?perPage=5&maxPages=11'
```

**Expected observation:** A configured success reports one fetched page plus normalized rate-limit
state; the invalid bound returns `400`. The client stops on absent `rel="next"`, page cap, or
cancellation and rejects another continuation host.

**Explain:** Why is a full page not proof of another page? How could an unchecked continuation URL
exfiltrate the PAT? How should a caller interpret `Retry-After`?

**Completion check:** Identify the tests for page caps, absent next links, cross-host links, and
rate-limit normalization.

## Module 4: Delegated Versus Workload OAuth

**Learning objective:** Select an OAuth flow based on the represented principal and keep token
acquisition/lifecycle on the server.

**Read in order:**

1. Microsoft registration and cookie post-configuration in `Program.cs`
2. `MicrosoftAuthController.cs`
3. `MicrosoftGraphTokenProvider.cs`
4. `MicrosoftGraphClient.cs`
5. `MicrosoftAuthenticationRoutingTests.cs` and Graph tests
6. [ADR 002](adrs/adr-002-dual-microsoft-oauth-flows.md)

**Exercise:** Configure Entra, open `/api/microsoft/login` in a browser, call `/api/microsoft/me`,
then call `/api/microsoft/users?top=5` with and without a browser session.

**Expected observation:** `/me` requires the encrypted cookie and delegated `User.Read`; `/users`
does not use the cookie and requires application `User.Read.All` with admin consent. Protected API
calls return `401`, not a login `302`.

**Explain:** Why can Client Credentials not represent the signed-in person? Why is `/users`
inbound-anonymous but outbound-authenticated? What does `.default` mean for app permissions?

**Completion check:** Locate separate delegated/application token calls and their separate bounded
telemetry `flow` values.

## Module 5: Inbound HMAC and Replay Defense

**Learning objective:** Authenticate exact wire bytes and distinguish integrity, freshness, replay,
payload validation, and idempotency.

**Read in order:**

1. `WebhookController.cs`
2. `WebhookSignatureVerifier.cs`
3. `WebhookOptions.cs`
4. Verifier unit tests and endpoint integration tests
5. [ADR 003](adrs/adr-003-exact-body-hmac-and-replay-cache.md)

**Exercise:** Use the signed command in the [API reference](api/api-reference.md#hmac-webhook), then
resend it unchanged, change one whitespace byte without resigning, and sign with an expired time.

**Expected observation:** The first valid delivery returns `202`; identical replay, changed bytes,
and stale time return `401` for different verifier reasons. Invalid JSON with a valid MAC returns
`400` only after authentication. Bodies over 65,536 bytes return `413`.

**Explain:** Why verify before parsing? Why does timestamp freshness not stop replay by itself? Why
is constant-time comparison useful? Why is replay rejection not business idempotency?

**Completion check:** Point to body bounding, ±5-minute freshness, fixed-time comparison, atomic
cache insertion, and verify-before-deserialize order.

## Module 6: Resilience, Aggregation, and Observability

**Learning objective:** Keep failure bounded, preserve independent successes, and correlate the
caller contract with logs, metrics, and traces.

**Read in order:**

1. `HttpClientRegistrationExtensions.cs`
2. `IntegrationException.cs` and `IntegrationExceptionHandler.cs`
3. `DemoAggregator.cs`
4. `ApiTelemetry.cs`, `TraceContextMiddleware.cs`, `OpenTelemetryConfiguration.cs`
5. `otel-collector.yaml`
6. Resilience, demo, Problem Details, telemetry, and safety tests

Use [resilience patterns](resilience-patterns.md) for the failure-budget model and the
[observability guide](observability.md) for the signal/cardinality contract.

**Exercise:** After delegated login, call `/api/demo` with GitHub intentionally unconfigured. Then
trigger a provider error, copy its Problem Details `traceId`, and find the request in Grafana.

```bash
curl --silent --show-error http://localhost:8080/api/github/profile
```

**Expected observation:** Up to three transient attempts fit within a 15-second total budget; caller
cancellation remains distinct. `/api/demo` returns `200` with per-provider envelopes when any call
succeeds and `502` only when all known calls fail. The same request is correlated across safe logs,
custom metrics, and traces without secret or payload labels.

**Explain:** Why are auth/validation failures not retried? Why do all provider tasks start before
the first await? Why are unexpected exceptions not partial-success data? Why is `traceId` valid in a
log query but forbidden as a metric label?

**Completion check:** Locate source/tests for 5-second attempt timeout, two retries, 15-second total
timeout, partial success, Problem Details correlation, and bounded label allowlists.

## 10-Minute Manager-Demo Rehearsal

Use the full script in the [demo playbook](operations/demo-playbook.md). Rehearse this time box:

| Minute | Show | Message |
| ---: | --- | --- |
| 0–1 | Architecture and auth matrix | One lab, six identity patterns, explicit trust boundaries |
| 1–2 | Health and public route | Optional secrets do not block startup |
| 2–3 | Basic and GitHub | Credentials are server-owned and provider failures are normalized |
| 3–5 | OAuth `/me` vs `/users` | Human identity differs from workload identity |
| 5–6 | HMAC valid/replay | Integrity, freshness, and replay are separate controls |
| 6–8 | `/api/demo` | Concurrent calls preserve partial success |
| 8–10 | Grafana correlation | One trace ID connects caller failure to three signals |

Completion criterion: deliver the flow without exposing a credential or depending on an
unrehearsed live-provider state; keep a known expected-failure alternative ready.

## Concept Glossary

| Term | Meaning in this lab |
| --- | --- |
| Authentication | Proving an identity or possession of a credential |
| Authorization | Deciding what that authenticated identity may do |
| Delegated identity | Application acts on behalf of a signed-in person |
| Workload identity | Application acts as itself, with application permissions |
| Bearer token | Possession is sufficient to use the token; protect it in transit/storage |
| PAT | Personal access token issued by GitHub, used here as a server-side bearer credential |
| HMAC | Shared-secret message authentication code proving integrity and secret possession |
| Nonce | One-time protocol value used by OIDC to bind/reject replayed authentication responses |
| Replay | Reusing a previously valid authenticated message |
| Pagination | Fetching a bounded result set through provider continuation metadata |
| Rate limiting | Restricting request budget locally or at a provider |
| Idempotency | Repeating an operation has no additional business effect |
| Timeout | Upper time bound for an attempt or complete operation |
| Retry | Repeating a transiently failed operation within a bounded budget |
| Circuit breaker | Temporarily reject calls after repeated failures so a dependency can recover |
| Bulkhead | Isolate/constrain concurrency so one dependency cannot consume all capacity |
| RFC 7807 | Standard Problem Details shape for HTTP API errors |
| Span | Timed unit of trace work with bounded attributes and parent/child context |
| Metric cardinality | Number of unique label combinations producing time series |
| OTLP | OpenTelemetry Protocol used to export logs, metrics, and traces |

## Extension Exercises

- Add a second public operation while preserving normalization and telemetry conventions.
- Design, but do not implement, a distributed webhook replay store and its atomic contract.
- Add a malformed-but-valid JSON provider test and trace its `502 upstream` mapping.
- Calculate series growth before proposing one additional low-cardinality metric label.

Use [CONTRIBUTING.md](../CONTRIBUTING.md) as the acceptance checklist for any implementation.
