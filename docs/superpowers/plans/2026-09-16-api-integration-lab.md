# API Integration Lab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a locally reproducible .NET 8 API integration showcase implementing every
authentication, resilience, security, aggregation, and observability requirement in the approved
API Integration Lab design.

**Architecture:** A single ASP.NET Core API is organized into feature folders containing
controllers, normalized models, and typed clients. Shared error, HTTP resilience, configuration, and
telemetry components provide bounded cross-cutting behavior; Docker Compose sends OTLP through a
standalone collector to a local Grafana LGTM backend.

**Tech Stack:** .NET 8, ASP.NET Core controllers, Microsoft.Identity.Web 4.14.2,
Microsoft.Extensions.Http.Resilience 10.10.0, OpenTelemetry .NET 1.18.0, Swashbuckle.AspNetCore
10.2.3, xUnit, Docker Compose, OpenTelemetry Collector, Grafana LGTM.

**Spec:** `docs/superpowers/specs/2026-09-16-api-integration-lab-design.md`

## Global Constraints

- Target `net8.0`; keep the repository runnable with the installed .NET 8 SDK.
- All external calls use typed `HttpClient` instances with finite timeouts and cancellation.
- GitHub and Graph collection requests are explicitly bounded.
- Secrets come from environment-backed configuration and never appear in source, responses, logs,
  metrics, or traces.
- Missing optional credentials must not prevent application startup.
- Metric labels are restricted to the enumerated provider, operation, outcome, error type, and
  auth-flow values in the spec.
- Inline comments explain authentication, retry, pagination, redaction, and signature decisions at
  their implementation points; routine syntax is not narrated.
- Automated tests never call live external APIs and never require credentials.
- Do not commit during execution unless Amit explicitly requests a commit. Each task ends with a
  diff-review checkpoint instead.

## File Map

| Area                                                      | Responsibility                                                                                           |
| --------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `ApiIntegrationLab.sln`                                   | Solution containing API and both test projects.                                                          |
| `src/ApiIntegrationLab.Api/Program.cs`                    | Composition root and middleware ordering only.                                                           |
| `src/ApiIntegrationLab.Api/Common/`                       | Typed failures, exception mapping, resilience, shared results, telemetry, and testable time abstraction. |
| `src/ApiIntegrationLab.Api/Integrations/<Provider>/`      | Provider-specific options, DTOs, normalized models, client, and controller.                              |
| `src/ApiIntegrationLab.Api/Authentication/Microsoft/`     | Delegated/app token acquisition adapter and sign-in/sign-out controller.                                 |
| `src/ApiIntegrationLab.Api/Authentication/Webhooks/`      | HMAC verification and webhook controller.                                                                |
| `tests/ApiIntegrationLab.UnitTests/`                      | Pure and mocked-handler tests for provider/auth behavior.                                                |
| `tests/ApiIntegrationLab.IntegrationTests/`               | Full host routing, middleware, Swagger, health, and problem-response tests.                              |
| `Dockerfile`, `docker-compose.yml`, `otel-collector.yaml` | Reproducible local application and observability stack.                                                  |
| `scripts/smoke-test.sh`                                   | Repeatable live local verification without exposing secrets.                                             |
| `README.md`                                               | Manager-facing demo, setup, auth matrix, security, and telemetry evidence.                               |

---

### Task 1: Scaffold the solution and executable test hosts

**Files:**

- Create: `ApiIntegrationLab.sln`
- Create: `global.json`
- Create: `src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj`
- Create: `src/ApiIntegrationLab.Api/Program.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Health/HealthController.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj`
- Create: `tests/ApiIntegrationLab.UnitTests/TestDoubles/StubHttpMessageHandler.cs`
- Create: `tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj`
- Create: `tests/ApiIntegrationLab.IntegrationTests/ApplicationFactory.cs`
- Create: `tests/ApiIntegrationLab.IntegrationTests/HealthEndpointTests.cs`
- Modify: `.gitignore`

**Interfaces:**

- Produces: an executable `Program` visible to `WebApplicationFactory<Program>` through
  `public partial class Program`.
- Produces: `GET /health` returning HTTP 200 with `{ "status": "Healthy" }`.

- [ ] **Step 1: Generate solution and projects**

Run:

```bash
dotnet new sln -n ApiIntegrationLab
dotnet new webapi -n ApiIntegrationLab.Api -o src/ApiIntegrationLab.Api --framework net8.0 --use-controllers --no-https
dotnet new xunit -n ApiIntegrationLab.UnitTests -o tests/ApiIntegrationLab.UnitTests --framework net8.0
dotnet new xunit -n ApiIntegrationLab.IntegrationTests -o tests/ApiIntegrationLab.IntegrationTests --framework net8.0
dotnet sln add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj
dotnet sln add tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj
dotnet sln add tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj
dotnet add tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj reference src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj
dotnet add tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj reference src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj
dotnet add tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 8.0.31
```

Delete the generated WeatherForecast controller/model and replace the generated `Program.cs` with
the minimal API host required below.

- [ ] **Step 2: Write the failing host test**

```csharp
public sealed class HealthEndpointTests : IClassFixture<ApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_healthy_without_external_credentials()
    {
        using var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 3: Run the focused test and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj --filter Health_returns_healthy_without_external_credentials`

Expected: FAIL because `/health` and `ApplicationFactory` are not implemented.

- [ ] **Step 4: Add the minimal host and test factory**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();

