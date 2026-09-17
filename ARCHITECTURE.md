---
title: "API Integration Lab Architecture"
status: active
scope: lab
owner: Amit Singh
last_reviewed: 2026-09-17
---

# Architecture

## Purpose and Scope

The API Integration Lab is a single .NET 8 service built to teach and demonstrate integration
boundaries: choosing an identity, placing credentials, calling an external contract, normalizing
responses, containing failure, and producing safe telemetry. It is deliberately a localhost lab,
not a production platform or a collection of independently deployed microservices.

The service owns its public HTTP contract, input bounds, credential attachment, provider response
normalization, error classification, resilience policy, aggregation, and telemetry. GitHub,
Microsoft, JSONPlaceholder, and Postman Echo own their identities, data, APIs, and availability.

## System Context (C4 Level 1)

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
flowchart LR
    Learner[Learner or manager] -->|Swagger / HTTP| Lab[API Integration Lab]
    Sender[Webhook sender] -->|Signed event| Lab
    Lab -->|Anonymous REST| Public[JSONPlaceholder]
    Lab -->|Basic Auth| Basic[Postman Echo]
    Lab -->|Bearer PAT| GitHub[GitHub REST]
    Lab -->|OIDC and OAuth| Entra[Microsoft Entra ID]
    Lab -->|Bearer access token| Graph[Microsoft Graph]
    Lab -->|OTLP/gRPC| Collector[OpenTelemetry Collector]
    Collector -->|OTLP/gRPC| LGTM[Grafana LGTM]
    Learner -->|Explore signals| Grafana[Grafana UI]
    LGTM --> Grafana

    classDef actor fill:#fde68a,stroke:#f59e0b,color:#111827;
    classDef app fill:#bfdbfe,stroke:#3b82f6,color:#111827;
    classDef provider fill:#bbf7d0,stroke:#22c55e,color:#111827;
    classDef telemetry fill:#e9d5ff,stroke:#a855f7,color:#111827;
    class Learner,Sender actor;
    class Lab app;
    class Public,Basic,GitHub,Entra,Graph provider;
    class Collector,LGTM,Grafana telemetry;
```

Trust crosses the browser/API boundary, webhook sender/API boundary, every outbound provider
boundary, and the API/telemetry boundary. Credentials are accepted or acquired only at those
explicit seams.

## Container View (C4 Level 2)

| Container | Runtime | Responsibility | State |
| --- | --- | --- | --- |
| API | ASP.NET Core on .NET 8 | HTTP surface, auth, typed provider clients, normalization, aggregation, errors, instrumentation | Encrypted browser cookie, in-memory Graph token cache, in-memory webhook replay cache |
| OTel Collector | Collector Contrib `0.160.0` | Receive OTLP, cap memory, add local environment, batch, forward | Process memory only |
| Grafana LGTM | `grafana/otel-lgtm:0.33.0` | Local Loki, Grafana, Tempo, and Prometheus-compatible exploration | Container-local development data |

Docker Compose creates one private network. Only API port `8080` and Grafana port `3000` are
published. The API and Collector configuration are not mounted writable.

## Component View (C4 Level 3)

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
flowchart TB
    HTTP[ASP.NET middleware] --> Controllers[Feature controllers]
    Controllers --> Demo[Demo aggregator]
    Controllers --> Clients[Typed integration clients]
    Controllers --> Webhook[Webhook verifier]
    Controllers --> Auth[Microsoft auth controller]
    Demo --> Clients
    Clients --> Handlers[Basic / GitHub auth handlers]
    Clients --> Tokens[Graph token provider]
    Handlers --> Resilience[Standard resilience pipeline]
    Tokens --> Resilience
    Resilience --> Providers[External providers]
    HTTP --> Errors[Integration exception handler]
    Controllers -. failures .-> Errors
    Clients -. bounded spans and metrics .-> Telemetry[ApiTelemetry + OTel]
    HTTP -. auto instrumentation .-> Telemetry

    classDef edge fill:#fde68a,stroke:#f59e0b,color:#111827;
    classDef feature fill:#bfdbfe,stroke:#3b82f6,color:#111827;
    classDef cross fill:#e9d5ff,stroke:#a855f7,color:#111827;
    classDef external fill:#bbf7d0,stroke:#22c55e,color:#111827;
    class HTTP,Providers edge;
    class Controllers,Demo,Clients,Webhook,Auth,Handlers,Tokens feature;
    class Resilience,Errors,Telemetry cross;
```

