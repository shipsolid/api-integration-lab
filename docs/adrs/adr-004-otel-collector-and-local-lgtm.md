---
adr: 004
title: "Export OpenTelemetry through a standalone Collector to local LGTM"
status: accepted
date: 2026-09-17
deciders: Amit Singh
scope: lab
domain: observability
---

# ADR 004: Export OpenTelemetry through a Standalone Collector to Local LGTM

## Context

The lab must demonstrate that authentication and provider failures are observable without coupling
application code to a proprietary backend or requiring a cloud account. It also needs one laptop
command that exposes metrics, logs, and traces for a manager demo.

## Decision

Instrument the API with OpenTelemetry .NET for ASP.NET Core, `HttpClient`, runtime signals, logs,
and custom bounded integration instruments. Export OTLP/gRPC to a standalone pinned Collector. The
Collector applies a 256 MiB memory limit with 64 MiB spike allowance, adds
`deployment.environment.name=local`, batches for two seconds, and forwards to pinned Grafana LGTM
on the private Compose network.

## Alternatives Considered

### Add a vendor-specific application SDK

Rejected because it would couple the application model to one backend and distract from OTel-native
instrumentation and portable semantic boundaries.

### Export directly from the API to LGTM

Rejected because Collector-side memory control, resource enrichment, batching, validation, and
future routing are part of the integration architecture being demonstrated.

### Use unpinned `latest` container images

Rejected because mutable tags make a learning demo and its validation commands non-reproducible.

## Consequences

Positive:

- application instrumentation is backend-neutral;
- all three signals are locally explorable;
- Collector configuration is independently validated;
- bounded custom labels make cardinality explainable and testable.

Negative:

- the local stack consumes more memory than API-only development;
- repository-defined retention, dashboards, alerts, and authentication do not exist in LGTM;
- Compose network plaintext OTLP is safe only within this local topology;
- production Grafana Cloud export would require TLS, remote endpoints, scoped write credentials,
  cost/retention decisions, and operational ownership.

## Links

- [Observability guide](../observability.md)
- [Architecture](../../ARCHITECTURE.md)
- [Collector configuration](../../otel-collector.yaml)