public partial class Program;
```

Create a controller-owned health route so Swagger discovers the same endpoint Docker probes:

```csharp
[ApiController]
public sealed class HealthController : ControllerBase
{
    /// <summary>Reports whether the API process can serve requests.</summary>
    /// <remarks>External provider credentials are intentionally excluded from liveness.</remarks>
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "Healthy" });
}
```

```csharp
public sealed class ApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing");
}
```

Add the reusable unit-test HTTP double used by later tasks:

```csharp
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;

    public int CallCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return _send(request, cancellationToken);
    }

    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));

    public static StubHttpMessageHandler Capture(HttpStatusCode status, string json) =>
        Json(json, status);

    public static StubHttpMessageHandler Unused() =>
        new((_, _) => throw new Xunit.Sdk.XunitException("The HTTP handler must not be called."));
}
```

Provider test files may add narrowly scoped factory methods beside their tests, but must build on
this handler rather than introducing a mocking framework.

Create `global.json` with SDK `8.0.131` and `rollForward` set to `latestFeature`. Extend
`.gitignore` with `.env`, `bin/`, `obj/`, TestResults, IDE state, and local telemetry data
directories.

- [ ] **Step 5: Verify the scaffold**

Run: `dotnet test ApiIntegrationLab.sln`

Expected: PASS, with the health endpoint succeeding when no provider credentials exist.

- [ ] **Step 6: Review checkpoint**

Run: `git diff --check && git status --short`

Confirm only scaffold files, `.gitignore`, and the approved spec/plan are present. Do not commit.

### Task 2: Add shared configuration, failures, HTTP resilience, and telemetry contracts

**Files:**

- Create: `src/ApiIntegrationLab.Api/Common/Configuration/OptionsValidator.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Errors/IntegrationErrorCategory.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Errors/IntegrationException.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Errors/IntegrationExceptionHandler.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Http/HttpClientRegistrationExtensions.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Models/IntegrationResult.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Telemetry/ApiTelemetry.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Time/ISystemClock.cs`
- Create: `src/ApiIntegrationLab.Api/Common/Time/SystemClock.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Common/OptionsValidatorTests.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Common/IntegrationExceptionTests.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Common/ApiTelemetryTests.cs`
- Modify: `src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`

**Interfaces:**

- Produces:
  `OptionsValidator.Require(string provider, params (string Key, string? Value)[] values)`.
- Produces: `IntegrationException` carrying provider, bounded category, suggested HTTP status, and
  optional retry delay.
- Produces: `IntegrationResult<T>(string Status, T? Data, IntegrationError? Error)` with `Success`
  and `Failure` factories.
- Produces: `ApiTelemetry.MeasureRequest(string provider, string operation)` returning a disposable
  outcome recorder.
- Produces: `ISystemClock.UtcNow` for deterministic replay tests.

- [ ] **Step 1: Add runtime packages**

Run:

```bash
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package Microsoft.Extensions.Http.Resilience --version 10.10.0
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package Swashbuckle.AspNetCore --version 10.2.3
```

- [ ] **Step 2: Write failing common-behavior tests**

```csharp
[Fact]
public void Require_lists_missing_keys_without_values()
{
    var error = Assert.Throws<IntegrationException>(() =>
        OptionsValidator.Require("github", ("GitHub:Token", null), ("GitHub:UserAgent", "lab")));

    Assert.Equal(IntegrationErrorCategory.Configuration, error.Category);
    Assert.Contains("GitHub:Token", error.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("lab", error.Message, StringComparison.Ordinal);
}

[Theory]
[InlineData(IntegrationErrorCategory.Authentication, 401)]
[InlineData(IntegrationErrorCategory.Throttled, 429)]
[InlineData(IntegrationErrorCategory.Timeout, 504)]
[InlineData(IntegrationErrorCategory.Configuration, 503)]
[InlineData(IntegrationErrorCategory.Upstream, 502)]
public void Error_categories_have_stable_http_mapping(IntegrationErrorCategory category, int status)
{
    Assert.Equal(status, IntegrationException.StatusFor(category));
}
```

Add a telemetry test using `MeterListener` that records one request and proves only documented label
keys are emitted: `provider`, `operation`, `outcome`, and `error.type`.

- [ ] **Step 3: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter "OptionsValidatorTests|IntegrationExceptionTests|ApiTelemetryTests"`

Expected: FAIL because the shared contracts do not exist.

- [ ] **Step 4: Implement the shared contracts**

Use these stable public shapes:

```csharp
public enum IntegrationErrorCategory
{
    Authentication,
    Authorization,
    Configuration,
    Throttled,
    Timeout,
    Upstream,
    Validation
}

public sealed class IntegrationException : Exception
{
    public string Provider { get; }
    public IntegrationErrorCategory Category { get; }
    public TimeSpan? RetryAfter { get; }
    public int StatusCode => StatusFor(Category);

    public IntegrationException(
        string provider,
        IntegrationErrorCategory category,
        string safeMessage,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        Provider = provider;
        Category = category;
        RetryAfter = retryAfter;
    }

    public static int StatusFor(IntegrationErrorCategory category) => category switch
    {
        IntegrationErrorCategory.Authentication => StatusCodes.Status401Unauthorized,
        IntegrationErrorCategory.Authorization => StatusCodes.Status403Forbidden,
        IntegrationErrorCategory.Throttled => StatusCodes.Status429TooManyRequests,
        IntegrationErrorCategory.Timeout => StatusCodes.Status504GatewayTimeout,
        IntegrationErrorCategory.Configuration => StatusCodes.Status503ServiceUnavailable,
        IntegrationErrorCategory.Validation => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status502BadGateway
    };
}

public sealed record IntegrationError(string Provider, string Category, string Message);
public sealed record IntegrationResult<T>(string Status, T? Data, IntegrationError? Error)
{
    public static IntegrationResult<T> Success(T data) => new("success", data, null);

    public static IntegrationResult<T> Failure(IntegrationException error) =>
        new("failed", default, new(
            error.Provider,
            error.Category.ToString().ToLowerInvariant(),
            error.Message));
}

public interface IIntegrationRequestMeasurement : IDisposable
{
    void Succeed();
    void Fail(string boundedErrorType);
}
```

`IntegrationExceptionHandler` must write RFC 7807 JSON containing `provider`, lowercase `category`,
`traceId`, and `retryAfterSeconds` only when present. The detail must be an owned safe message,
never the upstream response body.

`AddIntegrationHttpClient<TClient,TImplementation>` configures a base URI, a 15-second total
timeout, two retry attempts with exponential backoff/jitter, and retry predicates limited to
timeout, 408, 429, and 5xx outcomes. Add an inline comment explaining why auth and validation
failures are excluded.

`ApiTelemetry` owns `ActivitySource("ApiIntegrationLab")` and `Meter("ApiIntegrationLab")` with
instruments `api.client.requests`, `api.client.request.duration`, `api.client.errors`, and
`api.auth.token.requests`. `MeasureRequest(string provider, string operation)` returns
`IIntegrationRequestMeasurement`; `RecordTokenRequest(string flow, string outcome)` records token
acquisition. Both methods validate values against internal bounded sets before recording.

- [ ] **Step 5: Wire global error handling and shared services**

Register `IExceptionHandler`, Problem Details, `ISystemClock`, and `ApiTelemetry`; place
`UseExceptionHandler()` before controller mappings. Do not register provider clients yet.

- [ ] **Step 6: Verify common behavior**

Run: `dotnet test ApiIntegrationLab.sln`

Expected: PASS; telemetry tests prove the bounded label contract and no test value becomes a label.

- [ ] **Step 7: Review checkpoint**

Run: `git diff --check && git status --short`

Inspect exception details and telemetry tags for accidental secret or unbounded values. Do not
commit.

### Task 3: Implement the unauthenticated public API integration

**Files:**

- Create: `src/ApiIntegrationLab.Api/Integrations/PublicApi/PublicApiOptions.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/PublicApi/PublicPostDto.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/PublicApi/PublicPost.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/PublicApi/IPublicApiClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/PublicApi/PublicApiClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/PublicApi/PublicApiController.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/Integrations/PublicApiClientTestFactory.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/PublicApiClientTests.cs`
- Test: `tests/ApiIntegrationLab.IntegrationTests/PublicApiEndpointTests.cs`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: `src/ApiIntegrationLab.Api/appsettings.json`

**Interfaces:**

- Produces:
  `Task<IReadOnlyList<PublicPost>> IPublicApiClient.GetPostsAsync(int limit, CancellationToken cancellationToken)`.
- Produces: `GET /api/public/posts?limit=10`, accepting limits 1–100.
- Test support: `PublicApiClientTestFactory.Create(StubHttpMessageHandler handler)` constructs the
  production client with in-memory options and `ApiTelemetry`.

- [ ] **Step 1: Write failing client tests**

```csharp
[Fact]
public async Task GetPosts_maps_and_limits_provider_payload()
{
    var handler = StubHttpMessageHandler.Json("""
        [{"userId":1,"id":10,"title":"A title","body":"A body"},
         {"userId":1,"id":11,"title":"Second","body":"Second body"}]
        """);
    var client = PublicApiClientTestFactory.Create(handler);

    var posts = await client.GetPostsAsync(1, CancellationToken.None);

    var post = Assert.Single(posts);
    Assert.Equal(10, post.Id);
    Assert.Equal("A title", post.Title);
}

[Theory]
[InlineData(0)]
[InlineData(101)]
public async Task GetPosts_rejects_unbounded_limits(int limit)
{
    var client = PublicApiClientTestFactory.Create(StubHttpMessageHandler.Unused());
    await Assert.ThrowsAsync<IntegrationException>(() =>
        client.GetPostsAsync(limit, CancellationToken.None));
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter PublicApiClientTests`

Expected: FAIL because the public client and models do not exist.

- [ ] **Step 3: Implement the client and normalized model**

```csharp
public sealed record PublicPost(int Id, string Title, string BodyPreview);

public interface IPublicApiClient
{
    Task<IReadOnlyList<PublicPost>> GetPostsAsync(
        int limit,
        CancellationToken cancellationToken);
}
```

Configure the base URL as `https://jsonplaceholder.typicode.com/`. Request `posts`, deserialize with
web defaults, take exactly `limit`, and abbreviate body text deterministically. Record success/error
through `ApiTelemetry` without tagging post IDs or titles.

- [ ] **Step 4: Write and implement the endpoint test**

The integration test replaces `IPublicApiClient` with a fake, calls `/api/public/posts?limit=1`, and
asserts HTTP 200 plus the normalized schema. Add a second test asserting HTTP 400 Problem Details
for `limit=0`. Implement an `[ApiController]` route with XML summary and remarks explaining that
this is the no-auth baseline.

- [ ] **Step 5: Verify the public integration**

Run: `dotnet test ApiIntegrationLab.sln --filter "PublicApiClientTests|PublicApiEndpointTests"`

Expected: PASS.

- [ ] **Step 6: Review checkpoint**

Run: `git diff --check && git status --short`

Confirm the endpoint has no auth header, uses the shared resilience pipeline, and has no
high-cardinality telemetry. Do not commit.

### Task 4: Implement outbound Basic Authentication

**Files:**

- Create: `src/ApiIntegrationLab.Api/Integrations/BasicAuth/BasicAuthOptions.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/BasicAuth/BasicAuthProfile.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/BasicAuth/IBasicAuthClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/BasicAuth/BasicAuthenticationHandler.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/BasicAuth/BasicAuthClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/BasicAuth/BasicAuthController.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/BasicAuthenticationHandlerTests.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/BasicAuthClientTests.cs`
- Test: `tests/ApiIntegrationLab.IntegrationTests/BasicAuthEndpointTests.cs`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: `src/ApiIntegrationLab.Api/appsettings.json`

**Interfaces:**

- Produces:
  `Task<BasicAuthProfile> IBasicAuthClient.GetProfileAsync(CancellationToken cancellationToken)`.
- Produces: `GET /api/basic/profile`.
- Test support: create the `BasicAuthenticationHandler` directly with `IOptions<BasicAuthOptions>`
  and the shared stub handler; `BasicAuthClientTests` uses the same direct-construction pattern.

- [ ] **Step 1: Write failing header and configuration tests**

```csharp
[Fact]
public async Task Handler_sends_expected_basic_authorization_header()
{
    var capture = StubHttpMessageHandler.Capture(HttpStatusCode.OK, "{\"authenticated\":true}");
    var handler = new BasicAuthenticationHandler(
        Options.Create(new BasicAuthOptions { Username = "demo", Password = "s3cret" }))
    { InnerHandler = capture };
    using var client = new HttpClient(handler);

    await client.GetAsync("https://postman-echo.com/basic-auth");

    Assert.Equal("Basic", capture.LastRequest!.Headers.Authorization!.Scheme);
    Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("demo:s3cret")),
        capture.LastRequest.Headers.Authorization.Parameter);
}

[Fact]
public async Task Handler_rejects_missing_password_before_network_call()
{
    var handler = new BasicAuthenticationHandler(
        Options.Create(new BasicAuthOptions { Username = "demo", Password = "" }))
    { InnerHandler = StubHttpMessageHandler.Unused() };

    await Assert.ThrowsAsync<IntegrationException>(() =>
        new HttpClient(handler).GetAsync("https://postman-echo.com/basic-auth"));
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter "BasicAuthenticationHandlerTests|BasicAuthClientTests"`

Expected: FAIL because the Basic Auth feature does not exist.

- [ ] **Step 3: Implement Basic Auth without leaking credentials**

```csharp
public sealed class BasicAuthOptions
{
    public const string SectionName = "BasicAuth";
    public string BaseUrl { get; init; } = "https://postman-echo.com/";
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public sealed record BasicAuthProfile(string Provider, bool Authenticated);
```

The delegating handler calls `OptionsValidator.Require`, creates the header per request, and never
stores the Base64 value outside the request. Place an inline comment at header construction stating
that Base64 is reversible and HTTPS supplies confidentiality. `BasicAuthClient` calls `basic-auth`,
requires a successful response, maps `{ "authenticated": true }`, and never returns the username.

- [ ] **Step 4: Add endpoint behavior and tests**

Implement the controller with XML remarks covering configured credentials, HTTPS, and expected 401
behavior. Integration tests assert HTTP 200 with a fake client and HTTP 503 Problem Details when
options are absent; assert the serialized response contains neither username nor password.

- [ ] **Step 5: Verify the Basic integration**

Run: `dotnet test ApiIntegrationLab.sln --filter "BasicAuth"`

Expected: PASS.

- [ ] **Step 6: Review checkpoint**

Run: `git diff --check && rg -n "s3cret|Authorization.*Log|Password.*Log" src tests`

The only literal test password may appear inside its isolated test. No production log template or
response model may contain credentials or authorization headers. Do not commit.

### Task 5: Implement GitHub bearer authentication, pagination, and rate limits

**Files:**

- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubOptions.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubAuthenticationHandler.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubDtos.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubModels.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/IGitHubClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubLinkParser.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/GitHub/GitHubController.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/Integrations/GitHubTestFactory.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/GitHubAuthenticationHandlerTests.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/GitHubPaginationTests.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/GitHubRateLimitTests.cs`
- Test: `tests/ApiIntegrationLab.IntegrationTests/GitHubEndpointTests.cs`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: `src/ApiIntegrationLab.Api/appsettings.json`

**Interfaces:**

- Produces: `Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken)`.
- Produces:
  `Task<GitHubRepositoryPage> GetRepositoriesAsync(int perPage, int maxPages, CancellationToken cancellationToken)`.
- Produces: `Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken)`.
- Produces: `/api/github/profile`, `/api/github/repos`, and `/api/github/rate-limit`.
- Test support: `GitHubTestFactory.CreateAuthHandler(string? token, HttpMessageHandler inner)`,
  `CreateClient(StubHttpMessageHandler handler)`, `CreateClientWithToken(string token)`, and
  `TwoRepositoryPages()` construct deterministic production objects and response sequences. The
  token-bearing factory returns a real client backed by a success stub so telemetry tests never make
  a live request.

- [ ] **Step 1: Write failing authentication and pagination tests**

```csharp
[Fact]
public async Task Handler_omits_authorization_when_token_is_absent()
{
    var capture = StubHttpMessageHandler.Capture(HttpStatusCode.Unauthorized, "{}");
    var handler = GitHubTestFactory.CreateAuthHandler(token: null, capture);

    await new HttpClient(handler).GetAsync("https://api.github.com/user");

    Assert.Null(capture.LastRequest!.Headers.Authorization);
    Assert.True(capture.LastRequest.Headers.UserAgent.Count > 0);
    Assert.True(capture.LastRequest.Headers.Contains("X-GitHub-Api-Version"));
}