Feature folders keep each provider's controller, options, DTOs, normalized models, client, and auth
handler together. `Common/` contains only policies shared across providers: errors, resilience,
telemetry, validation, Swagger, health, and time.

## Critical Code Paths (C4 Level 4)

| Path | Key types | Why it warrants detail |
| --- | --- | --- |
| Provider call | Controller → `I*Client` → typed client → optional auth handler → resilience handler | Shows the distinction between API authentication, outbound credential placement, provider failure, and safe normalization |
| Delegated Graph | `MicrosoftAuthController` → OIDC middleware → `MicrosoftGraphTokenProvider` → `MicrosoftGraphClient` | Combines browser state, user identity, server token acquisition, and downstream API authorization |
| App-only Graph | `MicrosoftGraphController` → token provider application flow → Graph client | Represents the workload rather than a browser user |
| Webhook | `WebhookController` → `WebhookSignatureVerifier` → JSON parse | Security requires exact bytes and verification before deserialization |
| Aggregate | `DemoController` → `DemoAggregator` → three provider tasks | Demonstrates concurrency and partial success without masking unexpected defects |

## Integration Architecture

| External system | Direction | Protocol | Authentication | Failure behavior | Contract owner |
| --- | --- | --- | --- | --- | --- |
| JSONPlaceholder | Outbound | HTTPS REST/JSON | None | Transient failures retry; final failures become bounded upstream/timeout errors | JSONPlaceholder |
| Postman Echo | Outbound | HTTPS REST/JSON | Basic header | Missing local config becomes 503; provider auth becomes 401/403 | Postman Echo |
| GitHub REST | Outbound | HTTPS REST/JSON | Bearer PAT | Bounded pagination; cross-host links rejected; 429/limit exhaustion retains retry delay | GitHub |
| Microsoft Entra ID | Outbound/browser | OIDC + OAuth 2.0 | Auth Code or Client Credentials | Explicit login redirects; protected APIs keep 401/403 semantics | Microsoft |
| Microsoft Graph | Outbound | HTTPS REST/JSON | Bearer access token | Delegated and app-only paths map provider/auth/timeout failures safely | Microsoft |
| Webhook sender | Inbound | HTTP POST/JSON | HMAC-SHA256 | Reject oversized, stale, malformed, invalid, or replayed deliveries before processing | Sender and this API share the signing contract |
| OTel Collector | Outbound | OTLP/gRPC | Local network trust | Application work continues if export is unavailable; SDK/Collector buffering remains bounded | This repository |

## Critical Sequence Flows

### Outbound provider call

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
sequenceDiagram
    participant Caller
    participant Controller
    participant Client as Typed client
    participant Policy as Resilience pipeline
    participant Provider
    participant Handler as Exception handler
    Caller->>Controller: Bounded request
    Controller->>Client: Normalized operation + cancellation
    Client->>Policy: HTTPS request
    loop At most 3 total attempts
        Policy->>Provider: Attempt, 5s attempt timeout
        alt Transient 408/429/5xx/transport
            Provider-->>Policy: Transient failure
        else Final response
            Provider-->>Policy: HTTP + JSON
        end
    end
    alt Success and valid payload
        Policy-->>Client: Response
        Client-->>Controller: Normalized model
        Controller-->>Caller: 200 JSON
    else Exhausted or invalid
        Client-->>Handler: IntegrationException
        Handler-->>Caller: RFC 7807 + traceId
    end
```

### Delegated Authorization Code

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
sequenceDiagram
    participant Browser
    participant API
    participant Entra
    participant Cache as Token cache
    participant Graph
    Browser->>API: GET /api/microsoft/login
    API->>Entra: OIDC challenge with state + nonce
    Entra-->>Browser: Sign-in and consent
    Browser->>API: Callback with one-time code
    API->>Entra: Validate response and redeem code
    Entra-->>API: ID/access tokens
    API->>Cache: Store tokens server-side
    API-->>Browser: Encrypted HTTP-only session cookie
    Browser->>API: GET /api/microsoft/me + cookie
    API->>Cache: Acquire delegated User.Read token
    API->>Graph: GET /me with bearer token
    alt Authorized
        Graph-->>API: User JSON
        API-->>Browser: Normalized user, no token
    else Missing session/consent
        Graph-->>API: 401 or 403
        API-->>Browser: Safe Problem Details
    end
```

