# API Integration Lab Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete, learning-first documentation system for the API Integration Lab without
inventing production capabilities that the repository does not implement.

**Architecture:** Keep `README.md` as the short project and demo entry point, add `docs/README.md`
as the documentation hub, and split deeper material into focused architecture, interface, learning,
operations, assurance, and decision documents. Every factual claim must trace to executable source,
tests, or checked-in runtime configuration; cross-links replace duplicated prose.

**Tech Stack:** Markdown, GitHub-flavored Markdown, themed Mermaid, .NET 8 source and XML remarks,
runtime Swagger/OpenAPI, Docker Compose, OpenTelemetry Collector, Grafana LGTM, shell validation.

**Spec:** `docs/superpowers/specs/2026-09-17-project-documentation-design.md`

## Global Constraints

- Modify documentation files only; do not change packages, code, tests, or runtime behavior.
- Preserve the current user edits in
  `docs/superpowers/specs/2026-09-16-api-integration-lab-design.md` and
  `docs/superpowers/plans/2026-09-16-api-integration-lab.md`; make only targeted stale-reference
  corrections in those files.
- Preserve the deletion of `req.md`.
- Do not commit or push. End every task with a diff-review checkpoint.
- Use owner `Amit Singh`, scope `lab`, and review date `2026-09-17` in new frontmatter.
- Use exact route names, query limits, timeout values, retry counts, metric names, configuration
  keys, and image versions from source.
- Do not include real credentials, tenant identifiers, identity values, provider bodies, HMAC
  material, or access tokens.
- Do not claim production availability, latency, throughput, recovery, support, compliance, or
  retention guarantees.
- Keep runtime Swagger authoritative for machine-generated schemas; the checked-in API reference is
  a human learning guide.
- Every Mermaid diagram must include the repository's explicit base-theme initialization and pastel
  `classDef` styles.
- Every command must run from the repository root and be copy-pasteable.
- Cross-link canonical explanations instead of repeating large sections.
- New documents must contain no unresolved placeholders or speculative commitments.

## File Map

| File | Responsibility |
| --- | --- |
| `README.md` | Short project entry point, fast start, demo summary, and documentation links |
| `docs/README.md` | Audience-based documentation map and source-of-truth guidance |
| `ARCHITECTURE.md` | C4-style views, integrations, critical sequences, deployment, scaling, and failures |
| `CONTRIBUTING.md` | Repository structure, coding conventions, review, testing, and documentation rules |
| `DEVELOPMENT.md` | Local setup, execution modes, debugging, and code-reading entry points |
| `SECURITY.md` | Trust boundaries, STRIDE threats, secrets, certificates, and known limitations |
| `DEPENDENCIES.md` | SDK, NuGet, image, provider, and tool dependency inventory |
| `FAQ.md` | Recurring learning, credential, Docker, OAuth, Swagger, and telemetry questions |
| `docs/authentication.md` | Comparison and lifecycle of all six authentication mechanisms |
| `docs/learning-guide.md` | Guided code tour, exercises, observations, and completion criteria |
| `docs/observability.md` | Signal strategy, metric schema, cardinality, queries, and pipeline diagnosis |
| `docs/resilience-patterns.md` | Retry, timeout, circuit-breaker, limiter, mapping, and degradation behavior |
| `docs/api/api-reference.md` | Human-readable routes, bounds, authentication, responses, and errors |
| `docs/adrs/adr-001-single-service-feature-folders.md` | Service and code-organization decision |
| `docs/adrs/adr-002-dual-microsoft-oauth-flows.md` | Delegated and workload OAuth decision |
| `docs/adrs/adr-003-exact-body-hmac-and-replay-cache.md` | Inbound webhook authentication decision |
| `docs/adrs/adr-004-otel-collector-and-local-lgtm.md` | Telemetry topology decision |
| `docs/operations/demo-playbook.md` | Repeatable manager-demo preparation, narrative, evidence, and cleanup |
| `docs/operations/runbook.md` | Local health, diagnosis, recovery, and maintenance procedures |
| `docs/standards/non-functional-requirements.md` | Implemented limits, security baseline, and explicit non-targets |
| `docs/standards/test-strategy.md` | Test layers, test data, coverage boundaries, and quality gates |
| `docs/superpowers/specs/2026-09-16-api-integration-lab-design.md` | Historical design with corrected source reference |
| `docs/superpowers/plans/2026-09-16-api-integration-lab.md` | Historical implementation plan with corrected source references |

---

### Task 1: Establish the documentation hub and refresh project entry points

**Files:**

- Create: `docs/README.md`
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-09-16-api-integration-lab-design.md`
- Modify: `docs/superpowers/plans/2026-09-16-api-integration-lab.md`

**Interfaces:**

- Consumes: the approved documentation design and current repository structure.
- Produces: the canonical document navigation used by every later task.
- Produces: corrected historical references after deletion of `req.md`.

- [ ] **Step 1: Capture the protected working-tree baseline**

Run:

```bash
git status --short --untracked-files=all
git diff -- docs/superpowers/specs/2026-09-16-api-integration-lab-design.md \
  docs/superpowers/plans/2026-09-16-api-integration-lab.md
