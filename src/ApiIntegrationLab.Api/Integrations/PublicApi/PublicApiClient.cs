using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Http;
using ApiIntegrationLab.Api.Common.Telemetry;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace ApiIntegrationLab.Api.Integrations.PublicApi;

public sealed class PublicApiClient : IPublicApiClient
{
    private const int MaximumPreviewLength = 120;
    private readonly HttpClient _httpClient;
    private readonly ApiTelemetry _telemetry;
    private readonly ILogger<PublicApiClient> _logger;

    public PublicApiClient(
        HttpClient httpClient,
        ApiTelemetry telemetry,
        ILogger<PublicApiClient> logger)
    {
        _httpClient = httpClient;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PublicPost>> GetPostsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
        {
            throw new IntegrationException(
                "public",
                IntegrationErrorCategory.Validation,
                "The post limit must be between 1 and 100.");
        }

        using var measurement = _telemetry.MeasureRequest("public", "posts");

        try
        {
            using var response = await _httpClient.GetAsync("posts", cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw MapFailure(response);

            var providerPosts = await response.Content.ReadFromJsonAsync<List<PublicPostDto?>>(
                cancellationToken: cancellationToken)
                ?? throw new IntegrationException(
                    "public",
                    IntegrationErrorCategory.Upstream,
                    "The public API returned an empty response.");

            var posts = providerPosts
                .Take(limit)
                .Select(Map)
                .ToArray();

            measurement.Succeed();
            _logger.LogInformation(
                "Public API request completed with {PostCount} normalized posts",
                posts.Length);
            return posts;
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
        // These exceptions are produced locally by the resilience pipeline, so normalize them just
        // like remote failures instead of letting implementation-specific Polly types reach callers.
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
            throw Upstream(exception);
        }
        catch (JsonException exception)
        {
            measurement.Fail("upstream");
            throw Upstream(exception);
        }
    }

    private static IntegrationException MapFailure(HttpResponseMessage response)
    {
        var category = response.StatusCode == HttpStatusCode.TooManyRequests
            ? IntegrationErrorCategory.Throttled
            : IntegrationErrorCategory.Upstream;
        return new IntegrationException(
            "public",
            category,
            $"The public API returned HTTP {(int)response.StatusCode}.",
            category == IntegrationErrorCategory.Throttled
                ? RetryAfterParser.Read(response)
                : null);
    }

    private static IntegrationException Timeout(Exception exception) =>
        new(
            "public",
            IntegrationErrorCategory.Timeout,
            "The public API did not respond within the configured timeout.",
            innerException: exception);

    private static IntegrationException Upstream(Exception exception) =>
        new(
            "public",
            IntegrationErrorCategory.Upstream,
            "The public API returned an invalid response.",
            innerException: exception);

    private static IntegrationException LocallyThrottled(RateLimiterRejectedException exception) =>
        new(
            "public",
            IntegrationErrorCategory.Throttled,
            "The public API request was deferred because local outbound capacity is exhausted.",
            exception.RetryAfter,
            exception);

    private static IntegrationException CircuitOpen(Exception exception) =>
        new(
            "public",
            IntegrationErrorCategory.Upstream,
            "The public API is temporarily unavailable because its circuit breaker is open.",
            innerException: exception);

    private static PublicPost Map(PublicPostDto? post)
    {
        if (post is null ||
            post.Id <= 0 ||
            string.IsNullOrWhiteSpace(post.Title) ||
            string.IsNullOrWhiteSpace(post.Body))
        {
            throw new IntegrationException(
                "public",
                IntegrationErrorCategory.Upstream,
                "The public API returned an invalid response.");
        }

        return new PublicPost(post.Id, post.Title, Abbreviate(post.Body));
    }

    private static string ToTelemetryError(IntegrationErrorCategory category)
    {
        return category switch
        {
            IntegrationErrorCategory.Throttled => "throttled",
            IntegrationErrorCategory.Timeout => "timeout",
            _ => "upstream"
        };
    }

    private static string Abbreviate(string body)
    {
        var normalized = string.Join(' ', body.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= MaximumPreviewLength
            ? normalized
            : normalized[..MaximumPreviewLength];
    }
}