[Fact]
public async Task Repositories_follow_next_link_until_max_pages()
{
    var handler = GitHubTestFactory.TwoRepositoryPages();
    var client = GitHubTestFactory.CreateClient(handler);

    var result = await client.GetRepositoriesAsync(perPage: 1, maxPages: 2, CancellationToken.None);

    Assert.Equal(2, result.Repositories.Count);
    Assert.Equal(2, result.PagesFetched);
    Assert.Equal(2, handler.CallCount);
}
```

Add tests proving no `Link` header stops immediately, `maxPages=1` ignores a next link, invalid
bounds (`perPage` outside 1–100 or `maxPages` outside 1–10) fail before HTTP, and cancellation stops
the loop.

- [ ] **Step 2: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter "GitHubAuthenticationHandlerTests|GitHubPaginationTests|GitHubRateLimitTests"`

Expected: FAIL because the GitHub feature does not exist.

- [ ] **Step 3: Implement stable GitHub contracts**

```csharp
public interface IGitHubClient
{
    Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken);
    Task<GitHubRepositoryPage> GetRepositoriesAsync(
        int perPage,
        int maxPages,
        CancellationToken cancellationToken);
    Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken);
}

public sealed record GitHubProfile(
    string Login,
    string? DisplayName,
    int PublicRepositoryCount,
    Uri ProfileUrl);

public sealed record GitHubRepository(
    string Name,
    string? Description,
    bool IsPrivate,
    Uri Url,
    DateTimeOffset UpdatedAt);

public sealed record GitHubRepositoryPage(
    IReadOnlyList<GitHubRepository> Repositories,
    int PagesFetched,
    GitHubRateLimit RateLimit);

public sealed record GitHubRateLimit(
    int Limit,
    int Remaining,
    int Used,
    DateTimeOffset ResetAt,
    string Resource);
```

