---
title: API Reference
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# API Reference

The API is served at `http://localhost:8080` in Docker Compose. Swagger at `/swagger` is generated
from the same controllers and XML comments; this page adds copyable requests, representative
responses, and cross-endpoint rules.

## Contract Authority and Base URLs

Runtime Swagger/OpenAPI is authoritative for generated schemas and controller annotations. This
checked-in reference is the human learning contract and must be updated in the same change when a
route, bound, authentication rule, or normalized response changes.

The Docker Compose base URL is `http://localhost:8080`; an API-only `dotnet run` uses the URL printed
by ASP.NET Core unless `ASPNETCORE_URLS` is set.

- Request and response bodies use JSON with camel-case property names.
- Validation failures and normalized integration failures use `application/problem+json`.
- Provider credentials are configured on the server. Never paste a PAT, Basic credentials, client
  secret, or Graph token into Swagger.
- All query bounds are inclusive.
- Caller cancellation is propagated; it is not converted into an upstream provider failure.

## Authentication Summary

| Method | Path | Authentication | Success | Purpose |
| --- | --- | --- | ---: | --- |
| `GET` | `/health` | None | `200` | Process liveness |
| `GET` | `/api/public/posts?limit=10` | None | `200` | No-auth REST baseline |
| `GET` | `/api/basic/profile` | Server-owned Basic credentials | `200` | Outbound Basic Auth |
| `GET` | `/api/github/profile` | Server-owned GitHub PAT | `200` | Token identity |
| `GET` | `/api/github/repos?perPage=30&maxPages=3` | Server-owned GitHub PAT | `200` | Bounded pagination |
| `GET` | `/api/github/rate-limit` | Server-owned GitHub PAT | `200` | Normalized rate-limit state |
| `GET` | `/api/microsoft/login?returnUrl=/swagger` | None; starts OIDC | `302` | Delegated sign-in |
| `GET` | `/api/microsoft/logout` | Cookie | `302` | Local and Entra sign-out |
| `GET` | `/api/microsoft/me` | Cookie plus delegated token | `200` | Signed-in Graph profile |
| `GET` | `/api/microsoft/users?top=10` | None; app token is server-owned | `200` | App-only Graph users |
| `POST` | `/api/webhooks/events` | HMAC headers | `202` | Authenticated inbound event |
| `GET` | `/api/demo` | Cookie | `200` or `502` | Concurrent partial-success demo |

Contract details at a glance:

| Path | Normalized success body | Expected errors | Configuration prerequisite |
| --- | --- | --- | --- |
| `/health` | `{ status }` | Process-level `5xx` only | None |
| `/api/public/posts` | `PublicPost[]` | `400`, `429`, `502`, `504` | Internet/provider availability |
| `/api/basic/profile` | `BasicAuthProfile` | `401`, `403`, `429`, `502`, `503`, `504` | Both Basic values |
| `/api/github/profile` | `GitHubProfile` | `401`, `403`, `429`, `502`, `503`, `504` | PAT for success; HTTPS base URL |
| `/api/github/repos` | `GitHubRepositoryPage` | `400`, `401`, `403`, `429`, `502`, `503`, `504` | PAT for success; HTTPS base URL |
| `/api/github/rate-limit` | `GitHubRateLimit` | `401`, `403`, `429`, `502`, `503`, `504` | PAT for success; HTTPS base URL |
| `/api/microsoft/login` | Browser redirect | `302` or `503` | Complete Entra settings |
| `/api/microsoft/logout` | Browser redirect | `302` or `401` | Authenticated cookie/Entra settings |
| `/api/microsoft/me` | `GraphUser` | `401`, `403`, `429`, `502`, `503`, `504` | Cookie, delegated consent, Entra settings |
| `/api/microsoft/users` | `GraphUser[]` | `400`, `401`, `403`, `429`, `502`, `503`, `504` | App permission/admin consent, Entra settings |
| `/api/webhooks/events` | `{ eventType, receivedAt }` | `400`, `401`, `413`, `503` | Shared webhook secret |
| `/api/demo` | Three `IntegrationResult` envelopes | `401` or `502` | Cookie; providers may fail independently |

## Health

### `GET /health`

Checks whether the process can serve HTTP. It deliberately does not validate optional credentials or
external providers, so a GitHub or Microsoft outage does not turn liveness red.

```bash
curl --fail --silent http://localhost:8080/health
```

```json
{
  "status": "Healthy"
}
```

## Public API