### Exact-body HMAC verification

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
sequenceDiagram
    participant Sender
    participant Endpoint
    participant Verifier
    participant Replay as Replay cache
    Sender->>Endpoint: Timestamp + sha256=MAC + exact bytes
    Endpoint->>Endpoint: Read at most 65,536 bytes
    alt Oversized
        Endpoint-->>Sender: 413 Problem Details
    else Within limit
        Endpoint->>Verifier: Headers + exact bytes
        Verifier->>Verifier: Parse timestamp and require ±5m
        Verifier->>Verifier: HMAC(timestamp.body), fixed-time compare
        Verifier->>Replay: Atomically register accepted MAC
        alt Invalid, stale, or duplicate
            Verifier-->>Sender: 401 Problem Details
        else First valid delivery
            Verifier-->>Endpoint: Verified
            Endpoint->>Endpoint: Parse JSON and validate eventType
            Endpoint-->>Sender: 202 safe acknowledgement
        end
    end
```

### Concurrent aggregate

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
sequenceDiagram
    participant Browser
    participant Demo
    participant Public
    participant GitHub
    participant Graph
    Browser->>Demo: GET /api/demo with session cookie
    par Independent calls start together
        Demo->>Public: Posts
    and
        Demo->>GitHub: Repositories
    and
        Demo->>Graph: Delegated profile
    end
    Public-->>Demo: Success or IntegrationException
    GitHub-->>Demo: Success or IntegrationException
    Graph-->>Demo: Success or IntegrationException
    alt At least one success
        Demo-->>Browser: 200, one envelope per provider
    else All expected provider calls fail
        Demo-->>Browser: 502 Problem Details
    end
    Note over Demo: Caller cancellation propagates; unexpected bugs are not converted to provider failures
```

### Telemetry export

```mermaid
%%{init: {'theme':'base','themeVariables':{'background':'#111827','primaryTextColor':'#111827','lineColor':'#cbd5e1','fontFamily':'Inter, sans-serif'}}}%%
flowchart LR
    API[API: ASP.NET + HttpClient + custom OTel] -->|OTLP/gRPC| Receive[Collector OTLP receiver]
    Receive --> Limit[Memory limiter: 256 MiB / 64 MiB spike]
    Limit --> Resource[Add deployment.environment.name=local]
    Resource --> Batch[Batch: 2s]
    Batch -->|OTLP/gRPC, local plaintext| LGTM[LGTM]
    LGTM --> Prom[Prometheus-compatible metrics]
    LGTM --> Loki[Loki logs]
    LGTM --> Tempo[Tempo traces]
    Prom --> Grafana[Grafana Explore]
    Loki --> Grafana
    Tempo --> Grafana

    classDef source fill:#bfdbfe,stroke:#3b82f6,color:#111827;
    classDef processor fill:#fde68a,stroke:#f59e0b,color:#111827;
    classDef backend fill:#e9d5ff,stroke:#a855f7,color:#111827;
    class API source;
    class Receive,Limit,Resource,Batch processor;
    class LGTM,Prom,Loki,Tempo,Grafana backend;
```

## Data Flow and State

1. ASP.NET validates route/query inputs before provider I/O where possible.
2. A controller calls a provider interface or the aggregate interface with the caller's cancellation
   token.
3. An auth handler or token provider adds credentials only after confirming HTTPS.
4. The shared resilience handler bounds each attempt and the whole operation.
5. Provider DTOs are validated and mapped into smaller owned response models.
6. Expected failures become `IntegrationException`; the global handler emits safe RFC 7807 JSON.
7. Logs, metrics, and spans carry bounded operational dimensions, never credentials or payloads.

No provider response is persisted. In-memory Microsoft tokens, cookie keys, and accepted webhook
MACs are lost on restart, which is correct for this local lab and unsuitable for multiple replicas.

## Deployment Architecture

The only supported deployment is Docker Compose on one developer machine. The API calls internet
providers over HTTPS and sends OTLP to the Collector over the private Compose network. The Collector
forwards to LGTM over that same network. The optional corporate-CA override mounts the host's public
trust bundle read-only; build-time restore uses a BuildKit secret that is absent from final layers.

There is no environment-promotion flow, ingress, cloud load balancer, Kubernetes workload, or
multi-region topology.

## Security and Trust Boundaries