The handler always sends GitHub's media type, a nonempty configurable user agent, and API version
`2022-11-28`; it sends `Authorization: Bearer` only when a token exists. Comments explain why GitHub
requires a user agent and why the tokenless request is intentional for the failure demonstration.

Parse pagination only from a syntactically valid `rel="next"` link with an HTTPS GitHub API host.
This host check prevents a malicious upstream link from forwarding the bearer token elsewhere.
Comments explain why item-count inference is unsafe. Parse rate headers after every response.

Map upstream 401, 403 authorization, 403 rate-limit, 429, timeout, and invalid payloads into the
shared typed errors without copying the upstream body.

- [ ] **Step 4: Add controller and integration tests**

Test all three routes, query bounds, RFC 7807 auth failure, and `Retry-After` on throttling. XML
remarks must show the no-token 401 → configured-token 200 demonstration and explain pagination caps.

- [ ] **Step 5: Verify the GitHub integration**

Run: `dotnet test ApiIntegrationLab.sln --filter "GitHub"`

Expected: PASS.

- [ ] **Step 6: Review checkpoint**

Run:
`git diff --check && rg -n "Token|Authorization|Login|Repository" src/ApiIntegrationLab.Api/Common/Telemetry src/ApiIntegrationLab.Api/Integrations/GitHub`

Confirm token values never enter logs/traces and repository/login values are not metric labels. Do
not commit.

### Task 6: Implement both Microsoft Graph OAuth flows

**Files:**

- Create: `src/ApiIntegrationLab.Api/Authentication/Microsoft/GraphOptions.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Microsoft/IGraphTokenProvider.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Microsoft/MicrosoftGraphTokenProvider.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Microsoft/MicrosoftAuthController.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/MicrosoftGraph/GraphDtos.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/MicrosoftGraph/GraphModels.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/MicrosoftGraph/IMicrosoftGraphClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/MicrosoftGraph/MicrosoftGraphClient.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/MicrosoftGraph/MicrosoftGraphController.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/Integrations/GraphTestFactory.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/TestDoubles/RecordingGraphTokenProvider.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/MicrosoftGraphClientTests.cs`
- Test: `tests/ApiIntegrationLab.IntegrationTests/MicrosoftGraphEndpointTests.cs`
- Modify: `src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: `src/ApiIntegrationLab.Api/appsettings.json`

**Interfaces:**

- Produces: `IGraphTokenProvider.GetDelegatedTokenAsync(ClaimsPrincipal user, CancellationToken)`
  and `GetApplicationTokenAsync(CancellationToken)`.
- Produces: `IMicrosoftGraphClient.GetMeAsync(ClaimsPrincipal user, CancellationToken)` and
  `GetUsersAsync(int top, CancellationToken)`.
- Produces: login, callback, logout, delegated `/me`, and application `/users` routes from the spec.
- Test support: `RecordingGraphTokenProvider(string delegatedToken, string appToken)` records calls;
  `GraphTestFactory.CreateClient(IGraphTokenProvider, StubHttpMessageHandler)` constructs the real
  Graph client. The Microsoft.Identity.Web adapter remains thin and is covered by host registration
  plus Graph client token-selection tests.

- [ ] **Step 1: Add Microsoft Identity Web**

Run:

```bash
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package Microsoft.Identity.Web --version 4.14.2
```

- [ ] **Step 2: Write failing token-selection and Graph-request tests**

```csharp
[Fact]
public async Task GetMe_uses_delegated_UserRead_token()
{
    var tokens = new RecordingGraphTokenProvider(delegatedToken: "user-token", appToken: "app-token");
    var capture = StubHttpMessageHandler.Json("{\"id\":\"1\",\"displayName\":\"Demo User\"}");
    var client = GraphTestFactory.CreateClient(tokens, capture);

    await client.GetMeAsync(new ClaimsPrincipal(new ClaimsIdentity("test")), CancellationToken.None);

    Assert.Equal(1, tokens.DelegatedCalls);
    Assert.Equal(0, tokens.ApplicationCalls);
    Assert.Equal("user-token", capture.LastRequest!.Headers.Authorization!.Parameter);
    Assert.EndsWith("/v1.0/me?$select=id,displayName,mail,userPrincipalName",
        capture.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);
}

[Fact]
public async Task GetUsers_uses_application_default_scope_token()
{
    var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
    var capture = StubHttpMessageHandler.Json("{\"value\":[]}");
    var client = GraphTestFactory.CreateClient(tokens, capture);

    await client.GetUsersAsync(10, CancellationToken.None);

    Assert.Equal(0, tokens.DelegatedCalls);
    Assert.Equal(1, tokens.ApplicationCalls);
    Assert.Equal("app-token", capture.LastRequest!.Headers.Authorization!.Parameter);
}
```

Add tests for `top` bounds 1–50, missing AzureAd keys, token-acquisition failure redaction,
malformed Graph payload, and mapping of `@odata.nextLink` absence without adding Graph pagination
beyond `$top`.

- [ ] **Step 3: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter MicrosoftGraphClientTests`

Expected: FAIL because Graph auth and clients do not exist.