### `GET /api/public/posts`

| Query | Default | Allowed | Meaning |
| --- | ---: | ---: | --- |
| `limit` | `10` | `1`–`100` | Maximum normalized posts returned |

```bash
curl --fail --silent 'http://localhost:8080/api/public/posts?limit=2'
```

```json
[
  {
    "id": 1,
    "title": "example title",
    "bodyPreview": "A bounded preview of the provider body"
  }
]
```

This route sends no `Authorization` header. It demonstrates that no-auth still needs bounded input,
DTO validation, normalization, cancellation, resilience, and safe failures.

## Basic Authentication

### `GET /api/basic/profile`

Configure `BASIC_AUTH_USERNAME` and `BASIC_AUTH_PASSWORD`; the API constructs an outbound HTTPS
`Authorization: Basic <base64>` header. The caller never supplies or receives those credentials.

```bash
curl --silent --show-error http://localhost:8080/api/basic/profile
```

```json
{
  "provider": "postman-echo",
  "authenticated": true
}
```

Expected failures:

- `503 configuration` when either server-side value is absent;
- `401 authentication` when the provider rejects the pair;
- `504 timeout` when the resilience budget expires;
- `502 upstream` for other exhausted provider/transport failures.

## GitHub

All GitHub routes use `GITHUB_TOKEN` from server configuration. A fine-grained, read-only PAT is
recommended.

### `GET /api/github/profile`

```bash
curl --silent --show-error http://localhost:8080/api/github/profile
```

```json
{
  "login": "octocat",
  "displayName": "The Octocat",
  "publicRepositoryCount": 8,
  "profileUrl": "https://github.com/octocat"
}
```

Without a token, the API intentionally sends no bearer header and GitHub normally returns `401`.

### `GET /api/github/repos`

| Query | Default | Allowed | Meaning |
| --- | ---: | ---: | --- |
| `perPage` | `30` | `1`–`100` | Provider page size |
| `maxPages` | `3` | `1`–`10` | Hard cap on followed pages |

```bash
curl --fail --silent \
  'http://localhost:8080/api/github/repos?perPage=5&maxPages=2'
```

```json
{
  "repositories": [
    {
      "name": "example",
      "description": "Example repository",
      "isPrivate": false,
      "url": "https://github.com/octocat/example",
      "updatedAt": "2026-09-17T00:00:00+00:00"
    }
  ],
  "pagesFetched": 1,
  "rateLimit": {
    "limit": 5000,
    "remaining": 4999,
    "used": 1,
    "resetAt": "2026-09-17T01:00:00+00:00",
    "resource": "core"
  }
}
```

The client follows only GitHub's `Link` relation `rel="next"`, rejects continuation links for a
different host, and stops at `maxPages`. It does not infer another page from the response size.

### `GET /api/github/rate-limit`

```bash
curl --fail --silent http://localhost:8080/api/github/rate-limit
```

Returns `limit`, `remaining`, `used`, `resetAt`, and `resource` for the core budget. GitHub `403`
with `X-RateLimit-Remaining: 0` and HTTP `429` are normalized as throttling. When available, the API
returns `Retry-After` and `retryAfterSeconds`.

## Microsoft Authentication

Configure the Entra registration described in [authentication.md](../authentication.md). Tokens are
acquired and cached server-side.

### `GET /api/microsoft/login`

Starts the Authorization Code flow. `returnUrl` defaults to `/swagger`; non-local values are ignored
and replaced by `/swagger` to prevent open redirects.

```text
http://localhost:8080/api/microsoft/login?returnUrl=/swagger
```

Expected result: `302` to Microsoft Entra ID. If Microsoft configuration is incomplete, the route
returns `503 configuration` instead of invoking an unregistered OIDC scheme.

## Microsoft Graph

### `GET /api/microsoft/me`

Requires the encrypted local authentication cookie created after login. The server acquires a
delegated `User.Read` token and calls Graph `/me`.

```json
{
  "id": "provider-object-id",
  "displayName": "Demo User",
  "mail": "demo@example.invalid",
  "userPrincipalName": "demo@example.invalid"
}
```

An unauthenticated API request returns `401`, not a browser redirect. This keeps the HTTP API
contract distinct from the explicit login route.

### `GET /api/microsoft/users`

| Query | Default | Allowed | Meaning |
| --- | ---: | ---: | --- |
| `top` | `10` | `1`–`50` | Maximum users requested from Graph |

