---
adr: 001
title: "Use one service with provider feature folders"
status: accepted
date: 2026-09-17
deciders: Amit Singh
scope: lab
domain: architecture
---

# ADR 001: Use One Service with Provider Feature Folders

## Context

The lab must demonstrate several authentication and provider behaviors while remaining easy to run,
read, test, and present on one laptop. Each provider has different credentials, DTOs, bounds, and
failure semantics, but the repository has one owner and one deployment lifecycle.

## Decision

Use one ASP.NET Core service. Organize provider code into feature folders containing its controller,
options, provider DTOs, normalized models, typed-client interface/implementation, and auth handler
where required. Put only genuinely shared policies—errors, resilience, telemetry, OpenAPI, health,
validation, and time—under `Common/`.

Controllers depend on provider interfaces rather than transport details. This keeps the provider
boundary replaceable in tests and makes each authentication flow readable end to end.

## Alternatives Considered

### One microservice per provider

Rejected because independent deployment, networking, configuration, and telemetry would add
operational ceremony without an independent team, scaling requirement, or release lifecycle.

### Technical-layer folders only

Rejected because global `Controllers`, `Models`, and `Services` folders separate a provider's route
from the contract and credential logic it evolves with.

### One generic integration client

Rejected because a generic client would hide the exact Basic, bearer, OAuth, pagination,
rate-limit, and payload-validation behavior the lab is intended to teach.

## Consequences

Positive:

- one command starts and tests the complete lab;
- provider features remain cohesive and independently fakeable;
- shared policies are consistent without erasing provider differences;
- Swagger and telemetry show one coherent service boundary.

Negative:

- all integrations share one process and deployment;
- a composition-root change can affect every feature;
- feature-folder discipline is required to prevent `Common/` becoming a catch-all;
- this boundary would need reevaluation if providers gained independent ownership or scaling needs.

## Links

- [Architecture](../../ARCHITECTURE.md)
- [Approved design](../superpowers/specs/2026-09-16-api-integration-lab-design.md)
- [Contributing](../../CONTRIBUTING.md)