- [ ] **Step 4: Implement token acquisition behind a testable adapter**

```csharp
public interface IGraphTokenProvider
{
    Task<string> GetDelegatedTokenAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
    Task<string> GetApplicationTokenAsync(CancellationToken cancellationToken);
}

public sealed class GraphOptions
{
    public const string SectionName = "MicrosoftGraph";
    public Uri BaseUrl { get; init; } = new("https://graph.microsoft.com/v1.0/");
    public string[] DelegatedScopes { get; init; } = ["User.Read"];
    public string ApplicationScope { get; init; } = "https://graph.microsoft.com/.default";
}

public sealed record GraphUser(
    string Id,
    string DisplayName,
    string? Mail,
    string? UserPrincipalName);

public interface IMicrosoftGraphClient
{
    Task<GraphUser> GetMeAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<GraphUser>> GetUsersAsync(
        int top,
        CancellationToken cancellationToken);
}
```

The adapter wraps `ITokenAcquisition.GetAccessTokenForUserAsync` and
`ITokenAcquisition.GetAccessTokenForAppAsync`, validates `AzureAd:TenantId`, `ClientId`, and
`ClientSecret` at endpoint use, records only bounded `delegated`/`application` token metrics, and
never logs or exposes returned token strings. Inline comments contrast user identity with workload
identity at the two acquisition calls.

- [ ] **Step 5: Configure the OIDC cookie flow and Graph client**

Register:

```csharp
services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(["User.Read"])
    .AddInMemoryTokenCaches();
```

Set callback path `/auth/microsoft/callback`, save no tokens in response payloads, and use secure,
HTTP-only cookies compatible with localhost. `MicrosoftAuthController.Login` returns `Challenge`
with a validated local return URL; logout signs out cookie and OIDC schemes.

The client uses explicit `HttpRequestMessage` instances with bearer headers and fixed `$select`
fields, maps provider DTOs to `GraphUser`, and records no ID/mail/user-principal-name metric tags.

- [ ] **Step 6: Add endpoint tests and inline Swagger guidance**

Integration tests assert login challenges OIDC, `/me` requires a user session, `/users` works
through a fake app-token client without a cookie, invalid `top` returns 400, and missing
configuration returns 503 without crashing host startup. Swagger remarks list redirect URI,
delegated `User.Read`, and application `User.Read.All` admin-consent requirements.

- [ ] **Step 7: Verify both Microsoft flows**

Run: `dotnet test ApiIntegrationLab.sln --filter "Microsoft|Graph"`

Expected: PASS.

- [ ] **Step 8: Review checkpoint**

Run:
`git diff --check && rg -n "AccessToken|ClientSecret|Authorization" src/ApiIntegrationLab.Api/Authentication/Microsoft src/ApiIntegrationLab.Api/Integrations/MicrosoftGraph`

Every occurrence must be configuration access, an outbound header assignment, or an explanatory
comment; none may be a log argument or response property. Do not commit.

### Task 7: Implement replay-resistant inbound HMAC authentication

**Files:**

- Create: `src/ApiIntegrationLab.Api/Authentication/Webhooks/WebhookOptions.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Webhooks/WebhookVerificationResult.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Webhooks/IWebhookSignatureVerifier.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Webhooks/WebhookSignatureVerifier.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Webhooks/WebhookEvent.cs`
- Create: `src/ApiIntegrationLab.Api/Authentication/Webhooks/WebhookController.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/Authentication/WebhookTestFactory.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Authentication/WebhookSignatureVerifierTests.cs`
- Test: `tests/ApiIntegrationLab.IntegrationTests/WebhookEndpointTests.cs`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: `src/ApiIntegrationLab.Api/appsettings.json`

**Interfaces:**

- Produces:
  `WebhookVerificationResult Verify(byte[] body, string? timestamp, string? signature, DateTimeOffset now)`.
- Produces: `POST /api/webhooks/events` using `X-Webhook-Timestamp` and `X-Webhook-Signature-256`.
- Test support: `WebhookTestFactory.Create(string secret)` constructs the verifier and
  `Sign(string secret, string timestamp, byte[] body)` returns the exact `sha256=<hex>` header.

- [ ] **Step 1: Write failing HMAC tests**

```csharp
[Fact]
public void Verify_accepts_signature_over_timestamp_dot_exact_body()
{
    var body = Encoding.UTF8.GetBytes("{\"eventType\":\"demo.created\",\"value\":1}");
    const string timestamp = "1789500000";
    var verifier = WebhookTestFactory.Create(secret: "test-secret");
    var signature = WebhookTestFactory.Sign("test-secret", timestamp, body);

    var result = verifier.Verify(
        body,
        timestamp,
        signature,
        DateTimeOffset.FromUnixTimeSeconds(1789500000));

    Assert.True(result.IsValid);
}

[Theory]
[InlineData("sha256=00")]
[InlineData("not-prefixed")]
[InlineData("")]
public void Verify_rejects_invalid_signatures(string signature)
{
    var result = WebhookTestFactory.Create("test-secret").Verify(
        "{}"u8.ToArray(), "1789500000", signature,
        DateTimeOffset.FromUnixTimeSeconds(1789500000));

    Assert.False(result.IsValid);
}
```

Add tests for missing headers, malformed timestamp, timestamps older/newer than five minutes, a
single-byte body change, uppercase/non-hex signature, missing secret, and fixed-time comparison
path.

- [ ] **Step 2: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter WebhookSignatureVerifierTests`

Expected: FAIL because webhook verification does not exist.

- [ ] **Step 3: Implement exact-byte verification**

```csharp
public sealed record WebhookVerificationResult(bool IsValid, string? Failure)
{
    public static WebhookVerificationResult Valid() => new(true, null);
    public static WebhookVerificationResult Invalid(string failure) => new(false, failure);
}

public interface IWebhookSignatureVerifier
{
    WebhookVerificationResult Verify(
        byte[] body,
        string? timestamp,
        string? signature,
        DateTimeOffset now);
}
```

Parse the timestamp invariantly, reject absolute clock skew greater than five minutes, require a
64-character lowercase hexadecimal digest after `sha256=`, and compute:

```csharp
var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.")
    .Concat(body)
    .ToArray();
var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signedPayload);
var provided = Convert.FromHexString(signature["sha256=".Length..]);
var valid = CryptographicOperations.FixedTimeEquals(expected, provided);
```

After a successful fixed-time comparison, atomically cache the accepted digest until the signed
timestamp plus the replay window. Reject a duplicate cache key as `signature_replayed` and remove
expired keys during verification; store neither the body nor the secret.

Add comments explaining raw-body preservation, the replay window, and fixed-time comparison. Clear
temporary secret byte arrays when practical and never log body/signature values.

- [ ] **Step 4: Implement endpoint and integration tests**

Read the request body into a bounded buffer with a 64 KiB limit, verify before JSON deserialization,
then require a nonempty `eventType`. Return an acknowledgement containing only event type and
receipt time. Tests send correctly and incorrectly signed requests and assert 202, 401, 400, and 413
behavior; capture logs and assert body/signature text is absent.

- [ ] **Step 5: Verify webhook authentication**

Run: `dotnet test ApiIntegrationLab.sln --filter "Webhook|Hmac"`

Expected: PASS.

- [ ] **Step 6: Review checkpoint**

Run:
`git diff --check && rg -n "Request\.Body|Signature|Webhook" src/ApiIntegrationLab.Api/Authentication/Webhooks`

Confirm parsing occurs only after verification and no sensitive value is logged. Do not commit.

### Task 8: Implement concurrent normalized aggregation

**Files:**

- Create: `src/ApiIntegrationLab.Api/Integrations/Demo/DemoResponse.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/Demo/IDemoAggregator.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/Demo/DemoAggregator.cs`
- Create: `src/ApiIntegrationLab.Api/Integrations/Demo/DemoController.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/Integrations/DemoTestFactory.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/TestDoubles/ConcurrentStartProbe.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/TestDoubles/TestUsers.cs`
- Test: `tests/ApiIntegrationLab.UnitTests/Integrations/DemoAggregatorTests.cs`
- Test: `tests/ApiIntegrationLab.IntegrationTests/DemoEndpointTests.cs`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`

