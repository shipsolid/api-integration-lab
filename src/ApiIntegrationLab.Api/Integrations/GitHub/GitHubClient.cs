using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Http;
using ApiIntegrationLab.Api.Common.Telemetry;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace ApiIntegrationLab.Api.Integrations.GitHub;

public sealed class GitHubClient : IGitHubClient
{
    private readonly HttpClient _httpClient;
    private readonly ApiTelemetry _telemetry;
    private readonly ILogger<GitHubClient> _logger;

    public GitHubClient(
        HttpClient httpClient,
        ApiTelemetry telemetry,
        ILogger<GitHubClient> logger)
    {
        _httpClient = httpClient;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        using var measurement = _telemetry.MeasureRequest("github", "profile");
        try
        {
            using var response = await _httpClient.GetAsync("user", cancellationToken);
            EnsureSuccess(response);
            var dto = await response.Content.ReadFromJsonAsync<GitHubProfileDto>(
                cancellationToken: cancellationToken)
                ?? throw InvalidPayload("profile");
            if (string.IsNullOrWhiteSpace(dto.Login) || dto.PublicRepositories < 0)
                throw InvalidPayload("profile");

            var profile = new GitHubProfile(
                dto.Login,
                dto.Name,
                dto.PublicRepositories,
                RequireHttpsUri(dto.HtmlUrl, "profile"));
            measurement.Succeed();
            _logger.LogInformation("GitHub profile request completed with status {StatusCode}", (int)response.StatusCode);
            return profile;
        }
        catch (IntegrationException exception)
        {
            measurement.Fail(ToTelemetryError(exception.Category));
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            measurement.Cancel();
            throw;
        }
        catch (RateLimiterRejectedException exception)
        {
            measurement.Fail("throttled");
            throw LocallyThrottled(exception);
        }
        catch (BrokenCircuitException exception)
        {
            measurement.Fail("upstream");
            throw CircuitOpen(exception);
        }
        catch (TimeoutRejectedException exception)
        {
            measurement.Fail("timeout");
            throw Timeout(exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            measurement.Fail("timeout");
            throw Timeout(exception);
        }
        catch (HttpRequestException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload("profile", exception);
        }
        catch (JsonException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload("profile", exception);
        }
    }

    public async Task<GitHubRepositoryPage> GetRepositoriesAsync(
        int perPage,
        int maxPages,
        CancellationToken cancellationToken)
    {
        if (perPage is < 1 or > 100 || maxPages is < 1 or > 10)
        {
            throw new IntegrationException(
                "github",
                IntegrationErrorCategory.Validation,
                "perPage must be 1-100 and maxPages must be 1-10.");
        }

        using var measurement = _telemetry.MeasureRequest("github", "repositories");
        try
        {
            var repositories = new List<GitHubRepository>();
            Uri? next = new(_httpClient.BaseAddress!, $"user/repos?per_page={perPage}&page=1&sort=updated");
            var pagesFetched = 0;
            var rateLimit = EmptyRateLimit();

            while (next is not null && pagesFetched < maxPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var response = await _httpClient.GetAsync(next, cancellationToken);
                EnsureSuccess(response);
                var page = await response.Content.ReadFromJsonAsync<List<GitHubRepositoryDto?>>(
                    cancellationToken: cancellationToken)
                    ?? throw InvalidPayload("repositories");

                repositories.AddRange(page.Select(MapRepository));
                pagesFetched++;
                rateLimit = ParseRateLimitHeaders(response);

                // GitHub's Link relation is authoritative; a full page does not prove another page exists.
                next = GitHubLinkParser.GetNext(response, _httpClient.BaseAddress!);
            }

            measurement.Succeed();
            _logger.LogInformation(
                "GitHub repositories request completed with {PagesFetched} pages and {RepositoryCount} repositories",
                pagesFetched,
                repositories.Count);
            return new GitHubRepositoryPage(repositories, pagesFetched, rateLimit);
        }
        catch (IntegrationException exception)
        {
            measurement.Fail(ToTelemetryError(exception.Category));
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            measurement.Cancel();
            throw;
        }
        catch (RateLimiterRejectedException exception)
        {
            measurement.Fail("throttled");
            throw LocallyThrottled(exception);
        }
        catch (BrokenCircuitException exception)
        {
            measurement.Fail("upstream");
            throw CircuitOpen(exception);
        }
        catch (TimeoutRejectedException exception)
        {
            measurement.Fail("timeout");
            throw Timeout(exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            measurement.Fail("timeout");
            throw Timeout(exception);
        }
        catch (HttpRequestException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload("repositories", exception);
        }
        catch (JsonException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload("repositories", exception);
        }
    }

    public async Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken)
    {
        using var measurement = _telemetry.MeasureRequest("github", "rate_limit");
        try
        {
            using var response = await _httpClient.GetAsync("rate_limit", cancellationToken);
            EnsureSuccess(response);
            var envelope = await response.Content.ReadFromJsonAsync<GitHubRateLimitEnvelope>(
                cancellationToken: cancellationToken)
                ?? throw InvalidPayload("rate limit");
            var core = envelope.Resources?.Core ?? throw InvalidPayload("rate limit");
            if (core.Limit < 0 || core.Remaining < 0 || core.Used < 0 || core.Reset <= 0)
                throw InvalidPayload("rate limit");
            var result = new GitHubRateLimit(
                core.Limit,
                core.Remaining,
                core.Used,
                FromUnixTimeSeconds(core.Reset, "rate limit"),
                "core");
            measurement.Succeed();
            return result;
        }
        catch (IntegrationException exception)
        {
            measurement.Fail(ToTelemetryError(exception.Category));
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            measurement.Cancel();
            throw;
        }
        catch (RateLimiterRejectedException exception)
        {
            measurement.Fail("throttled");
            throw LocallyThrottled(exception);
        }
        catch (BrokenCircuitException exception)
        {
            measurement.Fail("upstream");
            throw CircuitOpen(exception);
        }
        catch (TimeoutRejectedException exception)
        {
            measurement.Fail("timeout");
            throw Timeout(exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            measurement.Fail("timeout");
            throw Timeout(exception);
        }
        catch (HttpRequestException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload("rate limit", exception);
        }
        catch (JsonException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload("rate limit", exception);
        }
    }

    private static GitHubRepository MapRepository(GitHubRepositoryDto? dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name) || dto.UpdatedAt == default)
            throw InvalidPayload("repository");
        return new GitHubRepository(
            dto.Name,
            dto.Description,
            dto.IsPrivate,
            RequireHttpsUri(dto.HtmlUrl, "repository"),
            dto.UpdatedAt);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var category = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => IntegrationErrorCategory.Authentication,
            HttpStatusCode.Forbidden when HeaderIsZero(response, "X-RateLimit-Remaining") =>
                IntegrationErrorCategory.Throttled,
            HttpStatusCode.Forbidden => IntegrationErrorCategory.Authorization,
            HttpStatusCode.TooManyRequests => IntegrationErrorCategory.Throttled,
            _ => IntegrationErrorCategory.Upstream
        };
        var retryAfter = category == IntegrationErrorCategory.Throttled
            ? ReadRetryAfter(response)
            : null;
        throw new IntegrationException(
            "github",
            category,
            $"GitHub returned HTTP {(int)response.StatusCode}.",
            retryAfter);
    }

    private static GitHubRateLimit ParseRateLimitHeaders(HttpResponseMessage response)
    {
        var reset = HeaderAsLong(response, "X-RateLimit-Reset");
        return new GitHubRateLimit(
            HeaderAsInt(response, "X-RateLimit-Limit"),
            HeaderAsInt(response, "X-RateLimit-Remaining"),
            HeaderAsInt(response, "X-RateLimit-Used"),
            reset > 0 ? FromUnixTimeSeconds(reset, "rate-limit header") : DateTimeOffset.UnixEpoch,
            HeaderAsString(response, "X-RateLimit-Resource") ?? "core");
    }

    private static GitHubRateLimit EmptyRateLimit() =>
        new(0, 0, 0, DateTimeOffset.UnixEpoch, "core");

    private static int HeaderAsInt(HttpResponseMessage response, string name) =>
        int.TryParse(HeaderAsString(response, name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static long HeaderAsLong(HttpResponseMessage response, string name) =>
        long.TryParse(HeaderAsString(response, name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static string? HeaderAsString(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static bool HeaderIsZero(HttpResponseMessage response, string name) =>
        string.Equals(HeaderAsString(response, name), "0", StringComparison.Ordinal);

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        if (RetryAfterParser.Read(response) is { } retryAfter)
            return retryAfter;

        var reset = HeaderAsLong(response, "X-RateLimit-Reset");
        if (reset <= 0)
            return null;
        var untilReset = DateTimeOffset.FromUnixTimeSeconds(reset) - DateTimeOffset.UtcNow;
        return untilReset > TimeSpan.Zero ? untilReset : TimeSpan.Zero;
    }

    private static Uri RequireHttpsUri(string value, string resource)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            return uri;
        throw InvalidPayload(resource);
    }

    private static DateTimeOffset FromUnixTimeSeconds(long value, string resource)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw InvalidPayload(resource);
        }
    }

    private static IntegrationException InvalidPayload(string resource) =>
        new("github", IntegrationErrorCategory.Upstream, $"GitHub returned an invalid {resource} response.");

    private static IntegrationException InvalidPayload(string resource, Exception exception) =>
        new(
            "github",
            IntegrationErrorCategory.Upstream,
            $"GitHub returned an invalid {resource} response.",
            innerException: exception);

    private static IntegrationException Timeout(Exception inner) =>
        new(
            "github",
            IntegrationErrorCategory.Timeout,
            "GitHub did not respond within the configured timeout.",
            innerException: inner);

    // Circuit-breaker and concurrency-limiter failures have no provider response to inspect.
    // Keeping their mapping here gives every GitHub operation the same safe external contract.
    private static IntegrationException LocallyThrottled(RateLimiterRejectedException exception) =>
        new(
            "github",
            IntegrationErrorCategory.Throttled,
            "The GitHub request was deferred because local outbound capacity is exhausted.",
            exception.RetryAfter,
            exception);

    private static IntegrationException CircuitOpen(Exception exception) =>
        new(
            "github",
            IntegrationErrorCategory.Upstream,
            "GitHub is temporarily unavailable because its circuit breaker is open.",
            innerException: exception);

    private static string ToTelemetryError(IntegrationErrorCategory category) => category switch
    {
        IntegrationErrorCategory.Authentication => "authentication",
        IntegrationErrorCategory.Authorization => "authorization",
        IntegrationErrorCategory.Configuration => "configuration",
        IntegrationErrorCategory.Throttled => "throttled",
        IntegrationErrorCategory.Timeout => "timeout",
        _ => "upstream"
    };
}
