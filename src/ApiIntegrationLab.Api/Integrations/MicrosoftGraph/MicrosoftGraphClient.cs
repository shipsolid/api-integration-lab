using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ApiIntegrationLab.Api.Authentication.Microsoft;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Http;
using ApiIntegrationLab.Api.Common.Telemetry;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace ApiIntegrationLab.Api.Integrations.MicrosoftGraph;

public sealed class MicrosoftGraphClient : IMicrosoftGraphClient
{
    private const string SelectedFields = "id,displayName,mail,userPrincipalName";
    private readonly HttpClient _httpClient;
    private readonly IGraphTokenProvider _tokens;
    private readonly ApiTelemetry _telemetry;
    private readonly ILogger<MicrosoftGraphClient> _logger;

    public MicrosoftGraphClient(
        HttpClient httpClient,
        IGraphTokenProvider tokens,
        ApiTelemetry telemetry,
        ILogger<MicrosoftGraphClient> logger)
    {
        _httpClient = httpClient;
        _tokens = tokens;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<GraphUser> GetMeAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        using var measurement = _telemetry.MeasureRequest("microsoft", "profile");
        try
        {
            EnsureHttpsBaseAddress();
            var token = await _tokens.GetDelegatedTokenAsync(user, cancellationToken);
            using var request = CreateRequest($"me?$select={SelectedFields}", token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            EnsureSuccess(response);
            var dto = await response.Content.ReadFromJsonAsync<GraphUserDto>(
                cancellationToken: cancellationToken)
                ?? throw InvalidPayload();
            var profile = Map(dto);
            measurement.Succeed();
            _logger.LogInformation("Microsoft delegated Graph request completed with status {StatusCode}", (int)response.StatusCode);
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
            throw new IntegrationException(
                "microsoft",
                IntegrationErrorCategory.Upstream,
                "Microsoft Graph returned an invalid response.",
                innerException: exception);
        }
        catch (JsonException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload(exception);
        }
    }

    public async Task<IReadOnlyList<GraphUser>> GetUsersAsync(
        int top,
        CancellationToken cancellationToken)
    {
        if (top is < 1 or > 50)
        {
            throw new IntegrationException(
                "microsoft",
                IntegrationErrorCategory.Validation,
                "top must be between 1 and 50.");
        }

        using var measurement = _telemetry.MeasureRequest("microsoft", "users");
        try
        {
            EnsureHttpsBaseAddress();
            var token = await _tokens.GetApplicationTokenAsync(cancellationToken);
            using var request = CreateRequest($"users?$top={top}&$select={SelectedFields}", token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            EnsureSuccess(response);
            var envelope = await response.Content.ReadFromJsonAsync<GraphUsersEnvelope>(
                cancellationToken: cancellationToken)
                ?? throw InvalidPayload();
            if (envelope.Value is null)
                throw InvalidPayload();
            var users = envelope.Value.Select(Map).ToArray();
            measurement.Succeed();
            _logger.LogInformation(
                "Microsoft application Graph request completed with {UserCount} users",
                users.Length);
            return users;
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
            throw new IntegrationException(
                "microsoft",
                IntegrationErrorCategory.Upstream,
                "Microsoft Graph returned an invalid response.",
                innerException: exception);
        }
        catch (JsonException exception)
        {
            measurement.Fail("upstream");
            throw InvalidPayload(exception);
        }
    }

    private static HttpRequestMessage CreateRequest(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private void EnsureHttpsBaseAddress()
    {
        if (_httpClient.BaseAddress?.Scheme != Uri.UriSchemeHttps)
        {
            throw new IntegrationException(
                "microsoft",
                IntegrationErrorCategory.Configuration,
                "The Microsoft Graph API URL must use HTTPS.");
        }
    }

    private static GraphUser Map(GraphUserDto? dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.DisplayName))
            throw InvalidPayload();
        return new GraphUser(dto.Id, dto.DisplayName, dto.Mail, dto.UserPrincipalName);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var category = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => IntegrationErrorCategory.Authentication,
            HttpStatusCode.Forbidden => IntegrationErrorCategory.Authorization,
            HttpStatusCode.TooManyRequests => IntegrationErrorCategory.Throttled,
            _ => IntegrationErrorCategory.Upstream
        };
        throw new IntegrationException(
            "microsoft",
            category,
            $"Microsoft Graph returned HTTP {(int)response.StatusCode}.",
            category == IntegrationErrorCategory.Throttled
                ? RetryAfterParser.Read(response)
                : null);
    }

    private static IntegrationException InvalidPayload() =>
        new("microsoft", IntegrationErrorCategory.Upstream, "Microsoft Graph returned an invalid response.");

    private static IntegrationException InvalidPayload(Exception exception) =>
        new(
            "microsoft",
            IntegrationErrorCategory.Upstream,
            "Microsoft Graph returned an invalid response.",
            innerException: exception);

    private static IntegrationException Timeout(Exception exception) =>
        new(
            "microsoft",
            IntegrationErrorCategory.Timeout,
            "Microsoft Graph did not respond within the configured timeout.",
            innerException: exception);

    // These are local resilience decisions, not Microsoft HTTP responses. Normalize them so the
    // controller and demo aggregator can still return RFC 7807 or partial success deterministically.
    private static IntegrationException LocallyThrottled(RateLimiterRejectedException exception) =>
        new(
            "microsoft",
            IntegrationErrorCategory.Throttled,
            "The Microsoft Graph request was deferred because local outbound capacity is exhausted.",
            exception.RetryAfter,
            exception);

    private static IntegrationException CircuitOpen(Exception exception) =>
        new(
            "microsoft",
            IntegrationErrorCategory.Upstream,
            "Microsoft Graph is temporarily unavailable because its circuit breaker is open.",
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