**Interfaces:**

- Consumes: `IPublicApiClient`, `IGitHubClient`, and `IMicrosoftGraphClient`.
- Produces:
  `Task<DemoResponse> GetAsync(ClaimsPrincipal user, CancellationToken cancellationToken)`.
- Produces: authorized `GET /api/demo`.
- Test support: `DemoTestFactory.Create(...)` supplies success/failure fakes,
  `CreateWithProbe(ConcurrentStartProbe)` blocks each fake until all three start, and
  `TestUsers.Authenticated` is a stable claims principal with no real identity data.

Use this factory signature so named arguments in the tests remain stable:

```csharp
public static DemoAggregator Create(
    IReadOnlyList<PublicPost>? publicResult = null,
    IntegrationException? publicError = null,
    GitHubRepositoryPage? githubResult = null,
    IntegrationException? githubError = null,
    GraphUser? graphResult = null,
    IntegrationException? graphError = null);
```

- [ ] **Step 1: Write failing aggregation tests**

```csharp
[Fact]
public async Task GetAsync_preserves_success_when_another_provider_fails()
{
    var aggregator = DemoTestFactory.Create(
        publicResult: [new PublicPost(1, "Public", "Body")],
        githubError: new IntegrationException(
            "github", IntegrationErrorCategory.Authentication, "GitHub rejected the token."),
        graphResult: new GraphUser("1", "Demo User", null, "demo@example.com"));

    var result = await aggregator.GetAsync(TestUsers.Authenticated, CancellationToken.None);

    Assert.Equal("success", result.PublicApi.Status);
    Assert.Equal("failed", result.GitHub.Status);
    Assert.Equal("success", result.Microsoft.Status);
    Assert.True(result.HasAnySuccess);
}

[Fact]
public async Task GetAsync_starts_all_provider_calls_before_awaiting_completion()
{
    var probes = new ConcurrentStartProbe(expectedStarts: 3);
    var aggregator = DemoTestFactory.CreateWithProbe(probes);

    await aggregator.GetAsync(TestUsers.Authenticated, CancellationToken.None);

    Assert.True(probes.AllStartedBeforeRelease);
}
```

Add tests for all success, all failure, caller cancellation, safe provider error messages, and no
provider-specific DTO escaping into the response.

- [ ] **Step 2: Run focused tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.UnitTests/ApiIntegrationLab.UnitTests.csproj --filter DemoAggregatorTests`

Expected: FAIL because aggregation does not exist.

- [ ] **Step 3: Implement provider envelopes and concurrency**

```csharp
public sealed record DemoResponse(
    IntegrationResult<IReadOnlyList<PublicPost>> PublicApi,
    IntegrationResult<GitHubRepositoryPage> GitHub,
    IntegrationResult<GraphUser> Microsoft)
{
    public bool HasAnySuccess =>
        PublicApi.Status == "success" ||
        GitHub.Status == "success" ||
        Microsoft.Status == "success";
}
```

Start all three tasks before awaiting them. Convert only `IntegrationException` into a failed
provider envelope; let cancellation propagate and map unexpected bugs through the global handler.
Create a parent activity and child events with provider names only. Add an inline comment explaining
why a provider failure is data for this aggregate rather than an exception that discards other
results.

- [ ] **Step 4: Implement endpoint semantics**

Mark `/api/demo` with `[Authorize]`. Return HTTP 200 with the envelope when `HasAnySuccess` is true;
throw an aggregate upstream `IntegrationException` when all fail so the global handler returns 502.
Integration tests assert unauthorized challenge behavior, partial success, and all-failure Problem
Details.

- [ ] **Step 5: Verify aggregation**

Run: `dotnet test ApiIntegrationLab.sln --filter "DemoAggregatorTests|DemoEndpointTests"`

Expected: PASS.

- [ ] **Step 6: Review checkpoint**

Run: `git diff --check && git status --short`

Confirm concurrent calls share caller cancellation and response errors reveal no upstream bodies. Do
not commit.

### Task 9: Complete Swagger, route documentation, health, and host-level behavior

**Files:**

- Create: `src/ApiIntegrationLab.Api/Common/OpenApi/SwaggerConfiguration.cs`
- Create: `tests/ApiIntegrationLab.IntegrationTests/SwaggerContractTests.cs`
- Create: `tests/ApiIntegrationLab.IntegrationTests/ProblemDetailsTests.cs`
- Create: `tests/ApiIntegrationLab.IntegrationTests/OptionalConfigurationTests.cs`
- Modify: `src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: every feature controller XML summary/remarks block

**Interfaces:**

- Produces: `/swagger`, `/swagger/v1/swagger.json`, and the complete endpoint catalogue.
- Produces: consistent `application/problem+json` responses.
- Preserves: `/health` success without provider configuration.

- [ ] **Step 1: Write failing Swagger contract tests**

```csharp
[Fact]
public async Task Swagger_contains_every_demo_route_and_explanation()
{
    var document = await _client.GetStringAsync("/swagger/v1/swagger.json");

    string[] routes =
    [
        "/api/public/posts", "/api/basic/profile", "/api/github/profile",
        "/api/github/repos", "/api/github/rate-limit", "/api/microsoft/login",
        "/api/microsoft/me", "/api/microsoft/users", "/api/microsoft/logout",
        "/api/webhooks/events", "/api/demo", "/health"
    ];

    foreach (var route in routes)
        Assert.Contains(route, document, StringComparison.Ordinal);

    Assert.Contains("Authorization Code", document, StringComparison.Ordinal);
    Assert.Contains("Client Credentials", document, StringComparison.Ordinal);
    Assert.Contains("HMAC", document, StringComparison.Ordinal);
}
```

Add tests asserting every operation has a summary and nonempty description, Swagger UI returns 200,
errors use Problem Details, trace IDs exist, secrets supplied in test configuration do not appear in
documents/errors, and optional credentials do not affect `/health` or Swagger.

- [ ] **Step 2: Run host-contract tests and verify failure**

Run:
`dotnet test tests/ApiIntegrationLab.IntegrationTests/ApiIntegrationLab.IntegrationTests.csproj --filter "SwaggerContractTests|ProblemDetailsTests|OptionalConfigurationTests"`

Expected: FAIL until Swagger/XML documentation and all host behavior are wired.

- [ ] **Step 3: Enable XML documentation and Swagger UI in every environment**

Add to the API project:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

