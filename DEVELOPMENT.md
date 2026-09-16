---
title: Development Guide
owner: Amit Singh
scope: lab
last_reviewed: 2026-09-17
status: active
---

# Development Guide

This guide covers the repeatable local development loop. For a first conceptual tour, use the
[learning guide](docs/learning-guide.md); for runtime diagnosis, use the
[runbook](docs/operations/runbook.md).

## Toolchain

| Tool | Required version or capability | Used for |
| --- | --- | --- |
| .NET SDK | `8.0.131`, with `latestFeature` roll-forward | Restore, build, run, and test |
| Docker | Current supported engine | Reproducible API image |
| Docker Compose | v2 (`docker compose`) | API, Collector, and LGTM stack |
| Bash | Bash-compatible shell | Smoke test |
| `curl` | Any current release | HTTP and health checks |
| OpenSSL | SHA-256/HMAC support | Signed webhook requests |
| ShellCheck | Any current release | Shell static analysis |

Confirm the pinned SDK is selected:

```bash
dotnet --version
```

Expected output starts with `8.0.131`. A later .NET 8 feature band may be selected only because
`global.json` explicitly allows `latestFeature`; do not silently build with .NET 9 or later.

## First-Time Setup

```bash
git clone <repository-url>
cd api-integration-lab
cp .env.example .env
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build
```

All values in `.env` may remain empty for health, Swagger, public API, validation, and expected
failure-path work. Fill only the integration being exercised. `.env` is ignored; `.env.example`
must contain names and guidance but no usable secrets.

## Fast API-Only Loop

Run the API directly when changing controllers, clients, contracts, or tests:

```bash
dotnet run --project src/ApiIntegrationLab.Api
```

The launch URL printed by ASP.NET Core is authoritative. If an explicit local port is useful:

```bash
ASPNETCORE_URLS=http://localhost:8080 \
  dotnet run --project src/ApiIntegrationLab.Api
```

Open Swagger at <http://localhost:8080/swagger> and health at <http://localhost:8080/health>.
Without an OTLP endpoint, telemetry export may log connection failures; this does not change the API
contract and is expected in the API-only loop.

## Full Local Stack

Start the application, standalone OpenTelemetry Collector, and LGTM backend:

```bash
docker compose --env-file .env up --build --detach
docker compose ps
docker compose logs --tail=100 api otel-collector lgtm
```

| Surface | URL | Expected result |
| --- | --- | --- |
| API health | <http://localhost:8080/health> | HTTP `200` |
| Swagger UI | <http://localhost:8080/swagger> | Interactive endpoint guidance |
| OpenAPI JSON | <http://localhost:8080/swagger/v1/swagger.json> | Generated API contract |
| Grafana | <http://localhost:3000> | Local Explore UI |

Run safe live checks:

```bash
set -a
source .env
set +a
scripts/smoke-test.sh
```

The script prints check names and results, not response bodies. Credentialed checks are skipped
unless their variables are exported. Stop the stack without deleting its recoverable volumes:

```bash
docker compose down
```

## Corporate CA Setup

If TLS interception causes NuGet `NU1301` or certificate-chain errors during image restore, expose
the trusted host bundle only to the BuildKit restore layer:

```bash
docker build \
  --secret id=ca_bundle,src=/etc/ssl/certs/ca-certificates.crt \
  --tag api-integration-lab:local .

docker compose \
  --file docker-compose.yml \
  --file docker-compose.corporate-ca.yml \
  --env-file .env \
  up --detach --no-build
```

Do not copy a corporate CA, proxy credential, or package credential into the image or repository.
The Dockerfile's secret mount exists only during the restore layer.

## Provider Configuration

| Integration | Variables | External setup |
| --- | --- | --- |
| Basic Auth | `BASIC_AUTH_USERNAME`, `BASIC_AUTH_PASSWORD` | Synthetic local values; do not reuse a real password |
| GitHub | `GITHUB_TOKEN` | Fine-grained PAT with only required read access |
| Microsoft | `MS_TENANT_ID`, `MS_CLIENT_ID`, `MS_CLIENT_SECRET` | Redirect URI, delegated `User.Read`, application `User.Read.All`, admin consent |
| HMAC webhook | `WEBHOOK_SECRET` | Same locally generated value in sender and API |

