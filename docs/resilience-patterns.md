---
title: Resilience Patterns
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Resilience Patterns

Resilience here means bounded, classifiable failure—not hiding every failure or promising that a
provider call always succeeds. All typed outbound clients use the shared standard resilience
handler; protocol-specific boundaries add their own controls.

## Pattern Catalog

| Pattern | Where applied | Exact configuration | Guarded failure | Trade-off / amplification | Verification |
| --- | --- | --- | --- | --- | --- |
| Attempt timeout | All typed outbound clients | 5 seconds | One slow attempt | May abandon work provider still performs | Registration tests/source |
| Total timeout | All typed outbound clients | 15 seconds | Retry/backoff exceeding caller budget | Must exceed useful attempt window but remain bounded | Registration tests/source |
| Transient retry | All typed outbound clients | 2 retries, 250 ms exponential base, jitter | Transport, `408`, `429`, `5xx` | Up to 3 provider attempts and extra latency | Registration tests/client tests |
| Circuit breaker | Standard handler | Library standard defaults | Repeated unhealthy dependency calls | Rejects locally while open; recovery probe may fail | Client exception mapping tests/source |
| Concurrency/rate limiter | Standard handler | Library standard defaults | Excess local outbound demand | Local rejection can occur before provider call | Client exception mapping tests/source |
| Caller cancellation | Controllers through clients | Propagated `CancellationToken` | Caller no longer wants work | Partial work may already have reached provider | Cancellation unit tests/source |
| Pagination bound | GitHub repositories | `perPage` 1–100; `maxPages` 1–10 | Infinite/expensive traversal | May intentionally return only a prefix | Pagination tests |
| Retry delay preservation | GitHub/Graph throttling | `Retry-After`, else GitHub reset when safe | Immediate caller repeat after `429` | Delay may become stale in transit | Rate-limit and parser tests |
| Typed error normalization | Every provider client | Seven bounded categories | Provider-specific status/exception leakage | Loses provider-specific detail by design | Exception/Problem Details tests |
| Partial-success fallback | `/api/demo` | Three concurrent tasks; `200` if any succeeds | One dependency hides healthy data | Caller must inspect every envelope | Aggregator/endpoint tests |
| Collector memory/batching | OTel Collector | `memory_limiter` + `batch` processors | Telemetry pressure/chattiness | Memory pressure can drop data; batching delays export | Collector config validation |
| HMAC resource/freshness limits | Webhook | 65,536 bytes, ±5 minutes, one accepted digest | Oversize or replayed inbound work | Legitimate delayed/redelivered events can be rejected | Webhook tests |

## Retry Semantics

The shared pipeline relies on the standard handler predicate for transient HTTP and transport
outcomes. Application code does not turn these deterministic failures into retries:

- `400` validation cannot be repaired by repeating the same request;
- `401` authentication needs a different/renewed credential;
- `403` authorization needs permission or consent;
- `503 configuration` needs local settings.

All current provider operations are `GET`, so automatic transient retry does not duplicate a
state-changing provider operation. If a future client adds `POST`, `PATCH`, or `DELETE`, its
idempotency and retry contract must be decided explicitly before using the shared handler.

Jitter prevents many callers from retrying at the same deterministic instant. It does not reduce
worst-case attempt amplification: one inbound request can still generate up to three outbound
attempts for a retryable failure.

## Timeout Layering

`HttpClient.Timeout` is disabled (`InfiniteTimeSpan`) so one resilience pipeline owns timing:

```text
total request budget: 15 s
  attempt 1: at most 5 s
  backoff: exponential from 250 ms + jitter
  attempt 2: at most 5 s
  backoff: exponential + jitter
  attempt 3: remaining total budget, never beyond 15 s
```

The total timeout is the final guard against attempts plus delays expanding without limit. An
attempt may receive less than five seconds when the total budget is nearly exhausted.

Polly timeout rejection becomes `IntegrationErrorCategory.Timeout` and HTTP `504`. A caller's own
cancelled token is rethrown and recorded as `cancelled`, because it does not prove provider timeout.

## Local Rejections

The standard handler can reject before network I/O:

- an open circuit becomes a safe `upstream` failure (`502`);
- local rate-limiter rejection becomes `throttled` (`429`) and preserves a known retry delay;
- attempt/total timeout becomes `timeout` (`504`).

Normalizing these outcomes prevents Polly exception types from leaking through controllers and
keeps `/api/demo` envelopes consistent whether a failure came from the network or local policy.

## Provider HTTP Mapping

| Provider response | Category | API status | Retry metadata |
| --- | --- | ---: | --- |
| `401` | `authentication` | `401` | None |
| `403` | `authorization` | `403` | None |
| GitHub `403` with remaining `0` | `throttled` | `429` | `Retry-After` or reset time |
| `429` | `throttled` | `429` | Parsed when present |
| Other final non-success | `upstream` | `502` | None |
| Exhausted resilience timeout | `timeout` | `504` | None |
| Malformed/missing required payload | `upstream` | `502` | None |

The API never returns an upstream response body. Provider bodies can contain implementation detail,
identity data, or unsafe content; the caller gets a bounded safe message and correlation trace ID.

## Pagination as a Resilience Boundary

GitHub continuation handling combines:

- caller bounds (`perPage`, `maxPages`);
- provider-authoritative `rel="next"`;
- same HTTPS host validation before forwarding a PAT;
- cancellation between pages;
- rate-limit metadata from the final fetched page.

This is both availability and security control: it bounds work and prevents credential-bearing
continuation to another origin.

## Partial Success and Degradation

`DemoAggregator` starts public, GitHub, and delegated Graph calls before awaiting any, so independent
latencies overlap. A known `IntegrationException` becomes `IntegrationResult<T>.Failure`; successful
results remain visible. The endpoint returns:

- `200` when one or more provider envelopes succeeded;
- `502 upstream` only when all three known integration calls failed.

Unexpected exceptions are deliberately not converted to provider envelopes. A null dereference,
DI error, or programming defect should fail visibly and be fixed, not masquerade as graceful
dependency degradation.

## Inbound Resilience

The webhook endpoint protects work before parsing:

1. declared and streamed body limits bound memory;
2. freshness bounds replay time;
3. exact-body HMAC rejects unauthenticated content;
4. atomic replay storage rejects the same accepted digest;
5. JSON parse and semantic validation happen only after authentication.

This in-memory design is single-replica. It provides no durable event queue, retry delivery,
idempotent business processing, or shared replay state.

## Telemetry Pipeline Resilience

The standalone Collector's memory limiter protects its process and the batch processor reduces
export overhead. Those processors improve the local learning pipeline; they do not guarantee
telemetry delivery. There is no checked-in persistent queue, durable buffer, sampling policy, or
backend SLA.

## Change Checklist

Before changing a resilience policy:

- identify retryable methods/statuses and worst-case attempt amplification;
- keep per-attempt work inside an explicit total budget;
- preserve caller cancellation separately;
- define local rejection → API category/status mapping;
- estimate added provider load and latency;
- add success, terminal failure, and cancellation tests;
- confirm telemetry labels remain bounded;
- update the [NFR](standards/non-functional-requirements.md),
  [API reference](api/api-reference.md), and [runbook](operations/runbook.md).