Register `AddEndpointsApiExplorer` and `AddSwaggerGen`, include the XML file, describe cookie/OIDC
and bearer demonstrations without asking Swagger to store provider tokens, and expose Swagger at
`/swagger`. Add controller remarks with prerequisites, exact flow order, expected failures, and safe
example responses. Keep explanations at the implementation surface so the source remains sufficient.

- [ ] **Step 4: Finalize middleware order**

Use this order: forwarded headers where configured, exception handler, Swagger, authentication,
authorization, controllers, health. Avoid HTTPS redirection because the explicit local Docker
contract is HTTP localhost and outbound provider traffic remains HTTPS.

- [ ] **Step 5: Verify all API contracts**

Run: `dotnet test ApiIntegrationLab.sln`

Expected: PASS.

- [ ] **Step 6: Review checkpoint**

Run: `git diff --check && rg -n "<summary>|<remarks>" src/ApiIntegrationLab.Api`

Open the generated Swagger JSON and confirm every route explains setup and failure behavior. Do not
commit.

### Task 10: Export safe OpenTelemetry logs, metrics, and traces

**Files:**

- Create: `src/ApiIntegrationLab.Api/Common/Telemetry/OpenTelemetryConfiguration.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/TestDoubles/TelemetryTestListener.cs`
- Create: `tests/ApiIntegrationLab.UnitTests/Common/OpenTelemetrySafetyTests.cs`
- Create: `tests/ApiIntegrationLab.IntegrationTests/TelemetryCorrelationTests.cs`
- Modify: `src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj`
- Modify: `src/ApiIntegrationLab.Api/Program.cs`
- Modify: `src/ApiIntegrationLab.Api/appsettings.json`

**Interfaces:**

- Consumes: `ApiTelemetry.ActivitySourceName` and `ApiTelemetry.MeterName`.
- Produces: OTLP logs, metrics, and traces with service name `api-integration-lab`.
- Produces: correlated structured log records without secret/high-cardinality telemetry fields.
- Test support: `TelemetryTestListener.ListenTo(string sourceName)` captures activities and exposes
  a deterministic `SerializedActivities` string built from display names and tag keys/values.

- [ ] **Step 1: Add OpenTelemetry packages**

Run:

```bash
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package OpenTelemetry.Extensions.Hosting --version 1.18.0
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.18.0
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package OpenTelemetry.Instrumentation.AspNetCore --version 1.18.0
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package OpenTelemetry.Instrumentation.Http --version 1.18.0
dotnet add src/ApiIntegrationLab.Api/ApiIntegrationLab.Api.csproj package OpenTelemetry.Instrumentation.Runtime --version 1.18.0
```

- [ ] **Step 2: Write failing telemetry safety tests**

```csharp
[Fact]
public async Task Outbound_span_does_not_contain_authorization_or_query_secrets()
{
    using var listener = TelemetryTestListener.ListenTo(ApiTelemetry.ActivitySourceName);
    var client = GitHubTestFactory.CreateClientWithToken("never-export-this-token");

    await client.GetProfileAsync(CancellationToken.None);

    Assert.DoesNotContain("never-export-this-token", listener.SerializedActivities,
        StringComparison.Ordinal);
    Assert.DoesNotContain("authorization", listener.SerializedActivities,
        StringComparison.OrdinalIgnoreCase);
}
```

Add a structured-log capture test with marker credentials and webhook body, then assert the captured
rendered messages and properties contain none of them. Add a request test asserting a trace ID
appears in safe application log scope and Problem Details correlation.

- [ ] **Step 3: Run telemetry tests and verify failure**

Run:
`dotnet test ApiIntegrationLab.sln --filter "OpenTelemetrySafetyTests|TelemetryCorrelationTests"`

Expected: FAIL until OpenTelemetry registration and safe enrichment exist.

- [ ] **Step 4: Register OpenTelemetry once for all signals**

```csharp
services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "api-integration-lab",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"))
    .WithTracing(tracing => tracing
        .AddSource(ApiTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(ApiTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation())
    .UseOtlpExporter();
```

Register logging with `AddOpenTelemetry`, formatted messages, scopes, parsed state values, and OTLP.
Use standard instrumentation attributes only; do not enrich with authorization headers, query
strings, user claims, bodies, signatures, GitHub logins, or Graph identities. Add a comment at the
configuration boundary explaining that identifiers belong in provider responses and trace-correlated
logs only when explicitly safe—not metric dimensions.

- [ ] **Step 5: Verify telemetry safety and cardinality**

Run: `dotnet test ApiIntegrationLab.sln --filter "Telemetry|ApiTelemetry"`

Expected: PASS, with the four documented custom metric instruments and only bounded tag values.

- [ ] **Step 6: Review checkpoint**

Run:
`git diff --check && rg -n "AddTag|SetTag|Record\(|Log(Information|Warning|Error)" src/ApiIntegrationLab.Api`

Manually inspect every tag/log property against the approved schema. Do not commit.

### Task 11: Add collector configuration and the reproducible Docker Compose stack

**Files:**

- Create: `Dockerfile`
- Create: `.dockerignore`
- Create: `docker-compose.yml`
- Create: `otel-collector.yaml`
- Create: `.env.example`
- Modify: `.gitignore`

**Interfaces:**

- Produces: API on `localhost:8080`, Swagger on `/swagger`, and Grafana on `localhost:3000`.
- Produces: application OTLP → standalone Collector → Grafana LGTM flow.
- Uses: `otel/opentelemetry-collector-contrib:0.160.0` and `grafana/otel-lgtm:0.33.0`.

- [ ] **Step 1: Add collector configuration with inline operational comments**

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  # Bound memory before batching so a disconnected local backend cannot grow the collector forever.
  memory_limiter:
    check_interval: 1s
    limit_mib: 256
    spike_limit_mib: 64
  resource:
    attributes:
      - key: deployment.environment.name
        value: local
        action: upsert
  batch:
    timeout: 2s

exporters:
  otlp/lgtm:
    endpoint: lgtm:4317
    tls:
      # Traffic stays inside the local Compose network; LGTM exposes plaintext OTLP for demos.
      insecure: true

extensions:
  health_check:
    endpoint: 0.0.0.0:13133

service:
  extensions: [health_check]
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, resource, batch]
      exporters: [otlp/lgtm]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, resource, batch]
      exporters: [otlp/lgtm]
    logs:
      receivers: [otlp]
      processors: [memory_limiter, resource, batch]
      exporters: [otlp/lgtm]
```

- [ ] **Step 2: Validate the collector configuration**

Run:

```bash
docker run --rm -v "$PWD/otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro" \
  otel/opentelemetry-collector-contrib:0.160.0 \
  validate --config=/etc/otelcol-contrib/config.yaml
