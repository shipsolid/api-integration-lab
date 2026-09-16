My manager asked me if I can prove I know api integration. Connect to an app with authentication (cover multiple authentication for coverage of breadth) and get data from that app.

Yes. The strongest way to demonstrate this is **not** to build several unrelated integrations. Build one small **API Integration Lab** that connects to a few APIs using different authentication mechanisms and normalizes the returned data.

For example:

| Integration             | Authentication                       | What you demonstrate                                   |
| ----------------------- | ------------------------------------ | ------------------------------------------------------ |
| Public API              | None                                 | HTTP/REST fundamentals                                 |
| GitHub API              | Personal Access Token / Bearer token | Token-based authentication                             |
| Basic-auth test API     | Basic Auth                           | Username/password → `Authorization: Basic`             |
| Microsoft Graph         | OAuth 2.0 Authorization Code         | User-delegated authentication, consent, token exchange |
| Azure / Microsoft Graph | OAuth 2.0 Client Credentials         | Machine-to-machine authentication                      |
| Optional webhook        | HMAC signature                       | Authenticating inbound API calls                       |

For the token-based case, [GitHub](https://github.com?utm_source=chatgpt.com) is particularly useful because you can authenticate, call endpoints such as the authenticated-user/repository APIs, handle pagination and rate-limit headers, and return meaningful data. For OAuth, [Microsoft Graph](https://graph.microsoft.com?utm_source=chatgpt.com) gives you a substantially stronger demonstration because you can show the actual OAuth flow rather than merely supplying a static secret.

### I would build it like this

Since you already have .NET experience, make a small **.NET 8 Web API**:

```text
api-integration-lab/
│
├── src/
│   ├── Api/
│   ├── Integrations/
│   │   ├── PublicApi/
│   │   ├── GitHub/
│   │   ├── BasicAuth/
│   │   └── MicrosoftGraph/
│   │
│   ├── Authentication/
│   │   ├── BasicAuth/
│   │   ├── BearerToken/
│   │   ├── OAuthAuthorizationCode/
│   │   └── OAuthClientCredentials/
│   │
│   └── Common/
│       ├── Http/
│       ├── Models/
│       └── Resilience/
│
├── tests/
├── docker-compose.yml
└── README.md
```

Expose your own endpoints such as:

```http
GET /integrations/public/data
GET /integrations/github/profile
GET /integrations/github/repos

GET /integrations/basic/data

GET /auth/microsoft/login
GET /auth/microsoft/callback
GET /integrations/microsoft/profile

GET /integrations/microsoft/app-data
```

The important part is that you can explain the authentication lifecycle rather than just show that HTTP calls work.

For example, **OAuth Authorization Code**:

```text
Your application
      │
      │ redirect user
      ▼
Identity Provider
      │
      │ login + consent
      ▼
Authorization Code
      │
      │ POST /token
      ▼
Access Token
      │
      │ Authorization: Bearer <token>
      ▼
Microsoft Graph
      │
      ▼
JSON response
```

And **Client Credentials**:

```text
Your application
      │
      │ client_id
      │ client_secret
      ▼
Identity Provider
      │
      ▼
Access Token
      │
      ▼
API
```

Those two OAuth flows are particularly valuable because you can explain **why they exist**: Authorization Code represents a user; Client Credentials represents an application/workload.

### Don't stop at authentication

If your manager wants proof that you understand **API integration**, I'd deliberately demonstrate the production concerns too:

**HTTP → Authentication → Serialization → Pagination → Rate limiting → Retries → Timeouts → Error handling → Secret management → Logging/metrics/tracing**

Instrument the application with OpenTelemetry. Then a request can produce a trace like:

```text
GET /integrations/github/repos
        │
        ├── authenticate
        │
        ├── github.api GET /user
        │
        ├── github.api GET /user/repos?page=1
        │
        ├── github.api GET /user/repos?page=2
        │
        └── transform response
```

That connects the exercise directly to your observability background instead of making it look like a basic coding demo.

### What I would present to your manager

The demo story becomes:

> **"I built a small integration service demonstrating API consumption across unauthenticated APIs, Basic authentication, static bearer-token authentication, OAuth 2.0 delegated authentication and OAuth 2.0 machine-to-machine authentication. It handles token acquisition, pagination, errors, rate limits and resilience, with OpenTelemetry instrumentation around the outbound API calls."**

That's substantially stronger evidence than saying *"I know how to call REST APIs."*

I would keep the project small enough to complete in **1–2 days**, with **4 authentication mechanisms implemented properly** rather than 10 superficial examples.

---

How shall I deploy and showcase on local?

For this proof-of-concept, I would **not deploy to Azure initially**. Make the entire demo reproducible on a laptop with Docker Compose. That actually makes the showcase cleaner: clone → configure secrets → `docker compose up` → demo.

## Recommended local architecture

```text
                        YOUR LAPTOP
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   Browser                                                   │
│      │                                                      │
│      ▼                                                      │
│  ┌───────────────┐                                         │
│  │ Swagger UI    │  http://localhost:8080/swagger           │
│  └───────┬───────┘                                         │
│          │                                                  │
│          ▼                                                  │
│  ┌───────────────────────┐                                 │
│  │ API Integration Lab   │                                 │
│  │ .NET 8 Web API        │                                 │
│  │                       │                                 │
│  │ Authentication        │                                 │
│  │ ├─ None               │                                 │
│  │ ├─ Basic              │                                 │
│  │ ├─ Bearer/PAT         │                                 │
│  │ ├─ OAuth Auth Code    │                                 │
│  │ └─ Client Credentials │                                 │
│  └───────────┬───────────┘                                 │
│              │                                              │
│              │ OpenTelemetry                               │
│              ▼                                              │
│        ┌───────────┐                                       │
│        │ OTel      │                                       │
│        │ Collector │                                       │
│        └─────┬─────┘                                       │
│              │                                              │
└──────────────┼──────────────────────────────────────────────┘
               │
               │ HTTPS
               ▼
        External APIs
        ├─ GitHub
        ├─ Microsoft Graph
        └─ Test APIs
```

I would use **Swagger/OpenAPI as the demo UI** rather than building React/Angular. Your objective is API integration, not frontend development.

## Dockerize it

Your `Dockerfile` can be conventional:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Api/Api.csproj \
    -c Release \
    -o /app/publish

FROM runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
```

Then use Compose to make the project reproducible:

```yaml
services:

  api:
    build: .
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_URLS: http://+:8080

      GitHub__Token: ${GITHUB_TOKEN}

      Microsoft__TenantId: ${MS_TENANT_ID}
      Microsoft__ClientId: ${MS_CLIENT_ID}
      Microsoft__ClientSecret: ${MS_CLIENT_SECRET}

      OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317

    depends_on:
      - otel-collector

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    ports:
      - "4317:4317"
      - "4318:4318"
```

Secrets go into an uncommitted `.env`:

```bash
GITHUB_TOKEN=...
MS_TENANT_ID=...
MS_CLIENT_ID=...
MS_CLIENT_SECRET=...
```

And:

```gitignore
.env
```

Commit only:

```text
.env.example
```

with empty values.

## Your demo becomes extremely simple

Start with:

```bash
git clone ...
cd api-integration-lab

cp .env.example .env

docker compose up --build
```

Then open:

```text
http://localhost:8080/swagger
```

Swagger presents your integration catalogue.

I'd structure it something like:

```text
API Integration Lab
────────────────────────────────────────

Public API
  GET /api/public/posts

Basic Authentication
  GET /api/basic/profile

GitHub
  GET /api/github/profile
  GET /api/github/repos
  GET /api/github/rate-limit

Microsoft OAuth - Delegated
  GET /api/microsoft/login
  GET /api/microsoft/me

Microsoft OAuth - Application
  GET /api/microsoft/users

Diagnostics
  GET /health
```

Now you can execute each request directly in front of your manager.

## Make the demo prove authentication

This is important.

Don't simply execute:

```text
GET GitHub profile
→ 200
```

First deliberately demonstrate failure:

```text
GitHub request

Without token
     ↓
401 Unauthorized

Configure token
     ↓
Authorization: Bearer ********
     ↓
200 OK
     ↓
GitHub JSON
```

Then explain what happened at the HTTP layer:

```http
GET /user HTTP/1.1
Host: api.github.com
Authorization: Bearer ********
Accept: application/vnd.github+json
```

Do the same with OAuth.

For example:

```text
GET /api/microsoft/login

          ↓

Microsoft login

          ↓

Consent

          ↓

Authorization code

          ↓

Your callback

          ↓

POST /token

          ↓

Access token

          ↓

GET graph.microsoft.com/v1.0/me

          ↓

Your API

          ↓

Swagger response
```

That demonstrates you understand the protocol rather than having copied an SDK example.

## Add one thing that makes your demo considerably stronger

Add an endpoint:

```http
GET /api/demo
```

It performs several integrations:

```text
GET /api/demo
       │
       ├── GitHub
       │     └── repositories
       │
       ├── Microsoft Graph
       │     └── profile
       │
       └── Public API
             └── data
       │
       ▼
Aggregate / Normalize
       │
       ▼
Response
```

For example:

```json
{
  "github": {
    "username": "example-user",
    "repositories": 23
  },
  "microsoft": {
    "displayName": "Demo User"
  },
  "publicApi": {
    "status": "available"
  }
}
```

Now you've demonstrated **integration**, not merely individual API calls.

---

## And exploit your observability expertise

This can differentiate your implementation.

Instrument every outbound `HttpClient` request with OpenTelemetry and export it to your existing Grafana Cloud environment or a local tracing backend.

Your trace could show:

```text
GET /api/demo                         642 ms
│
├── GitHub GET /user                 121 ms
│
├── GitHub GET /user/repos           183 ms
│
├── Microsoft POST /oauth2/token      94 ms
│
├── Microsoft GET /v1.0/me           151 ms
│
└── transform response                 3 ms
```

Then show metrics such as:

```text
api_client_requests_total
api_client_request_duration_seconds
api_client_errors_total
api_auth_token_requests_total
```

and logs:

```text
INFO  GitHub request completed status=200 duration=121ms
INFO  OAuth token acquired expires_in=3599
WARN  GitHub rate limit remaining=4
ERROR Graph API returned status=429 retry_after=10
```

**Never log access tokens/client secrets.**

That lets you tell a much better engineering story:

```text
Authentication
      +
API Integration
      +
Resilience
      +
Security
      +
Observability
      =
Production-grade integration
```

## Suggested repository scope

Keep version 1 constrained:

```text
api-integration-lab/
├── src/
├── tests/
├── Dockerfile
├── docker-compose.yml
├── otel-collector.yaml
├── .env.example
├── .gitignore
└── README.md
```

The README should contain an architecture diagram, authentication matrix, local setup, API examples, security considerations and screenshots/traces.

This is enough to turn what could look like a **"can Amit call an API?" exercise** into evidence that you understand **API integration architecture end-to-end**—while keeping the whole thing runnable from one laptop.
