---
title: Contributing
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Contributing

This repository is a personal learning lab, but changes should still be easy to review, reproduce,
and learn from. Keep each change focused on an integration boundary, preserve safe defaults, and
update the documentation that explains the affected behavior.

## Prerequisites

- .NET SDK `8.0.131` or a compatible .NET 8 feature band selected by [`global.json`](global.json);
- Docker with Compose v2 for the full API and telemetry stack;
- `curl` and `openssl` for [`scripts/smoke-test.sh`](scripts/smoke-test.sh);
- `shellcheck` when changing shell scripts;
- provider credentials only for the authenticated integration being exercised.

See [DEVELOPMENT.md](DEVELOPMENT.md) for setup commands and troubleshooting.

## Development Setup

```bash
cp .env.example .env
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build
```

Run only the API during a short code/test loop:

```bash
dotnet run --project src/ApiIntegrationLab.Api
```

Run the complete local stack when validating telemetry or container behavior:

```bash
docker compose --env-file .env up --build --detach
scripts/smoke-test.sh
```

## Repository Structure

| Path | Responsibility |
| --- | --- |
| `src/ApiIntegrationLab.Api/Integrations/` | Provider-facing controllers, typed clients, DTOs, models, options, and auth handlers |
| `src/ApiIntegrationLab.Api/Authentication/` | Inbound HMAC and Microsoft identity helpers |
| `src/ApiIntegrationLab.Api/Common/` | Cross-cutting errors, resilience, telemetry, OpenAPI, time, and shared result types |
| `tests/ApiIntegrationLab.UnitTests/` | Isolated client, parser, policy, telemetry, and verifier tests |
| `tests/ApiIntegrationLab.IntegrationTests/` | In-process HTTP contract and middleware tests |
| `tests/manual/` | Deliberately manual browser/provider verification |
| `docs/` | Learning, architecture, API, security, operations, and decision records |

## Coding Guidelines

- Keep nullable reference types enabled and treat compiler warnings as design feedback.
- Prefer immutable records for normalized API contracts and small interfaces for provider clients.
- Organize provider behavior by feature folder; do not introduce a generic provider abstraction
  unless two real integrations have the same contract and failure semantics.
- Validate caller-controlled bounds before network I/O.
- Pass `CancellationToken` through controllers, clients, token acquisition, and aggregation.
- Add comments only where the reason, security boundary, or framework behavior is not obvious.
- Keep external DTOs internal. Return normalized lab models so provider payload changes do not leak
  into the public API accidentally.

## Integration Client Conventions

Each outbound integration should have these explicit boundaries:

1. an options type containing the base URL and provider-specific configuration;
2. a typed `HttpClient` interface and implementation;
3. a delegating handler when credentials belong on every request;
4. private or internal wire DTOs and public normalized models;
5. conversion of provider failures into `IntegrationException` categories;
6. bounded input, response disposal, JSON validation, cancellation, and telemetry coverage.

Register clients in `Program.cs` with `AddHttpClient` and `AddIntegrationResilience`. Do not create
`HttpClient` instances directly or implement an unbounded retry loop inside a provider client.

For pagination, follow provider continuation metadata, cap page count, and validate continuation
hosts before forwarding credentials. A full page alone is not proof that another page exists.

## Authentication and Secret Rules

- Never commit `.env`, tokens, client secrets, webhook secrets, cookies, or captured provider
  responses containing identity data.
- Put credentials in request headers through handlers or Microsoft.Identity.Web. Never return them
  in API responses, include them in exception messages, or attach them to telemetry.
- Keep Authorization Code and Client Credentials flows separate: the first represents a user; the
  second represents the application.
- Verify HMAC against the exact body bytes before deserialization, compare digests in constant time,
  and preserve both the freshness and one-time replay checks.
- Use synthetic test credentials. Never copy personal or production credentials into fixtures.

See [docs/authentication.md](docs/authentication.md) and [SECURITY.md](SECURITY.md) before changing
an authentication boundary.

## Error and Telemetry Conventions

- Map failures to the existing bounded categories: `authentication`, `authorization`,
  `configuration`, `throttled`, `timeout`, `upstream`, or `validation`.
- Return RFC 7807 Problem Details with a trace ID and safe metadata. Do not return upstream bodies,
  stack traces, exception text, or credentials.
- Reuse the bounded provider, operation, flow, outcome, and error-type dimensions in
  `ApiTelemetry`; request IDs, user IDs, URLs, raw exception text, and timestamps are not metric
  labels.
- Treat caller cancellation separately from upstream timeout or failure.
- Add or update telemetry safety tests when changing attributes, log fields, or metric tags.

## Branch and Pull Request Conventions

- Use a short-lived branch for normal changes. Direct work on `main` is exceptional and must be
  explicitly authorized by the repository owner.
- Keep one logical change per commit and use imperative commit subjects.
- Explain why the behavior changes, which integration boundaries are affected, and how the change
  was validated.
- Include screenshots only when Swagger or Grafana presentation materially changes; redact all
  identity and credential data.
- Do not push, merge, or bypass hooks on someone else's behalf without explicit authorization.

## Testing

At minimum, every behavior change needs:

- one happy-path test;
- one expected-failure test;
- a contract or integration test when routing, middleware, status codes, Problem Details, auth, or
  dependency injection changes.

Run the standard gate:

```bash
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
```

For provider work, also run the applicable checks in [tests/manual/README.md](tests/manual/README.md)
and record which credentialed checks were skipped.

## Formatting and Static Validation

```bash
dotnet format ApiIntegrationLab.sln --verify-no-changes --no-restore
git diff --check
```

Do not mix unrelated formatting changes into a feature or fix. Shell changes must retain
`set -euo pipefail`, quoted expansions, temporary-directory cleanup, and disabled command tracing.

## Review Process

- [ ] Public request/response and failure contracts are intentional.
- [ ] Caller-controlled work has explicit limits.
- [ ] Credentials cannot enter URLs, response bodies, logs, metrics, or traces.
- [ ] Retry behavior is safe for the HTTP method and bounded by the total timeout.
- [ ] Metric dimensions remain bounded; cardinality impact is stated for new labels.
- [ ] Unit and integration tests cover success and expected failure.
- [ ] Relevant docs, Swagger descriptions, examples, and ADRs are updated.
- [ ] `git diff --check`, build, tests, and applicable smoke checks pass.

## Documentation Updates

The code and documentation form one learning surface:

- update [docs/api/api-reference.md](docs/api/api-reference.md) for route or contract changes;
- update [ARCHITECTURE.md](ARCHITECTURE.md) for component, trust-boundary, or critical-flow changes;
- add an ADR under `docs/adrs/` for a durable decision with meaningful alternatives;
- update [docs/observability.md](docs/observability.md) for signal or label changes;
- update [docs/operations/runbook.md](docs/operations/runbook.md) for new failure or recovery paths.

Documentation examples must be runnable, use placeholders for secrets, and state expected results.