```

Expected: exit 0 with a valid configuration message.

- [ ] **Step 3: Create the multi-stage API image**

Use `mcr.microsoft.com/dotnet/sdk:8.0` to restore/publish the API and
`mcr.microsoft.com/dotnet/aspnet:8.0` for runtime. Copy project files before source files for
restore cache efficiency, create a non-root runtime user, expose 8080, and install only the minimal
health probe dependency if the base image lacks one. Comments explain layer order and non-root
ownership.

- [ ] **Step 4: Create Compose and secret template**

Compose defines:

```yaml
services:
  api:
    build: .
    ports: ["8080:8080"]
    environment:
      ASPNETCORE_URLS: http://+:8080
      OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
      OTEL_EXPORTER_OTLP_PROTOCOL: grpc
      GitHub__Token: ${GITHUB_TOKEN:-}
      AzureAd__TenantId: ${MS_TENANT_ID:-}
      AzureAd__ClientId: ${MS_CLIENT_ID:-}
      AzureAd__ClientSecret: ${MS_CLIENT_SECRET:-}
      BasicAuth__Username: ${BASIC_AUTH_USERNAME:-}
      BasicAuth__Password: ${BASIC_AUTH_PASSWORD:-}
      Webhook__Secret: ${WEBHOOK_SECRET:-}
    depends_on:
      otel-collector:
        condition: service_started

  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.160.0
    command: ["--config=/etc/otelcol-contrib/config.yaml"]
    volumes:
      - ./otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro
    depends_on: [lgtm]

  lgtm:
    image: grafana/otel-lgtm:0.33.0
    ports: ["3000:3000"]
```

Add health checks supported by the selected images and a named local data volume only if LGTM
requires one. `.env.example` contains empty values plus comments for origin, permission, and
sensitivity of each variable. `.env` remains ignored.

- [ ] **Step 5: Validate image and Compose rendering**

Run:

```bash
docker build -t api-integration-lab:test .
docker compose --env-file .env.example config --quiet
```

Expected: both commands exit 0; rendered Compose contains no nonempty secret.

- [ ] **Step 6: Review checkpoint**

Run: `git diff --check && git status --short`

Confirm no `latest` tags, host secret files, or writable source mounts exist. Do not commit.

### Task 12: Add a secret-safe live smoke harness

**Files:**

- Create: `scripts/smoke-test.sh`
- Create: `tests/manual/README.md`
- Modify: `.gitignore`

**Interfaces:**

- Consumes: running Compose stack and optional environment credentials.
- Produces: nonzero exit on failed mandatory checks and explicit `SKIP` for unconfigured credential
  flows.

- [ ] **Step 1: Write the smoke-test contract as shell assertions**

The script must use `set -euo pipefail`, a configurable `API_BASE_URL` defaulting to
`http://localhost:8080`, temporary files from `mktemp -d`, and a cleanup trap. It must never enable
shell tracing or print secret-bearing commands.

Required checks:

```text
PASS health
PASS swagger
PASS public API
PASS Basic Auth or SKIP missing BASIC_AUTH_USERNAME/BASIC_AUTH_PASSWORD
PASS GitHub profile or PASS expected tokenless 401
PASS HMAC valid signature
PASS HMAC invalid signature rejected
INFO Microsoft login redirect or SKIP missing MS_* configuration
PASS Grafana health
```

- [ ] **Step 2: Implement HMAC generation without logging the secret**

Use current Unix seconds, a fixed JSON body, and `openssl dgst -sha256 -hmac "$WEBHOOK_SECRET"`;
strip the command prefix from output and send `sha256=<digest>`. If no webhook secret is exported,
use a process-local demo value only when the running API was started with that same explicit value;
otherwise report `SKIP` rather than inventing mismatched configuration.

- [ ] **Step 3: Run shell static and syntax checks**

Run:

```bash
bash -n scripts/smoke-test.sh
if command -v shellcheck >/dev/null 2>&1; then shellcheck scripts/smoke-test.sh; fi
```

Expected: no syntax errors; ShellCheck clean when installed.

- [ ] **Step 4: Document manual OAuth execution**

`tests/manual/README.md` gives the exact Entra redirect URI, delegated/application permissions,
admin-consent requirement, login → callback → `/me` sequence, app-only `/users` check, aggregate
check, and the Grafana Explore queries for service name and trace ID. It contains no credential
value.

- [ ] **Step 5: Review checkpoint**

Run: `git diff --check && rg -n "set -x|echo.*SECRET|echo.*TOKEN" scripts tests/manual`

Expected: no secret-printing patterns. Do not commit.

### Task 13: Write the showcase README and perform full verification

**Files:**

- Modify: `README.md`
- Optionally create from real local output: `docs/images/swagger-api-catalog.png`
- Optionally create from real local output: `docs/images/grafana-trace.png`

**Interfaces:**

- Produces: clone → configure → Compose → Swagger/Grafana walkthrough for a manager.
- Produces: architecture, authentication matrix, endpoint catalogue, security model, telemetry
  schema, live-demo script, troubleshooting, and representative validated trace output.

- [ ] **Step 1: Write README structure from verified behavior**

Include these sections with exact commands and no aspirational claims:

```text
Why this lab exists
Architecture
Authentication matrix
Prerequisites
Quick start
Provider configuration
Endpoint demo sequence
Expected failure demonstrations
GitHub pagination and rate limits
Microsoft Authorization Code lifecycle
Microsoft Client Credentials lifecycle
HMAC signing example
Resilience and error contract
Observability and cardinality budget
Security boundaries
Automated tests and validation
Troubleshooting
```

Use a Mermaid architecture diagram with the required pastel-on-dark initialization directive and
colored `classDef` nodes. Include HTTP request/response examples with redacted tokens. Capture
actual Swagger/Grafana screenshots only if the rendered stack exposes no personal or tenant data;
otherwise include a sanitized trace tree and exact Grafana query instead of manufacturing
screenshots.

- [ ] **Step 2: Run fast local verification**

Run:

```bash
dotnet restore ApiIntegrationLab.sln
dotnet build ApiIntegrationLab.sln --configuration Release --no-restore
dotnet test ApiIntegrationLab.sln --configuration Release --no-build
bash -n scripts/smoke-test.sh
docker compose --env-file .env.example config --quiet
```

Expected: all commands exit 0.

- [ ] **Step 3: Run container and collector verification**

Run:

```bash
docker run --rm -v "$PWD/otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro" \
  otel/opentelemetry-collector-contrib:0.160.0 \
  validate --config=/etc/otelcol-contrib/config.yaml
docker build -t api-integration-lab:test .
docker compose --env-file .env.example up -d
docker compose ps
```

Wait only for bounded health-check deadlines, then run `scripts/smoke-test.sh`. Inspect API and
collector logs for secret marker values and errors. Generate public/API traffic and confirm in
Grafana:

- trace service `api-integration-lab` contains inbound and outbound spans;
- logs correlate on trace ID;
- `api_client_requests_total` and duration histogram appear;
- no forbidden metric label exists.

- [ ] **Step 4: Exercise real credential flows when credentials are available**

Run the documented GitHub, Microsoft delegated, Microsoft application, aggregate, Basic, and HMAC
checks. If credentials are unavailable, report those live checks as unverified rather than treating
mocked automated coverage as a live pass.

- [ ] **Step 5: Stop the validation stack without deleting user data**

Run: `docker compose down`

Do not pass `--volumes`; local telemetry remains recoverable until the user chooses to delete it.

- [ ] **Step 6: Final requirement and diff audit**

Run:

```bash
git diff --check
git status --short
rg -n "T[B]D|T[O]DO|implement l[a]ter|fill in d[e]tails" README.md src tests scripts Dockerfile docker-compose.yml otel-collector.yaml
```

Walk every acceptance criterion in the design spec and point to its implementation and evidence.
The original project brief is retained in Git history and intentionally absent from the working
tree. Do not commit or push.