```

Expected: the two historical Superpowers files already contain user formatting edits, `req.md` is
deleted, and the new documentation design and plan are untracked. Save this output for comparison;
do not revert any existing line.

- [ ] **Step 2: Create the documentation index**

Create `docs/README.md` with frontmatter followed by these exact sections:

```markdown
# API Integration Lab Documentation

## Choose a Reading Path
## Document Catalog
## Sources of Truth
## Documentation Boundaries
## Maintenance Rules
```

The reading-path table must include:

| Audience | Ordered path |
| --- | --- |
| First-time learner | README → learning guide → authentication → API → architecture |
| Manager/demo reviewer | README → demo playbook → architecture → observability → security |
| Contributor | development → contributing → architecture → ADRs → test strategy |
| Local operator | runbook → observability → resilience → security |
| Security reviewer | security → authentication → API → HMAC ADR → OAuth ADR |

List every planned document from the File Map even before it exists so later tasks have a stable
navigation contract. Label the Superpowers documents as historical design/execution records rather
than primary learner documentation.

- [ ] **Step 3: Add a concise documentation entry point to README**

Add an `## Documentation` section after the architecture overview. Link to `docs/README.md` and
surface four direct paths: learning guide, architecture, API reference, and runbook. Add an
`## Ownership boundary` section stating:

- repository owner: Amit Singh;
- operating model: personal local learning lab;
- on-call and support SLA: none;
- external dependencies are owned by their providers;
- the repository owns normalization, error mapping, security boundaries, and local telemetry.

Do not remove the existing quick start, demo sequence, security, observability, test, or
troubleshooting content in this task.

- [ ] **Step 4: Correct stale references to the deleted brief**

In the 2026-09-16 design, replace the source line with:

```markdown
**Source:** Original project brief, retained in Git history at `089832d:req.md`.
```

In the 2026-09-16 plan:

- change the goal's `req.md` reference to `the approved API Integration Lab design`;
- change scaffold checkpoints that mention the untracked brief to the approved design/spec files;
- change the final preservation instruction to state that the original brief is retained in Git
  history and intentionally absent from the working tree.

Make no formatting or wording changes outside the lines containing `req.md`.

- [ ] **Step 5: Verify navigation and protected edits**

Run:

```bash
if rg -n 'req\.md' docs/superpowers/plans/2026-09-16-api-integration-lab.md; then exit 1; fi
rg -n '^\*\*Source:\*\* Original project brief, retained in Git history at `089832d:req\.md`\.$' \
  docs/superpowers/specs/2026-09-16-api-integration-lab-design.md
rg -n '^## (Documentation|Ownership boundary)$' README.md
rg -n '^## (Choose a Reading Path|Document Catalog|Sources of Truth|Documentation Boundaries|Maintenance Rules)$' docs/README.md
git diff --check
```

Expected: the historical plan contains no stale working-tree reference, the design points to the
brief's immutable Git history location, required headings are found, and `git diff --check` exits
successfully.

- [ ] **Step 6: Review the task diff without committing**

Run:

```bash
git diff -- README.md docs/README.md \
  docs/superpowers/specs/2026-09-16-api-integration-lab-design.md \
  docs/superpowers/plans/2026-09-16-api-integration-lab.md
```

Confirm the historical files changed only at stale references and the README remains a concise
entry point. Do not commit.

---

### Task 2: Document architecture and durable decisions

**Files:**

- Create: `ARCHITECTURE.md`
- Create: `docs/adrs/adr-001-single-service-feature-folders.md`
- Create: `docs/adrs/adr-002-dual-microsoft-oauth-flows.md`
- Create: `docs/adrs/adr-003-exact-body-hmac-and-replay-cache.md`
- Create: `docs/adrs/adr-004-otel-collector-and-local-lgtm.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: `Program.cs`, controllers, typed clients, auth handlers, `DemoAggregator`, telemetry
  configuration, Dockerfile, Compose files, Collector configuration, and approved design.
- Produces: stable architecture vocabulary and decision links consumed by security, operations,
  resilience, observability, and learning documents.

- [ ] **Step 1: Write the C4-style architecture document**

Create `ARCHITECTURE.md` with frontmatter and these sections:

```markdown
# Architecture

