---
title: Frequently Asked Questions
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Frequently Asked Questions

## Why does the application start with empty credentials?

Each provider is an optional learning path. Health, Swagger, public requests, validation, tests, and
safe failure contracts remain available on a fresh clone; a credential is validated when its route
needs it. See [development setup](DEVELOPMENT.md#provider-configuration).

## Why does GitHub return 401 without a PAT?

The GitHub handler intentionally omits `Authorization` when no PAT is configured, allowing the real
provider authentication boundary to be observed. Basic intentionally demonstrates a different
local `503 configuration` boundary. See [authentication](docs/authentication.md#github-bearerpat).

## Why is `/api/microsoft/me` protected but `/api/microsoft/users` app-only?

`/me` represents the signed-in person and requires the local cookie plus delegated `User.Read`.
`/users` represents the workload and acquires a Client Credentials token using application
`User.Read.All`; inbound cookie state is irrelevant. See
[the OAuth comparison](docs/authentication.md#choosing-the-correct-pattern).

## Why does login use a browser rather than curl?

Authorization Code includes interactive authentication/consent, redirects, state/nonce validation,
and a browser cookie. A single curl call cannot meaningfully complete that user journey. See the
[Authorization Code flow](docs/authentication.md#oauth-20-authorization-code).

## Why can a valid HMAC request still be rejected as a replay?

A correct MAC proves the signed bytes and secret possession, but the exact request can be copied.
The lab also requires a fresh timestamp and accepts each digest once within that window. See
[ADR 003](docs/adrs/adr-003-exact-body-hmac-and-replay-cache.md).

## Why is the raw webhook body verified before JSON parsing?

Parsing and reserializing can change whitespace, escaping, or property order. The sender signed wire
bytes, so the receiver must verify those exact bytes before interpreting JSON. See the
[HMAC API contract](docs/api/api-reference.md#hmac-webhook).

## Why are retries limited and authentication failures not retried?

Retries add latency and provider load. The lab permits two retries for transient outcomes inside a
15-second total budget; the same request cannot repair invalid credentials, permissions, or input.
See [resilience patterns](docs/resilience-patterns.md#retry-semantics).

## Why can `/api/demo` return partial data?

The three providers are independent. Each known integration failure becomes its own envelope so one
outage does not erase healthy results; HTTP `502` means all three known calls failed. See the
[aggregate API contract](docs/api/api-reference.md#aggregate-demo).

## Where are tokens stored, and why are they absent from responses?

Basic/PAT/client secret are process configuration; Microsoft access tokens use the in-memory
Microsoft.Identity.Web cache; the browser receives only an encrypted HTTP-only session cookie.
Returning tokens would enlarge the exposure surface without helping the caller. See
[credential lifecycles](docs/authentication.md#credential-and-token-lifecycles).

## How do I find a failed request in Grafana?

Copy the safe Problem Details `traceId`, search Loki for it, open the corresponding Tempo trace, then
use provider/operation metrics to assess scope. See the
[correlation workflow](docs/observability.md#correlation-workflow).

## Why are user IDs and trace IDs not metric labels?

They are high-cardinality and identity-bearing. They create a series per changing value; trace IDs
belong in logs/traces for point lookup. Metrics use finite allowlists with a computed custom upper
bound. See the [cardinality budget](docs/observability.md#cardinality-budget).

## What changes before multi-replica or non-local deployment?

At minimum: inbound TLS, secret manager/rotation, shared data-protection keys, distributed token
cache, atomic shared replay store, authenticated telemetry, ingress rate limiting, authorization
design, durable storage decisions, and measured SLO/capacity targets. See
[known security limitations](SECURITY.md#known-limitations) and
[architecture scaling](ARCHITECTURE.md#scaling-model).

## What works without internet or provider credentials?

Build, unit tests, integration tests, source/Swagger generation tests, validation behavior, and
unconfigured failure paths need no provider credentials. Live public/provider smoke calls require
internet, and real OAuth needs Entra configuration. See the [test strategy](docs/standards/test-strategy.md).

## Where is the generated OpenAPI contract?

With the API running, use <http://localhost:8080/swagger/v1/swagger.json>; Swagger UI is
<http://localhost:8080/swagger>. Runtime OpenAPI is the generated schema authority, while the
[checked-in API reference](docs/api/api-reference.md) explains use and failure semantics.

## Why is Grafana empty after restarting or removing the stack?

Generate new API traffic and diagnose API → Collector → LGTM. Compose declares no persistent volume,
so removing/recreating LGTM can discard local telemetry. See the
[pipeline runbook](docs/operations/runbook.md#telemetry-pipeline-diagnosis).

## Where should I start reading?

Choose a role-based path in the [documentation map](docs/README.md), or follow the six-module
[learning guide](docs/learning-guide.md) if your goal is to understand the implementation end to end.
