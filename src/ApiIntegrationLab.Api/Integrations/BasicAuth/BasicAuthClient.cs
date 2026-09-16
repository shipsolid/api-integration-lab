using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Http;
using ApiIntegrationLab.Api.Common.Telemetry;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace ApiIntegrationLab.Api.Integrations.BasicAuth;

public sealed class BasicAuthClient : IBasicAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly ApiTelemetry _telemetry;
    private readonly ILogger<BasicAuthClient> _logger;

    public BasicAuthClient(
        HttpClient httpClient,
        ApiTelemetry telemetry,
        ILogger<BasicAuthClient> logger)
    {
        _httpClient = httpClient;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<BasicAuthProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        using var measurement = _telemetry.MeasureRequest("basic", "profile");

        try
        {
            using var response = await _httpClient.GetAsync("basic-auth", cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw MapFailure(response);

            var payload = await response.Content.ReadFromJsonAsync<BasicAuthResponse>(
                cancellationToken: cancellationToken)
                ?? throw new IntegrationException(
                    "basic",
                    IntegrationErrorCategory.Upstream,
                    "The Basic Auth provider returned an empty response.");
            if (payload.Authenticated is not { } authenticated)
            {
                throw new IntegrationException(
                    "basic",
                    IntegrationErrorCategory.Upstream,
                    "The Basic Auth provider returned an invalid response.");
            }

            measurement.Succeed();
            _logger.LogInformation(
                "Basic Auth request completed with authenticated={Authenticated}",
                authenticated);
            return new BasicAuthProfile("postman-echo", authenticated);
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
        // The resilience handler throws these before an HTTP response exists. Translate them into
        // the same stable error contract used for provider responses.
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
        var category = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => IntegrationErrorCategory.Authentication,
            HttpStatusCode.Forbidden => IntegrationErrorCategory.Authorization,
            HttpStatusCode.TooManyRequests => IntegrationErrorCategory.Throttled,
            _ => IntegrationErrorCategory.Upstream
        };
        return new IntegrationException(
            "basic",
            category,
            $"The Basic Auth provider returned HTTP {(int)response.StatusCode}.",
            category == IntegrationErrorCategory.Throttled
                ? RetryAfterParser.Read(response)
                : null);
    }

    private static IntegrationException Timeout(Exception exception) =>
        new(
            "basic",
            IntegrationErrorCategory.Timeout,
            "The Basic Auth provider did not respond within the configured timeout.",
            innerException: exception);

    private static IntegrationException Upstream(Exception exception) =>
        new(
            "basic",
            IntegrationErrorCategory.Upstream,
            "The Basic Auth provider returned an invalid response.",
            innerException: exception);

    private static IntegrationException LocallyThrottled(RateLimiterRejectedException exception) =>
        new(
            "basic",
            IntegrationErrorCategory.Throttled,
            "The Basic Auth request was deferred because local outbound capacity is exhausted.",
            exception.RetryAfter,
            exception);

    private static IntegrationException CircuitOpen(Exception exception) =>
        new(
            "basic",
            IntegrationErrorCategory.Upstream,
            "The Basic Auth provider is temporarily unavailable because its circuit breaker is open.",
            innerException: exception);

    private static string ToTelemetryError(IntegrationErrorCategory category)
    {
        return category switch
        {
            IntegrationErrorCategory.Authentication => "authentication",
            IntegrationErrorCategory.Authorization => "authorization",
            IntegrationErrorCategory.Configuration => "configuration",
            IntegrationErrorCategory.Throttled => "throttled",
            IntegrationErrorCategory.Timeout => "timeout",
            _ => "upstream"
        };
    }

    private sealed record BasicAuthResponse(
        [property: JsonPropertyName("authenticated")] bool? Authenticated);
}
