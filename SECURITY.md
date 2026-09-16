---
title: Security
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Security

## Scope and Security Posture

This is a single-user localhost learning lab, not a production security boundary. It demonstrates
safe integration mechanics: server-owned credentials, explicit identity flows, HTTPS provider
checks, bounded input/work, exact-byte HMAC, replay defense, safe errors, and telemetry hygiene.

The API and Grafana are exposed on host HTTP ports for local use. Do not expose this Compose stack to
an untrusted network. A non-local deployment requires a separate threat model and the hardening gaps
listed below.

## Trust Boundaries and Assets

| Boundary | Assets crossing it | Required property |
| --- | --- | --- |
| Browser/caller → API | Query/body, cookie, HMAC headers | Bounded input; explicit route auth; no open redirect |
| API → providers | Basic pair, PAT, OAuth access token | HTTPS before credential attachment; minimum permissions |
| Browser ↔ Entra ↔ API callback | OIDC state/nonce/code and local cookie | Middleware validation; server-side redemption; local redirect only |
| Webhook sender → API | Timestamp, exact body, MAC | HTTPS outside localhost; constant-time MAC; freshness + replay defense |
| API → Collector → LGTM | Logs, metrics, spans | No secrets/payloads; bounded dimensions; local-network scope |
| Host → containers | `.env`, CA trust material, port bindings | Local access control; ephemeral secret mount; no committed secrets |

Protected assets include Basic credentials, GitHub PAT, Entra client secret, OAuth tokens, session
cookie/data-protection keys, webhook secret, provider identity data, telemetry, and source/config.

## Threat Model (STRIDE)

| Actor / surface | STRIDE threats | Current mitigations and evidence | Residual local-lab risk |
| --- | --- | --- | --- |
| Malicious API caller | Spoofing, tampering, DoS | Route authorization, model/client bounds, total timeout; endpoint tests | No ingress TLS/rate limit for inbound callers |
| Webhook sender/attacker | Spoofing, tampering, replay, DoS | 64 KiB bound, exact HMAC, strict format, ±5 min, fixed-time compare, atomic cache; webhook tests | Shared secret and replay state are process-local |
| Misconfigured/compromised provider credential | Spoofing, elevation, disclosure | Server-side config, HTTPS check, no response/log export, minimum-permission guidance; handler/safety tests | No managed secret store or automatic rotation |
| Malicious pagination link | Disclosure, SSRF-like credential forwarding | GitHub next-link parsing requires expected HTTPS host; pagination tests | Provider is still trusted to return safe data volume within caps |
| External provider | Tampering, disclosure, availability failure | DTO validation, normalization, safe errors, bounded resilience, no upstream body relay | Provider availability/integrity outside repository control |
| Local Docker network participant | Spoofing, information disclosure | Collector/LGTM ports mostly internal; scoped local network | OTLP to LGTM is plaintext/unauthenticated inside network |
| Telemetry consumer | Information disclosure, repudiation | Bounded fields, webhook/log safety tests, trace correlation | Grafana has development defaults and no repository-defined access policy |
| Compromised workstation | All categories | `.env` ignored, non-root API container, no checked-in secrets | Host user can read `.env`, browser cookies, container/network traffic |

This model is evidence for the current topology, not a penetration-test result.

## Attack Surface

- Host ports `8080` (API/Swagger) and `3000` (Grafana).
- Anonymous routes: health, public posts, Basic/GitHub provider calls, Microsoft login, app-only
  Graph users, and webhook receiver.
- Cookie-protected routes: Microsoft logout, `/me`, and `/api/demo`.
- OIDC callbacks handled by Microsoft.Identity.Web middleware.
- Outbound HTTPS to five provider systems.
- Local OTLP receiver endpoints on the Compose network.
- Docker build supply chain and NuGet/container dependencies.

Swagger is intentionally enabled for every lab environment. An internet-facing service must disable
or authenticate it outside development.

## Authentication

Six patterns and their credential lifecycles are documented in
[docs/authentication.md](docs/authentication.md). Key rules:

- external credentials remain server-owned;
- Authorization Code represents the signed-in user;
- Client Credentials represents the API workload;
- HMAC authenticates exact bytes and secret possession but does not encrypt content;
- missing optional configuration affects only the relevant route, not startup/liveness.

## Authorization

Authorization is route-based plus external-provider permission; there is no application RBAC or
ABAC model.

- `/api/microsoft/me`, `/api/microsoft/logout`, and `/api/demo` require the local authenticated
  cookie session.
- `/api/microsoft/users` is inbound anonymous but uses the application's Graph permission
  `User.Read.All` with tenant admin consent.
- GitHub and Basic permissions are those of their configured server-side credentials.
- Webhook acceptance proves shared-secret possession/freshness, not an application role.

Authentication and authorization failures remain distinct (`401` versus `403`).

## Secrets Management and Rotation

Current lab controls:

- `.env` and `.env.*` are ignored except the empty `.env.example` template;
- Compose maps environment values into configuration without embedding them in images;
- Docker build CA material uses a BuildKit secret, not a copied file;
- tests use synthetic markers and assert secrets/tokens/signatures are absent from telemetry;
- API responses contain normalized data and safe errors, never provider tokens.

Rotation is manual: obtain a new minimum-scope credential, update `.env`, recreate the API
container, verify the route, then revoke the old credential. Microsoft client-secret rotation should
overlap old/new validity only as required by the app-registration procedure. HMAC rotation currently
has no dual-key window; coordinate sender/API change or accept a short local interruption.