- Browser sessions cross the local HTTP boundary and rely on encrypted HTTP-only cookies.
- Basic, GitHub, and Graph credentials cross only HTTPS provider boundaries.
- HMAC proves possession of a shared secret and binds the timestamp to exact transmitted bytes.
- The replay cache protects one process only.
- Telemetry receives operational metadata; payloads, tokens, signatures, and identities are excluded
  from custom dimensions.
- Grafana is a local development UI with no repository-defined access-control policy.

See [SECURITY.md](SECURITY.md) for the threat model.
See [resilience patterns](docs/resilience-patterns.md) for exact failure budgets and
[observability](docs/observability.md) for the signal/cardinality contract.

## Key Design Decisions

- [ADR 001](docs/adrs/adr-001-single-service-feature-folders.md): one service with provider feature
  folders and typed-client interfaces.
- [ADR 002](docs/adrs/adr-002-dual-microsoft-oauth-flows.md): separate delegated and app-only
  Microsoft OAuth flows.
- [ADR 003](docs/adrs/adr-003-exact-body-hmac-and-replay-cache.md): exact-body HMAC plus freshness
  and one-time replay detection.
- [ADR 004](docs/adrs/adr-004-otel-collector-and-local-lgtm.md): OTel-native signals through a
  standalone Collector to pinned local LGTM.

## Technology Stack

| Layer | Technology | Why |
| --- | --- | --- |
| HTTP service | ASP.NET Core / .NET 8 | Typed middleware, auth, controllers, DI, and test host |
| Provider access | Typed `HttpClient` | One provider contract and policy boundary per integration |
| Resilience | Microsoft.Extensions.Http.Resilience / Polly | Standard timeout, retry, breaker, and limiter behavior |
| Microsoft identity | Microsoft.Identity.Web | Protocol-correct OIDC/OAuth handling and token acquisition |
| API discovery | Swashbuckle | Executable Swagger UI generated from route metadata and XML remarks |
| Telemetry | OpenTelemetry .NET + Collector | Vendor-neutral instrumentation and routing |
| Local backend | Grafana LGTM | One-container exploration of metrics, logs, and traces |
| Packaging | Docker + Compose | Reproducible laptop demonstration |
| Tests | xUnit + WebApplicationFactory | Client-level and complete-host behavior without live credentials |

## Failure Modes

| Scenario | Impact | Mitigation |
| --- | --- | --- |
| Provider unavailable or slow | One operation times out or returns upstream failure | Attempt and total timeouts, bounded retry, circuit breaker, typed error |
| Provider throttles | Request returns 429 | Preserve safe Retry-After and avoid unbounded client retry |
| Missing credentials | Only that feature is unavailable | Optional startup; route returns actionable 503 or provider 401 |
| Expired/under-scoped credential | Provider returns 401/403 | Preserve auth versus authorization category without exposing body |
| Malformed provider JSON | Operation fails safely | Validate DTO and return owned upstream error |
| Malicious GitHub next link | Risk of bearer-token disclosure | Reject any pagination URI that changes host |
| Webhook replay | Duplicate event acceptance | Freshness window plus atomic accepted-MAC cache |
| API process restart | In-memory delegated token cache and replay memory lost | Sign in again; a surviving cookie alone cannot restore the token cache |
| Collector/LGTM unavailable | Telemetry temporarily absent | Bounded SDK/Collector processing; application request path remains independent |
| Every demo provider fails | No useful aggregate | Return 502; otherwise preserve partial success as 200 |

## Scaling Model

Most request handling is stateless and asynchronous, so CPU/memory and provider quotas are the first
single-process constraints. Input and pagination limits cap work per call, while the standard
resilience limiter prevents unbounded concurrent outbound work.

Horizontal scaling is intentionally unsupported: the in-memory token cache, cookie data-protection
keys, and webhook replay cache are not shared. A production multi-replica design would first need a
shared encrypted token/session strategy, durable data-protection keys, distributed replay storage,
TLS ingress, secret management, health/readiness separation, load tests, and production SLOs.

## Explicit Limitations

- Local HTTP only; do not expose this Compose stack to an untrusted network.
- No database, durable event handling, queue, or provider mutation.
- No production alert rules, dashboards-as-code, retention guarantees, or on-call ownership.
- No cloud, Kubernetes, Terraform, CI/CD, HA, DR, BCP, or multi-region design.
- External providers and internet connectivity are outside this repository's control.