This anonymous inbound route uses server-side Client Credentials and Graph application permission
`User.Read.All`; it never borrows the signed-in user's token.

```bash
curl --silent --show-error \
  'http://localhost:8080/api/microsoft/users?top=5'
```

### `GET /api/microsoft/logout`

Requires the local cookie. It signs out both the cookie scheme and OpenID Connect provider, then
returns the browser to `/swagger`.

## HMAC Webhook

### `POST /api/webhooks/events`

| Header | Required | Format |
| --- | --- | --- |
| `Content-Type` | Yes | `application/json` |
| `X-Webhook-Timestamp` | Yes | Unix seconds, within ±5 minutes |
| `X-Webhook-Signature-256` | Yes | `sha256=` plus 64 lowercase hex characters |

The signed input is the UTF-8 timestamp, one literal dot, and the exact request body bytes. The body
limit is 65,536 bytes.

```bash
body='{"eventType":"demo.created","value":1}'
timestamp="$(date +%s)"
digest="$(printf '%s.%s' "$timestamp" "$body" \
  | openssl dgst -sha256 -hmac "$WEBHOOK_SECRET" \
  | awk '{print $NF}')"

curl --request POST http://localhost:8080/api/webhooks/events \
  --header 'Content-Type: application/json' \
  --header "X-Webhook-Timestamp: $timestamp" \
  --header "X-Webhook-Signature-256: sha256=$digest" \
  --data-binary "$body"
```

Success is `202 Accepted`:

```json
{
  "eventType": "demo.created",
  "receivedAt": "2026-09-17T00:00:00+00:00"
}
```

The same accepted signature cannot be used twice. Generate a new timestamp/signature pair for a new
delivery. See [authentication.md](../authentication.md#inbound-hmac-sha256) for why
verification happens before JSON parsing.

## Aggregate Demo

### `GET /api/demo`

Requires delegated sign-in. It starts public posts, one GitHub repository page, and Graph `/me`
concurrently. Each provider gets an independent envelope:

```json
{
  "publicApi": {
    "status": "success",
    "data": [],
    "error": null
  },
  "gitHub": {
    "status": "failed",
    "data": null,
    "error": {
      "provider": "github",
      "category": "authentication",
      "message": "GitHub returned HTTP 401."
    }
  },
  "microsoft": {
    "status": "success",
    "data": {
      "id": "provider-object-id",
      "displayName": "Demo User",
      "mail": null,
      "userPrincipalName": "demo@example.invalid"
    },
    "error": null
  }
}
```

HTTP `200` means at least one provider succeeded; inspect every envelope. HTTP `502` means all three
known integration calls failed. Unexpected programming errors are not converted into envelopes.

## Common Error Contract

Normalized integration failures have this shape:

```json
{
  "type": "about:blank",
  "title": "Integration request failed",
  "status": 429,
  "detail": "GitHub returned HTTP 429.",
  "instance": "/api/github/profile",
  "provider": "github",
  "category": "throttled",
  "traceId": "0123456789abcdef0123456789abcdef",
  "retryAfterSeconds": 30
}
```

`retryAfterSeconds` and the HTTP `Retry-After` header appear only when a safe delay is known.

| Category | HTTP | Caller interpretation |
| --- | ---: | --- |
| `validation` | `400` | Correct the bounded input |
| `authentication` | `401` | Identity or credential was not accepted |
| `authorization` | `403` | Identity is known but lacks permission |
| `throttled` | `429` | Respect the returned delay when present |
| `configuration` | `503` | Configure the optional server-side integration |
| `timeout` | `504` | The bounded outbound time budget expired |
| `upstream` | `502` | Provider, payload, circuit, or exhausted transport failure |

ASP.NET model-validation Problem Details may have a framework-generated title and `errors` object;
the status remains `400`.

## Pagination and Rate-Limit Semantics

GitHub repository pagination follows only an HTTPS, same-host `rel="next"` link and never exceeds
`maxPages`. Rate limits are normalized from provider headers or `/rate_limit`. A throttled response
preserves a safe `Retry-After` delay in both the response header and optional Problem Details
`retryAfterSeconds`; absence of a delay is not permission to retry in a tight loop.

## Runtime Swagger

Open <http://localhost:8080/swagger> for interactive descriptions and
<http://localhost:8080/swagger/v1/swagger.json> for the generated contract. Swagger does not accept
external provider credentials: configure the server, then invoke the route. Microsoft login/logout
are browser redirect flows and should be completed in the browser rather than simulated as a single
API call.