A real service requires a managed secret store, audited access, automatic rotation/expiry alerts,
and a documented emergency-revocation process.

## Certificate and Trust Management

- Basic, GitHub, and Graph clients reject non-HTTPS base URLs before attaching credentials.
- Public provider URLs are HTTPS by default.
- Corporate interception trust is injected through an ephemeral BuildKit secret/explicit local
  override; TLS verification must not be disabled.
- Local caller → API/Grafana and Collector → LGTM traffic is plaintext inside the host/local Compose
  boundary.

Non-local use requires trusted TLS termination for inbound HTTP and authenticated/encrypted OTLP
across any untrusted network.

## Network Exposure

Compose publishes only API `8080` and Grafana `3000`. Collector OTLP/health and LGTM OTLP remain on
the local Compose network. Docker's host bindings are not an authorization layer: use only on a
trusted workstation, do not bind through port-forwarding/tunnels to untrusted clients, and do not
reuse this model as an internet deployment manifest.

## Input and Payload Controls

- public `limit`: 1–100;
- GitHub `perPage`: 1–100 and `maxPages`: 1–10;
- Graph `top`: 1–50;
- webhook: 65,536 bytes maximum, strict timestamp/signature grammar, ±5 minutes;
- Microsoft `returnUrl`: local URLs only;
- provider DTOs: required fields and HTTPS returned links are validated;
- pagination continuations: same expected HTTPS host;
- known provider failures: normalized RFC 7807 without upstream bodies.

These controls reduce exposure; they are not a substitute for ingress rate limiting, WAF policy, or
capacity testing in a production design.

## Logging and Telemetry Safety

Allowed custom context is limited to bounded provider/operation/outcome/error/flow values and trace
correlation. Forbidden content includes tokens, passwords, cookies, secrets, Authorization headers,
query strings, bodies, signatures, identity fields, raw URLs, and raw exception/provider response
text. See [observability.md](docs/observability.md) for the schema and tests.

## Dependency and Vulnerability Management

Direct NuGet versions and container tags are explicit and inventoried in
[DEPENDENCIES.md](DEPENDENCIES.md). Safe updates require release-note review, restore/build/tests,
identity contract checks, container smoke tests, and telemetry validation.

This repository contains no CI workflow, scheduled vulnerability scan, SBOM, signing/attestation,
or automated dependency updater. No vulnerability-free status is claimed. Before distribution or
deployment, add an approved dependency/container scanning and remediation process.

## OWASP API Security Review

| Risk area | Current treatment | Gap |
| --- | --- | --- |
| Object/function authorization | Small fixed routes; cookie protects delegated endpoints | No resource-level authorization model because no owned resource store exists |
| Authentication | Framework OIDC/cookie, server PAT/Basic, exact HMAC | Local HTTP and manual secret lifecycle |
| Unrestricted resource consumption | Query/page/body/time/retry bounds | No inbound global/user rate limit or load-tested capacity |
| SSRF/unsafe external consumption | Fixed base URLs; same-host continuation; DTO validation | Configuration access still controls provider base URLs |
| Security misconfiguration | Safe empty defaults, non-root image, explicit errors | Swagger/Grafana exposed and dev defaults used locally |
| Inventory/versioning | One `v1` OpenAPI document and dependency inventory | No API lifecycle/deprecation policy |
| Unsafe API consumption | Bounded resilience, payload validation, normalization | External contract drift remains a manual/live concern |

This is a focused review, not formal OWASP certification.

## Penetration Testing Status

No penetration test, DAST campaign, fuzzing campaign, or independent security assessment has been
performed. Automated tests cover selected authentication, replay, input, error, and telemetry-safety
properties only.

## Security Baseline

Before accepting a documentation or code change:

- no usable secret exists in source, examples, fixtures, logs, or responses;
- credentials are attached only after HTTPS/origin validation;
- caller-controlled work has explicit bounds;
- authentication and authorization remain distinct;
- retries exclude deterministic auth/validation failures;
- errors disclose only safe bounded fields plus trace ID;
- metric labels pass the cardinality allowlist;
- unit/integration/security tests and shell/static checks pass.

## Known Limitations

- Localhost HTTP for API/Grafana; no ingress TLS.
- In-memory Microsoft token cache and webhook replay cache.
- Ephemeral local data-protection keys/session behavior across restart/recreation.
- No shared replay defense, durable idempotency store, business datastore, or event queue.
- No secret manager, automated rotation, RBAC/ABAC, inbound rate limiter, WAF, or network policy.
- Plaintext unauthenticated OTLP within the Compose network.
- Development LGTM defaults without defined access, retention, backup, or audit policy.
- No production deployment, CI security gate, SBOM, signature, or vulnerability-response SLA.

Multi-replica or non-local deployment requires TLS, durable/shared data-protection keys, distributed
token cache, shared atomic replay defense, a secret manager, authenticated telemetry, explicit
authorization, and measured capacity/operating targets.

## Reporting Vulnerabilities

Report suspected vulnerabilities to repository owner Amit Singh through a private channel. Do not
open a public issue containing credentials, identity data, exploit instructions, provider payloads,
or sensitive telemetry. Include a redacted reproduction, affected route/component, impact, and safe
trace/error context. No response SLA is defined for this personal lab.