## Purpose and Scope
## System Context (C4 Level 1)
## Container View (C4 Level 2)
## Component View (C4 Level 3)
## Critical Code Paths (C4 Level 4)
## Integration Architecture
## Critical Sequence Flows
## Data Flow and State
## Deployment Architecture
## Security and Trust Boundaries
## Key Design Decisions
## Technology Stack
## Failure Modes
## Scaling Model
## Explicit Limitations
```

Use themed Mermaid diagrams for:

1. system context: learner/browser, webhook sender, API, four provider systems, Collector, LGTM;
2. component view: controllers, handlers/token provider, typed clients, global errors, telemetry;
3. outbound request sequence with retry and final RFC 7807 failure branch;
4. delegated OAuth sequence with state/nonce, code redemption, cookie, token cache, and Graph;
5. exact-body HMAC verification with body limit, freshness, MAC, replay cache, JSON parse;
6. concurrent `/api/demo` aggregation with partial-success and all-failed branches;
7. OTLP export from API through Collector to LGTM.

State that the service is stateless except for the in-memory Microsoft token cache, local cookie
data-protection keys, and in-memory accepted-MAC replay cache. Describe why those choices limit the
lab to one API replica.

- [ ] **Step 2: Write ADR 001 for the service boundary**

Create `docs/adrs/adr-001-single-service-feature-folders.md` using ADR number `001`, status
`accepted`, date `2026-09-17`, decider `Amit Singh`, and domain `architecture`. Record the decision
to use one ASP.NET Core service with provider feature folders and typed-client interfaces.

Alternatives and rejection reasons:

- provider-per-microservice: unnecessary deployment, networking, and observability overhead for a
  local breadth demonstration;
- technical-layer folders only: separates controllers from the provider contracts they evolve with;
- one generic integration client: erases provider-specific authentication and protocol behavior.

- [ ] **Step 3: Write ADR 002 for dual Microsoft OAuth flows**

Create `docs/adrs/adr-002-dual-microsoft-oauth-flows.md` with domain `security`. Record the decision
to implement Authorization Code with a server-side cookie/token cache for user identity and Client
Credentials with `.default` for workload identity, both through Microsoft.Identity.Web.

Reject:

- a single client-credentials flow because it cannot represent a signed-in user;
- returning tokens to Swagger because it expands token exposure;
- hand-written OAuth HTTP exchanges because the lab should demonstrate protocol use without
  reimplementing state, nonce, code redemption, and token caching.

Document the negative consequence that in-memory caches do not support multi-replica production.

- [ ] **Step 4: Write ADR 003 for inbound HMAC authentication**

Create `docs/adrs/adr-003-exact-body-hmac-and-replay-cache.md` with domain `security`. Record exact
UTF-8 `<unix-seconds>.<body-bytes>` HMAC-SHA256, lowercase canonical hex, fixed-time comparison,
five-minute freshness, 64 KiB body cap, and one-time accepted-MAC cache.

Reject:

- signing parsed/re-serialized JSON because wire bytes can change;
- timestamp freshness alone because identical valid deliveries remain replayable;
- asymmetric signatures because key distribution and rotation exceed this local shared-secret lab.

State that a replicated service requires a shared replay store or signed delivery identifier.

- [ ] **Step 5: Write ADR 004 for the telemetry topology**

Create `docs/adrs/adr-004-otel-collector-and-local-lgtm.md` with domain `observability`. Record
OTel-native instrumentation, OTLP/gRPC to a standalone pinned Collector, bounded resource/batch
processing, and local Grafana LGTM.

Reject:

- direct backend SDKs because they couple application code to one vendor;
- direct API-to-LGTM export because Collector processing and routing are part of the learning goal;
- unpinned `latest` images because demonstrations must be reproducible.

- [ ] **Step 6: Validate architecture and ADR structure**

Run:

```bash
rg -n '^## (System Context \(C4 Level 1\)|Container View \(C4 Level 2\)|Component View \(C4 Level 3\)|Failure Modes|Scaling Model)$' ARCHITECTURE.md
rg -L '^## Alternatives Considered$' docs/adrs/*.md
rg -L '^## Consequences$' docs/adrs/*.md
rg -L "^%%\{init: \{'theme':'base'" ARCHITECTURE.md
git diff --check
```

Expected: all architecture headings exist, both `rg -L` commands print nothing, every Mermaid block
uses the explicit theme, and whitespace validation succeeds.

- [ ] **Step 7: Review the task diff without committing**

Confirm every diagram has a failure branch where relevant, every ADR has explicit rejected
alternatives, and no document claims a production deployment. Do not commit.

---

### Task 3: Document contributor workflow, local development, and dependencies

**Files:**

- Create: `CONTRIBUTING.md`
- Create: `DEVELOPMENT.md`
- Create: `DEPENDENCIES.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: solution/project files, `global.json`, Dockerfile, Compose files, `.env.example`, test
  projects, existing README commands, and AGENTS conventions supplied for this repository.
- Produces: reproducible contributor setup and upgrade guidance used by the learning guide and
  runbook.

- [ ] **Step 1: Write contribution guidance**

Create `CONTRIBUTING.md` with frontmatter and these sections:

```markdown
# Contributing

## Prerequisites
## Development Setup
## Repository Structure
## Coding Guidelines
## Integration Client Conventions
## Authentication and Secret Rules
## Error and Telemetry Conventions
## Branch and Pull Request Conventions
## Testing
## Formatting and Static Validation
## Review Process
## Documentation Updates
```

Document feature-folder placement, typed-client interfaces, provider DTO versus normalized model,
explicit error categories, cancellation propagation, HTTPS-before-credential checks, bounded input,
bounded telemetry labels, inline-comment policy, and the rule that docs change with behavior.

Use the repository's imperative commit-message convention but state that this documentation session
does not perform commits or pushes.

- [ ] **Step 2: Write the local development guide**

Create `DEVELOPMENT.md` with frontmatter and exact instructions for:

- SDK `8.0.131` via `global.json`;
- environment variables from `.env.example`;
- local `dotnet run --project src/ApiIntegrationLab.Api` without telemetry stack;
- Docker Compose full-stack execution;
- corporate-CA build and runtime override;
- focused and full unit/integration test commands;
- Swagger and Grafana URLs;
- debugger entry points in `Program.cs`, controllers, typed clients, auth handlers, exception
  handler, telemetry, and tests;
- common failure signatures: `NU1301`, provider `401/403/429`, unconfigured `503`, Graph cookie
  `401`, Collector connection errors, and HMAC rejection.

Make clear which checks work without credentials and which require real GitHub/Microsoft values.

- [ ] **Step 3: Write the dependency inventory**

Create `DEPENDENCIES.md` with frontmatter and separate tables for:

1. .NET SDK/runtime: `net8.0`, SDK `8.0.131`, runtime image family;
2. NuGet packages and exact versions from the API project;
3. test packages and exact versions from both test projects;
4. container images: OTel Collector `0.160.0` and Grafana LGTM `0.33.0`;
5. external providers: JSONPlaceholder, Postman Echo, GitHub REST, Entra ID, Microsoft Graph;
6. local tools: Docker Compose v2, curl, OpenSSL, Bash, ShellCheck.

For each dependency capture purpose, where pinned, runtime/build/test scope, credential exposure,
and safe-upgrade validation. Do not infer vulnerability status or support dates.

- [ ] **Step 4: Validate commands and dependency versions**

Run:

```bash
dotnet --version
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
rg -n '<PackageReference Include=' src tests -g '*.csproj'
rg -n 'image:' docker-compose.yml
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
git diff --check
```

Expected: SDK resolves through `global.json`, restore/build succeed, the dependency inventory matches
the displayed versions, shell validation succeeds, and the diff is clean.

- [ ] **Step 5: Review the task diff without committing**

Confirm every command starts at repository root, no real secret example exists, and development
guidance does not imply unavailable production infrastructure. Do not commit.

---

### Task 4: Document the API, authentication mechanisms, and learner journey

**Files:**

- Create: `docs/api/api-reference.md`
- Create: `docs/authentication.md`
- Create: `docs/learning-guide.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: controller routes/XML remarks, request bounds, normalized models, authentication
  handlers, token provider, webhook verifier, error handler, Swagger tests, and manual checks.
- Produces: the canonical human API contract and learning sequence consumed by the demo playbook,
  security document, and FAQ.

- [ ] **Step 1: Write the human API reference**

Create `docs/api/api-reference.md` with frontmatter and these sections:

```markdown
# API Reference

## Contract Authority and Base URLs
## Authentication Summary
## Common Error Contract
## Health
## Public API
## Basic Authentication
## GitHub
## Microsoft Authentication
## Microsoft Graph
## HMAC Webhook
## Aggregate Demo
## Pagination and Rate-Limit Semantics
## Runtime Swagger
```

Document every route:

- `GET /health`;
- `GET /api/public/posts?limit=10`, bound 1–100;
- `GET /api/basic/profile`;
- `GET /api/github/profile`;
- `GET /api/github/repos?perPage=30&maxPages=3`, bounds 1–100 and 1–10;
- `GET /api/github/rate-limit`;
- `GET /api/microsoft/login?returnUrl=/swagger`;
- `GET /api/microsoft/logout`;
- `GET /api/microsoft/me`;
- `GET /api/microsoft/users?top=10`, bound 1–50;
- `POST /api/webhooks/events`;
- `GET /api/demo`.

For every route specify auth, request parameters/headers, normalized response shape, success status,
expected error statuses, configuration prerequisites, and a redacted curl example where curl is
appropriate. State that login/logout require a browser redirect flow.

Document RFC 7807 extensions `provider`, `category`, `traceId`, and optional
`retryAfterSeconds`; map validation→400, authentication→401, authorization→403, throttled→429,
upstream→502, configuration→503, and timeout→504.

- [ ] **Step 2: Write the authentication guide**

Create `docs/authentication.md` with frontmatter and these sections:

```markdown
# Authentication Guide

## Identity Before Credentials
## Authentication Matrix
## No Authentication
## Basic Authentication
## GitHub Bearer/PAT
## OAuth 2.0 Authorization Code
## OAuth 2.0 Client Credentials
## Inbound HMAC-SHA256
## Choosing the Correct Pattern
## Credential and Token Lifecycles
## Failure Comparison
## Production Hardening Gaps
```

For each mechanism explain who/what is authenticated, where the credential originates, where it is
attached, who validates it, what the API returns, how failure appears, and what must never be logged.
Include themed sequence diagrams for Basic, GitHub bearer, both OAuth flows, and HMAC. Cross-link
the OAuth and HMAC ADRs instead of restating their alternatives.

- [ ] **Step 3: Write the guided learning path**

Create `docs/learning-guide.md` with frontmatter and a six-module progression:

1. anonymous HTTP and normalization;
2. outbound Basic and bearer header handlers;
3. GitHub pagination and rate limits;
4. delegated versus workload OAuth;
5. inbound HMAC and replay defense;
6. resilience, aggregation, and observability.

Every module must contain:

- learning objective;
- files to read in order;
- command or Swagger exercise;
- expected success and failure observation;
- explanation questions Amit should be able to answer;
- completion check tied to source or telemetry evidence.

End with a 10-minute manager-demo rehearsal and a concept glossary covering authentication,
authorization, delegated identity, workload identity, bearer token, PAT, HMAC, nonce, replay,
pagination, rate limiting, idempotency, timeout, retry, circuit breaker, bulkhead, RFC 7807, span,
metric cardinality, and OTLP.

- [ ] **Step 4: Validate route and authentication coverage**

Run:

```bash
rg -n '\[(HttpGet|HttpPost)' src/ApiIntegrationLab.Api
rg -n '^###? (GET|POST) ' docs/api/api-reference.md
rg -n '^## (No Authentication|Basic Authentication|GitHub Bearer/PAT|OAuth 2\.0 Authorization Code|OAuth 2\.0 Client Credentials|Inbound HMAC-SHA256)$' docs/authentication.md
rg -n '^## Module [1-6]:' docs/learning-guide.md
git diff --check
```

Expected: all controller actions have a documented route, six auth mechanisms and six learning
modules are present, and whitespace validation succeeds.

- [ ] **Step 5: Review the task diff without committing**

Compare models and examples against source. Confirm access tokens and secrets never appear in
responses or example values, and runtime Swagger remains identified as schema authority. Do not
commit.

---

### Task 5: Document non-functional requirements and resilience behavior

**Files:**

- Create: `docs/standards/non-functional-requirements.md`
- Create: `docs/resilience-patterns.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: input validation, `WebhookOptions`, `HttpClientRegistrationExtensions`, client exception
  mapping, `DemoAggregator`, telemetry schema, and resilience tests.
- Produces: exact implemented budgets and failure semantics consumed by operations, security, and
  testing documents.

- [ ] **Step 1: Write the NFR document**

Create `docs/standards/non-functional-requirements.md` with frontmatter and these sections:

```markdown
# Non-Functional Requirements

## Scope and Measurement Status
## Request and Payload Bounds
## Resilience Budgets
## Security Baseline
## Observability and Cardinality
## Reproducibility
## Testability
## Explicitly Undefined Production Targets
## Verification Matrix
```

Record exact implemented requirements:

- public limit 1–100;
- GitHub page size 1–100 and page count 1–10;
- Graph top 1–50;
- webhook body maximum 65,536 bytes and replay window five minutes;
- attempt timeout five seconds, total timeout fifteen seconds, two retries, 250 ms exponential base
  delay with jitter;
- HTTPS required before Basic, GitHub, or Graph credentials are attached;
- custom metric schema upper bound 90 streams before histogram backend expansion;
- API can start without optional provider credentials;
- Docker images and SDK are pinned.

State that availability, p99 latency, throughput, capacity, durability, RTO, RPO, SLA, and telemetry
retention are not defined for this local lab because no production measurement or operating model
exists.

- [ ] **Step 2: Write the resilience patterns guide**

Create `docs/resilience-patterns.md` with frontmatter and a table for:

- attempt timeout;
- total timeout;
- transient retry with jitter;
- circuit breaker from the standard handler;
- concurrency/rate limiter from the standard handler;
- caller cancellation;
- GitHub pagination bounds;
- Retry-After preservation;
- typed error normalization;
- `/api/demo` partial-success fallback;
- Collector memory limiter and batching;
- HMAC request-size/freshness/replay limits.

For each list where applied, exact configuration, guarded failure, amplification/trade-off, and
verification test. Explain why auth/authorization/validation failures are not retried, why retry
must stay inside a total timeout, how local Polly rejection becomes a stable integration error, and
why unexpected exceptions are not disguised as provider failures.

- [ ] **Step 3: Validate every numeric claim against source**

Run:

```bash
rg -n 'TimeSpan\.FromSeconds|TimeSpan\.FromMilliseconds|MaxRetryAttempts' \
  src/ApiIntegrationLab.Api/Common/Http src/ApiIntegrationLab.Api/Authentication
rg -n 'is < 1 or >|MaxBodyBytes|ReplayWindow' src/ApiIntegrationLab.Api
rg -n 'MaximumPreviewLength|ProviderOperations|ErrorTypes|TokenFlows|Outcomes' \
  src/ApiIntegrationLab.Api
git diff --check
```

Expected: every number in both documents has a matching source definition or documented computed
cardinality formula.

- [ ] **Step 4: Review the task diff without committing**

Confirm no unmeasured performance target is presented as current, all retry amplification risks are
explicit, and every resilience row points to a test or source location. Do not commit.

---

### Task 6: Document observability and cardinality

**Files:**

- Create: `docs/observability.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: `ApiTelemetry`, OpenTelemetry registration, trace middleware, error handler, Collector
  pipeline, Compose topology, live Grafana queries in README/manual checks, and telemetry tests.
- Produces: the canonical metrics, logs, traces, label, and local-pipeline guide used by operations
  and the manager demo.

- [ ] **Step 1: Write the observability guide**

Create `docs/observability.md` with frontmatter and these sections:

```markdown
# Observability Guide

## Monitoring Strategy
## Signal Flow
## Metrics Catalog
## Label and Attribute Taxonomy
## Cardinality Budget
## Logs
## Traces
## Correlation Workflow
## Grafana Exploration
## Collector Processing
## Retention and Sampling
## Alert and Dashboard Status
## Troubleshooting the Pipeline
## Production Extension Guidance
```

The metrics catalog must include:

- `api.client.requests` / Prometheus `api_client_requests_total`;
- `api.client.request.duration` / histogram sum and count;
- `api.client.errors` / `api_client_errors_total`;
- `api.auth.token.requests` / `api_auth_token_requests_total`;
- relevant ASP.NET Core, HttpClient, and runtime auto-instrumentation as categories rather than an
  unstable exhaustive list.

For every custom label list allowed values, maximum combinations, and Low cardinality risk. Show
the 90-stream custom upper-bound calculation. Mark request IDs, trace IDs, raw URLs, timestamps,
user IDs, repository names, identities, tokens, bodies, and signatures as forbidden metric labels;
trace IDs belong in logs/traces for correlation.

- [ ] **Step 2: Add executable Grafana backend queries**

Include exact examples:

```promql
api_client_requests_total
```

```promql
sum by (provider, operation, outcome) (rate(api_client_requests_total[5m]))
```

```promql
sum by (provider, operation) (rate(api_client_request_duration_seconds_sum[5m]))
/
sum by (provider, operation) (rate(api_client_request_duration_seconds_count[5m]))
```

```logql
{service_name="api-integration-lab"} |= "<trace-id-from-problem-details>"
```

Document Tempo service search for `service.name=api-integration-lab`, the
`deployment.environment.name=local` resource attribute, and the Problem Details `traceId`
correlation path. The angle-bracket trace ID is an explicit user-supplied query value, not an
unfinished documentation field.

- [ ] **Step 3: Document local retention and alert limitations accurately**

State that LGTM container-local retention and sampling are development defaults not configured or
guaranteed by this repository. State that there are no checked-in dashboards, alert rules, Grafana
folders, or on-call routes. Provide production extension guidance as prerequisites, not current
capabilities, and reiterate OTel-native collection with bounded labels.

- [ ] **Step 4: Validate schema and pipeline claims**

Run:

```bash
rg -n 'Create(Counter|Histogram)|CreateCounter|CreateHistogram|ProviderOperations|TokenFlows|Outcomes|ErrorTypes' \
  src/ApiIntegrationLab.Api/Common/Telemetry/ApiTelemetry.cs
rg -n 'Add(AspNetCore|HttpClient|Runtime)Instrumentation|AddOtlpExporter' \
  src/ApiIntegrationLab.Api/Common/Telemetry/OpenTelemetryConfiguration.cs
docker run --rm \
  --volume "$PWD/otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro" \
  otel/opentelemetry-collector-contrib:0.160.0 \
  validate --config=/etc/otelcol-contrib/config.yaml
git diff --check
```

Expected: documentation matches the instrument schema, all configured instrumentation is covered,
Collector validation exits successfully, and the diff is clean.

- [ ] **Step 5: Review the task diff without committing**

Confirm every label has a cardinality rating, forbidden high-churn fields are explicit, and no
Grafana Cloud credential or retention claim is introduced. Do not commit.

---

### Task 7: Write the local operations runbook and manager demo playbook

**Files:**

- Create: `docs/operations/runbook.md`
- Create: `docs/operations/demo-playbook.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: Docker/Compose files, environment template, health route, smoke script, manual OAuth
  checks, API reference, observability guide, resilience guide, and README troubleshooting.
- Produces: executable local operations and demonstration procedures.

- [ ] **Step 1: Write the local runbook**

Create `docs/operations/runbook.md` with frontmatter and these sections:

```markdown
# Runbook: API Integration Lab

## Service Overview
## Scope and Ownership
## Health Checks
## Start, Stop, and Restart
## Common Failure Modes
## Provider Diagnosis
## Authentication Diagnosis
## Telemetry Pipeline Diagnosis
## Corporate CA Recovery
## Maintenance Procedures
## Local Rollback
## Local Data Recovery
## Escalation Boundary
```

Use exact commands for Compose status/logs, API health, Swagger, Grafana health, smoke script,
Collector config validation, Release tests, image rebuild, corporate-CA startup, restart after secret
changes, and teardown. Local rollback means checking out or rebuilding a known Git revision; do not
describe production traffic rollback.

The failure table must cover API unhealthy, `NU1301`, provider `401`, Graph `403`, GitHub `429`,
Graph `/me` cookie `401`, HMAC `401`, Collector export failure, empty Grafana, and port conflicts.

- [ ] **Step 2: Write the manager demo playbook**

Create `docs/operations/demo-playbook.md` with frontmatter and these sections:

```markdown
# Demo Playbook: API Integration Lab

## Objective and Audience
## Pre-demo Checklist
## Credential Safety
## Start and Verify
## Ten-minute Narrative
## Demonstration Steps
## Failure Demonstrations
## Observability Evidence
## Questions to Be Ready For
## Cleanup
## Fallback Plan
```

The narrative must show:

1. health and Swagger without credentials;
2. public API normalization;
3. Basic and GitHub header-based authentication;
4. GitHub pagination/rate limits;
5. delegated user identity versus client-credentials workload identity;
6. valid, invalid, and replayed HMAC webhook requests;
7. concurrent `/api/demo` partial success;
8. one trace correlated to logs and metrics in Grafana.

For every step list expected status and lesson. The fallback plan must use automated tests and
recorded source/Swagger evidence when external providers or internet access are unavailable; it
must not suggest recording secrets or personal provider payloads.

- [ ] **Step 3: Execute the credential-free runbook subset**

Run:

```bash
docker compose --env-file .env.example config --quiet
docker compose --file docker-compose.yml \
  --file docker-compose.corporate-ca.yml \
  --env-file .env.example config --quiet
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
git diff --check
```

Expected: both Compose models validate, the smoke script passes syntax and ShellCheck, and the diff
is clean. Do not use real credentials during document validation.

- [ ] **Step 4: Review the task diff without committing**

Confirm all operations are local, cleanup does not delete volumes unless explicitly stated, status
codes match the API reference, and no runbook claims an on-call escalation path. Do not commit.

---

### Task 8: Document security, testing, and recurring questions

**Files:**

- Create: `SECURITY.md`
- Create: `docs/standards/test-strategy.md`
- Create: `FAQ.md`
- Modify: `docs/README.md`

**Interfaces:**

- Consumes: authentication guide, architecture trust boundaries, API/error contract, security tests,
  telemetry safety tests, test project structure, manual guide, and runbook.
- Produces: the security review baseline, quality model, and concise recurring-question entry point.

- [ ] **Step 1: Write SECURITY.md and the STRIDE model**

Create `SECURITY.md` with frontmatter and these sections:

```markdown
# Security

## Scope and Security Posture
## Trust Boundaries and Assets
## Threat Model (STRIDE)
## Attack Surface
## Authentication
## Authorization
## Secrets Management and Rotation
## Certificate and Trust Management
## Network Exposure
## Input and Payload Controls
## Logging and Telemetry Safety
## Dependency and Vulnerability Management
## OWASP API Security Review
## Penetration Testing Status
## Security Baseline
## Known Limitations
## Reporting Vulnerabilities
```

The STRIDE table must cover malicious API callers, a webhook sender/attacker, compromised or
misconfigured provider credentials, malicious pagination links, external providers, local Docker
network participants, telemetry consumers, and a compromised workstation. Tie mitigations to
specific source/tests.

State that authorization is provider-permission and route-based rather than application RBAC/ABAC;
`/api/microsoft/me`, logout, and `/api/demo` require the local authenticated session while app-only
Graph users does not. State that localhost HTTP is a known limitation and non-local deployment
requires TLS, durable data-protection keys/token cache, shared replay defense, and a secret manager.

For vulnerability reporting, direct reports to the repository owner through a private channel and
explicitly prohibit opening public issues containing credentials or exploit details; do not invent
an email address or response SLA.

- [ ] **Step 2: Write the test strategy**

Create `docs/standards/test-strategy.md` with frontmatter and these sections:

```markdown
# Test Strategy

## Quality Goals
## Test Pyramid
## Unit Test Scope
## Integration Test Scope
## Smoke Test Scope
## Credentialed Manual Checks
## Test Data and Secret Strategy
## Resilience and Security Coverage
## Observability Coverage
## Compatibility Matrix
## Local Quality Gates
## Coverage Gaps
```

Document the current verified inventory as 82 unit and 28 integration tests, but state that counts
are snapshots and the test runner is authoritative. Explain mocked `HttpMessageHandler`, full
`WebApplicationFactory` routing/middleware tests, credential-free smoke behavior, and why real OAuth
and provider credentials remain manual.

List gaps honestly: no load test, no browser automation for Entra, no mutation/fuzz suite, no CI
workflow, and no external-provider contract test in automation.

- [ ] **Step 3: Write FAQ.md**

Create `FAQ.md` with frontmatter and concise answers to at least these questions:

- Why does the application start with empty credentials?
- Why does GitHub return 401 without a PAT?
- Why is `/api/microsoft/me` protected but `/api/microsoft/users` app-only?
- Why does login use a browser rather than curl?
- Why can a valid HMAC request still be rejected as a replay?
- Why is the raw webhook body verified before JSON parsing?
- Why are retries limited and auth failures not retried?
- Why can `/api/demo` return partial data?
- Where are tokens stored and why are they absent from responses?
- How do I find a failed request in Grafana?
- Why are user IDs and trace IDs not metric labels?
- What changes before multi-replica or non-local deployment?
- What works without internet or provider credentials?
- Where is the generated OpenAPI contract?

Link each answer to the canonical deeper document instead of duplicating full procedures.

- [ ] **Step 4: Run the full automated test suites**

Run:

```bash
dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj \
  --configuration Release --no-build --verbosity minimal
dotnet test tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj \
  --configuration Release --no-build --verbosity minimal
git diff --check
```

Expected: 82 unit and 28 integration tests pass, with zero failures, and the documentation diff has
no whitespace errors. If counts have legitimately changed, update the test-strategy snapshot to the
fresh result rather than forcing the old count.

- [ ] **Step 5: Review the task diff without committing**

Confirm every security mitigation has source/test evidence, no contact or SLA was invented, test
gaps are explicit, and FAQ answers link to canonical documents. Do not commit.

---

### Task 9: Integrate, validate, and review the complete documentation set

**Files:**

- Modify: `README.md`
- Modify: `docs/README.md`
- Review: every Markdown file created or modified in Tasks 1–8

**Interfaces:**

- Consumes: every planned document and the approved documentation design.
- Produces: a coherent, navigable, verified documentation system and final coverage report.

- [ ] **Step 1: Complete the catalog and cross-links**

Update `docs/README.md` so every created document is linked exactly once in the primary catalog and
appears in each relevant reading path. Update `README.md` direct links to use final paths. Verify
documents link outward to canonical sources as follows:

- architecture → ADRs, security, resilience, observability;
- API → authentication and runtime Swagger;
- learning guide → API, authentication, resilience, observability, demo playbook;
- runbook → observability, security, development;
- test strategy → contributing and manual checks;
- FAQ → all canonical deeper documents.

- [ ] **Step 2: Scan for unfinished or misleading content**

Run:

```bash
rg -n --glob '*.md' '(\[insert |\[replace |\[owner\]|\[date\]|\[value here\])' .
rg -n --glob '*.md' '(99\.9%|RTO|RPO|24x7|production SLA)' .
rg -n --glob '*.md' '(github_pat_|gh[pousr]_|glc_|glsa_|-----BEGIN .*PRIVATE KEY-----)' .
```

Expected: the first and third scans print nothing. The second may find explicit statements that
these production targets are undefined or out of scope; inspect every match and remove any claim
that presents them as implemented.

- [ ] **Step 3: Validate relative Markdown links**

Run this read-only validator from the repository root:

```bash
perl -MFile::Find -MFile::Spec -MFile::Basename=dirname -e '
my @failures;
find({ wanted => sub {
  my $file = $File::Find::name;
  return unless -f $file && $file =~ /\.md\z/;
  return if $file =~ m{^\./\.git/};
  open my $fh, "<", $file or die "cannot read $file: $!";
  local $/;
  my $body = <$fh>;
  while ($body =~ /\[[^\]]+\]\(([^)]+)\)/g) {
    my $target = $1;
    next if $target =~ m{^(?:https?://|mailto:|#)};
    $target =~ s/#.*\z//;
    next if $target eq "";
    my $path = File::Spec->canonpath(File::Spec->catfile(dirname($file), $target));
    push @failures, "$file: $target" unless -e $path;
  }
}, no_chdir => 1 }, ".");
die join("\n", @failures), "\n" if @failures;
print "All relative Markdown links resolve.\n";
'
```

Expected: `All relative Markdown links resolve.`

- [ ] **Step 4: Validate Mermaid conventions and Markdown hygiene**

Run:

```bash
rg -l '```mermaid' --glob '*.md' .
rg -L "^%%\{init: \{'theme':'base'" ARCHITECTURE.md docs/authentication.md
rg -n --glob '*.md' '[[:blank:]]+$' .
git diff --check
```

Expected: only intended documents contain Mermaid, the theme check prints nothing, the trailing
whitespace scan prints nothing, and Git diff validation succeeds.

- [ ] **Step 5: Run final repository verification**

Run:

```bash
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build --verbosity minimal
dotnet format ApiIntegrationLab.sln --verify-no-changes --no-restore
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
docker compose --env-file .env.example config --quiet
docker compose --file docker-compose.yml \
  --file docker-compose.corporate-ca.yml \
  --env-file .env.example config --quiet
docker run --rm \
  --volume "$PWD/otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro" \
  otel/opentelemetry-collector-contrib:0.160.0 \
  validate --config=/etc/otelcol-contrib/config.yaml
```

Expected: restore succeeds, Release build has zero warnings/errors, all tests pass, formatting
changes zero files, shell and Compose checks pass, and the pinned Collector accepts its config.

- [ ] **Step 6: Review source-to-document claim samples**

Sample and verify at minimum:

- every API route and bound against controller/client code;
- all error status mappings against `IntegrationException.StatusFor`;
- retry/timeout values against `AddIntegrationResilience`;
- OAuth scopes and redirects against app settings and controllers;
- HMAC format/limits against options and verifier;
- metric instruments and labels against `ApiTelemetry`;
- image and package pins against manifests;
- test counts against fresh runner output.

Correct documentation, not executable source, when a claim disagrees.

- [ ] **Step 7: Perform a final documentation review**

Use the `pr-code-review` skill on all documentation changes. Accept completion only when no Critical
or Important documentation defect remains: broken command, broken link, inaccurate security claim,
missing route, contradictory source-of-truth statement, leaked secret, or unsupported production
claim.

- [ ] **Step 8: Publish the coverage report without committing**

Report a table with every updated, written, retained, and skipped artifact. Close with the exact
counts of applicable document types completed and domains skipped with reasons. Include verification
evidence and disclose that real GitHub/Basic/Microsoft credential flows were not rerun without user
secrets. Show `git status --short`, leave all changes uncommitted, and do not push.