Microsoft redirect URI: `http://localhost:8080/auth/microsoft/callback`.

Restart the API after changing Microsoft settings. `Program.cs` decides at startup whether to
register the OpenID Connect handler or the intentionally unconfigured fallback.

## Build and Test Commands

```bash
# Restore versions declared by each project and resolve their transitive dependencies.
dotnet restore ApiIntegrationLab.sln

# Compile without repeating restore.
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore

# Complete automated suite.
dotnet test ApiIntegrationLab.sln --configuration Release --no-build

# Focused projects.
dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj \
  --configuration Release --no-build
dotnet test tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj \
  --configuration Release --no-build

# Formatting and shell checks.
dotnet format ApiIntegrationLab.sln --verify-no-changes --no-restore
bash -n scripts/smoke-test.sh
shellcheck scripts/smoke-test.sh
git diff --check
```

The integration tests use `WebApplicationFactory<Program>`; they do not need live provider
credentials or Docker. Provider HTTP behavior is replaced with controlled clients or handlers.

## Debugging Entry Points

| Symptom or question | Start here |
| --- | --- |
| Route, binding, or status code | Relevant `*Controller.cs` and integration endpoint test |
| Header or bearer credential not sent | `*AuthenticationHandler.cs` and its unit test |
| Provider payload rejected | Provider client, internal DTO, and payload test |
| Retry/timeout behavior | `Common/Http/HttpClientRegistrationExtensions.cs` |
| Error status or Problem Details field | `Common/Errors/IntegrationExceptionHandler.cs` |
| Missing trace correlation | `Common/Telemetry/TraceContextMiddleware.cs` |
| Metric name or tags | `Common/Telemetry/ApiTelemetry.cs` |
| OAuth handler selection | `Program.cs` and `MicrosoftAuthenticationRoutingTests.cs` |
| HMAC mismatch or replay | `WebhookSignatureVerifier.cs` and verifier tests |
| `/api/demo` partial result | `DemoAggregator.cs` and aggregator tests |
| Collector/backend delivery | `otel-collector.yaml` and Compose logs |

Use the trace ID returned in Problem Details to correlate an HTTP failure with API logs and traces.
Never enable shell tracing or print configuration objects while investigating credentials.

## Common Failures

### `NU1301` or certificate errors during restore

- Confirm general network/DNS reachability to NuGet.
- Behind TLS interception, use the ephemeral CA-secret build shown above.
- Do not disable TLS validation or commit a CA bundle as a workaround.

### API starts but a provider returns `503 configuration`

- This is expected when that provider's optional settings are empty.
- Compare `.env` variable names with `.env.example` and Compose mappings.
- Restart the API after editing `.env`; Compose does not mutate a running container's environment.

### GitHub returns `401` or `403`

- Check PAT expiry, resource owner, and minimum read permission.
- Do not paste the token into Swagger; the server owns and injects it.
- A `403` may represent authorization or rate-limit state; inspect safe Problem Details and headers.

### Microsoft login does not redirect

- All three Microsoft variables must be non-empty at startup.
- Confirm the redirect URI exactly matches the app registration.
- Use the client-secret value, not the portal object ID.

### `/api/microsoft/users` returns `403`

- Application `User.Read.All` differs from delegated `User.Read`.
- Confirm tenant admin consent for the application permission.

### Signed webhook returns `401`

- Sign `<unix-seconds>.<exact-body-bytes>` and transmit the same bytes with `--data-binary`.
- Use lowercase 64-character hex, optionally prefixed by `sha256=`.
- Keep timestamp skew within five minutes and generate a new signature for every retry; an accepted
  digest cannot be replayed.

### Grafana has no recent telemetry

- Generate API traffic after the stack is ready.
- Check `docker compose logs api otel-collector lgtm` in that order.
- Verify the API endpoint is `http://otel-collector:4317` with protocol `grpc` inside Compose.
- See [docs/observability.md](docs/observability.md) for signal names and Explore queries.

## Before Opening a Review

Follow the complete checklist in [CONTRIBUTING.md](CONTRIBUTING.md), update affected documentation,
and record live provider checks that could not run because credentials were intentionally absent.
