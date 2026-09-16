---
title: Dependencies
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Dependencies

This inventory explains why each direct dependency exists and what must be checked before updating
it. Project files and pinned container tags are the source of truth. Restore writes the exact
transitive graph to generated `obj/project.assets.json` files; this repository does not currently
commit NuGet lock files.

## Runtime and Build Platform

| Dependency | Declared version | Purpose | Upgrade checks |
| --- | --- | --- | --- |
| .NET SDK | `8.0.131` | Restore, compile, test, and publish | Build and test all projects; confirm `global.json` roll-forward behavior |
| ASP.NET Core runtime | Container tag `8.0` | Run the published Web API | Health, auth routing, Problem Details, Swagger, and non-root execution |
| .NET SDK image | Container tag `8.0` | Multi-stage restore and publish | BuildKit CA secret, package restore, publish output, image rebuild |

The moving `8.0` container tags receive compatible servicing updates. Rebuild regularly, inspect the
resolved image digests during review, and rerun the full container smoke test.

## Application NuGet Packages

| Package | Version | Why it exists | Upgrade-sensitive behavior |
| --- | ---: | --- | --- |
| `Microsoft.Extensions.Http.Resilience` | `10.10.0` | Standard `HttpClient` retry, timeout, circuit-breaker, and rate-limiter pipeline | Attempt count, timeout order, cancellation, `Retry-After`, package target compatibility |
| `Microsoft.Identity.Web` | `4.14.2` | OIDC web sign-in, token acquisition, in-memory token cache, client credentials | Default schemes, callback paths, cookie challenge behavior, token-cache registration |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | `1.18.0` | OTLP export of traces, metrics, and logs | gRPC endpoint/protocol, resource attributes, exporter defaults |
| `OpenTelemetry.Extensions.Hosting` | `1.18.0` | Host lifecycle and OpenTelemetry registration | Startup/shutdown flushing and service registration |
| `OpenTelemetry.Instrumentation.AspNetCore` | `1.18.0` | Inbound HTTP traces and metrics | Route attributes, filtering, exception recording, sensitive tags |
| `OpenTelemetry.Instrumentation.Http` | `1.18.0` | Outbound `HttpClient` spans and metrics | URL/header sanitization, status semantics, retry span shape |
| `OpenTelemetry.Instrumentation.Runtime` | `1.18.0` | Process/runtime metrics | Instrument names and volume |
| `Swashbuckle.AspNetCore` | `10.2.3` | OpenAPI generation and Swagger UI | XML comments, schema generation, operation filters, auth descriptions |

Keep the OpenTelemetry packages on the same compatible version unless an upstream compatibility
matrix explicitly supports a mixed set. A package update that introduces a new metric label or
trace attribute requires a cardinality and sensitive-data review, not only passing compilation.

## Test NuGet Packages

| Package | Version | Projects | Purpose |
| --- | ---: | --- | --- |
| `Microsoft.NET.Test.Sdk` | `17.8.0` | Unit, integration | Test discovery and execution |
| `xunit` | `2.5.3` | Unit, integration | Test framework |
| `xunit.runner.visualstudio` | `2.5.3` | Unit, integration | IDE and `dotnet test` runner integration |
| `coverlet.collector` | `6.0.0` | Unit, integration | Optional coverage collection |
| `Microsoft.AspNetCore.Mvc.Testing` | `8.0.31` | Integration | In-process test host through `WebApplicationFactory` |

Runner/framework upgrades must still discover both test projects and preserve parallel-test
assumptions. An ASP.NET testing package update must remain compatible with the application's .NET 8
target and middleware behavior.

## Container Dependencies

| Image | Tag | Responsibility | Upgrade checks |
| --- | ---: | --- | --- |
| `otel/opentelemetry-collector-contrib` | `0.160.0` | Receive OTLP, apply memory/resource/batch processors, export three signals | Collector config validation, startup logs, end-to-end trace/metric/log delivery |
| `grafana/otel-lgtm` | `0.33.0` | Local Grafana plus telemetry backends | Grafana health, Explore data sources, signal ingestion, local storage behavior |

Do not replace pinned Collector or LGTM tags with `latest`. Read upstream release notes for renamed
components, changed defaults, and storage migrations before updating.

## External Services

| Service | Use in this lab | Contract owned here | Contract owned externally |
| --- | --- | --- | --- |
| JSONPlaceholder | Public posts | Bounds, normalization, error mapping | Availability and source payload |
| Postman Echo | Basic Auth demonstration | Credential placement, configuration validation, normalized result | Authentication echo behavior |
| GitHub REST API | Profile, repositories, rate limits | PAT injection, pagination cap, host validation, normalized metadata | Token issuance, rate limits, API schema |
| Microsoft Entra ID | OIDC and OAuth token issuance | App configuration, scheme routing, safe failure handling | Authentication, consent, token service |
| Microsoft Graph | Delegated profile and app-only users | Scopes, bounded query, normalization, error mapping | Directory data, permission enforcement, API schema |

External APIs are not build dependencies, and automated tests must not depend on their live
availability. Their end-to-end checks remain explicit manual or smoke-test steps.

## Local Tooling

| Tool | Purpose | Safety note |
| --- | --- | --- |
| Docker Compose v2 | Orchestrate API, Collector, and LGTM | Use `.env`; do not place secrets in Compose files |
| Bash | Execute the smoke test | Keep `set -euo pipefail`; never enable `set -x` around secrets |
| `curl` | Probe HTTP endpoints | Avoid verbose output when Authorization headers are present |
| OpenSSL | Produce local HMAC-SHA256 signatures | Pass a local test secret; shell history may retain literal arguments |
| ShellCheck | Statically check the smoke script | Treat warnings as review items rather than suppressing broadly |

## Safe Upgrade Procedure

1. Read the direct dependency's release notes and compatibility guidance.
2. Change one dependency family at a time; OpenTelemetry packages count as one family.
3. Restore and inspect NuGet's proposed dependency graph:

   ```bash
   dotnet restore ApiIntegrationLab.sln --force-evaluate
   ```

4. Review project-file diffs and restore output for unexpected transitive changes. For a
   dependency-sensitive change, inspect the generated `obj/project.assets.json` graph locally.
5. Run the standard validation gate (the repository does not commit NuGet lock files):

   ```bash
   dotnet restore ApiIntegrationLab.sln
   dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
   dotnet test ApiIntegrationLab.sln --configuration Release --no-build
   docker compose --env-file .env build
   docker compose --env-file .env up --detach
   scripts/smoke-test.sh
   ```

6. For identity changes, manually exercise unconfigured startup, login redirect, delegated `/me`,
   app-only `/users`, logout, and protected-route `401` behavior.
7. For telemetry changes, verify all three signals in Grafana and recheck label cardinality and
   sensitive-data filtering.
8. For container changes, inspect the image, confirm the API remains non-root, and stop the stack
   with `docker compose down`.

Never accept an upgrade solely because compilation passes: this lab deliberately tests protocol,
middleware, observability, and failure-contract behavior that type checking cannot prove.
