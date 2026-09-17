---
title: "API Integration Lab Documentation"
status: active
scope: lab
owner: Amit Singh
last_reviewed: 2026-09-17
---

# API Integration Lab Documentation

Use this page as the map for the project. The root [README](../README.md) remains the fastest path
from clone to demo; the documents below explain how the system works, why it was designed this way,
and how to operate or extend it safely.

## Choose a Reading Path

| Audience | Read in this order |
| --- | --- |
| First-time learner | [README](../README.md) → [Learning guide](learning-guide.md) → [Authentication](authentication.md) → [API](api/api-reference.md) → [Architecture](../ARCHITECTURE.md) |
| Manager or demo reviewer | [README](../README.md) → [Demo playbook](operations/demo-playbook.md) → [Architecture](../ARCHITECTURE.md) → [Observability](observability.md) → [Security](../SECURITY.md) |
| Contributor | [Development](../DEVELOPMENT.md) → [Contributing](../CONTRIBUTING.md) → [Architecture](../ARCHITECTURE.md) → ADRs → [Test strategy](standards/test-strategy.md) |
| Local operator | [Runbook](operations/runbook.md) → [Observability](observability.md) → [Resilience](resilience-patterns.md) → [Security](../SECURITY.md) |
| Security reviewer | [Security](../SECURITY.md) → [Authentication](authentication.md) → [API](api/api-reference.md) → [HMAC ADR](adrs/adr-003-exact-body-hmac-and-replay-cache.md) → [OAuth ADR](adrs/adr-002-dual-microsoft-oauth-flows.md) |

## Document Catalog

| Area | Document | Use it for |
| --- | --- | --- |
| Discovery | [Project README](../README.md) | Purpose, quick start, demo sequence, and top-level validation |
| Architecture | [Architecture](../ARCHITECTURE.md) | System context, components, sequences, deployment, and failure modes |
| Development | [Local development](../DEVELOPMENT.md) | Running, debugging, and navigating the code |
| Contribution | [Contributing](../CONTRIBUTING.md) | Coding, testing, review, and documentation conventions |
| Learning | [Learning guide](learning-guide.md) | Guided source tour, exercises, and explanation prompts |
| Interface | [API reference](api/api-reference.md) | Routes, bounds, authentication, responses, and errors |
| Identity | [Authentication guide](authentication.md) | Six authentication mechanisms and their lifecycles |
| Reliability | [Resilience patterns](resilience-patterns.md) | Timeouts, retry, circuit breaking, rate limiting, and partial success |
| Observability | [Observability guide](observability.md) | Metrics, logs, traces, queries, and cardinality |
| Operations | [Local runbook](operations/runbook.md) | Diagnosis, recovery, maintenance, and local rollback |
| Demonstration | [Demo playbook](operations/demo-playbook.md) | Repeatable manager-facing walkthrough |
| Security | [Security and threat model](../SECURITY.md) | Trust boundaries, STRIDE analysis, controls, and limitations |
| Quality | [Test strategy](standards/test-strategy.md) | Test layers, secret-safe data, gates, and gaps |
| Requirements | [Non-functional requirements](standards/non-functional-requirements.md) | Implemented safety bounds and undefined production targets |
| Dependencies | [Dependency inventory](../DEPENDENCIES.md) | SDK, packages, images, tools, and provider dependencies |
| Troubleshooting | [FAQ](../FAQ.md) | Short answers with links to canonical guidance |
| Legal | [MIT License](../LICENSE) | Terms for using, copying, and modifying the repository |

Durable decisions:

- [ADR 001: Single service with provider feature folders](adrs/adr-001-single-service-feature-folders.md)
- [ADR 002: Dual Microsoft OAuth flows](adrs/adr-002-dual-microsoft-oauth-flows.md)
- [ADR 003: Exact-body HMAC and replay cache](adrs/adr-003-exact-body-hmac-and-replay-cache.md)
- [ADR 004: OTel Collector and local LGTM](adrs/adr-004-otel-collector-and-local-lgtm.md)

Historical engineering records:

- [Approved implementation design](superpowers/specs/2026-09-16-api-integration-lab-design.md)
- [Completed implementation plan](superpowers/plans/2026-09-16-api-integration-lab.md)
- [Documentation design](superpowers/specs/2026-09-17-project-documentation-design.md)
- [Documentation implementation plan](superpowers/plans/2026-09-17-project-documentation.md)

## Sources of Truth

| Information | Authoritative source |
| --- | --- |
| HTTP routes and schemas | Controller attributes, DTOs, XML remarks, and runtime Swagger |
| Authentication wiring | `Program.cs`, authentication handlers, and Graph token provider |
| Retry and timeout configuration | `Common/Http/HttpClientRegistrationExtensions.cs` |
| Error statuses and Problem Details | `Common/Errors/` |
| Metrics and allowed dimensions | `Common/Telemetry/ApiTelemetry.cs` |
| Telemetry transport | OpenTelemetry registration and `otel-collector.yaml` |
| Local topology | Dockerfile and Compose files |
| Verified behavior | Unit tests, integration tests, and `scripts/smoke-test.sh` |
| Design intent | ADRs and Superpowers engineering records |

Executable source wins when prose and code disagree. Correct the documentation in the same change
that alters a contract, limit, metric, authentication flow, or operating procedure.

## Documentation Boundaries

This repository documents a single-instance localhost learning lab. It intentionally has no cloud,
Kubernetes, Terraform, CI/CD, database, production SLO, disaster recovery, on-call, compliance, or
support-SLA documentation because those systems and commitments do not exist here.

## Maintenance Rules

- Owner: Amit Singh. There is no on-call rotation or support SLA.
- Living documents are updated in place and keep `last_reviewed` current.
- ADRs are historical records; supersede an accepted decision instead of rewriting it silently.
- Commands must run from the repository root and must not contain real credentials.
- Every Mermaid diagram uses the explicit repository theme and pastel classes.
- Documentation changes receive the same review and validation as code changes.
