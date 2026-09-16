using System.Net;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.PublicApi;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class PublicApiClientTests
{
    [Fact]
    public async Task GetPosts_maps_and_limits_provider_payload()
    {
        var handler = StubHttpMessageHandler.Json("""
            [
              {"userId":1,"id":10,"title":"A title","body":"A body"},
              {"userId":1,"id":11,"title":"Second","body":"Second body"}
            ]
            """);
        var client = CreateClient(handler);

        var posts = await client.GetPostsAsync(1, CancellationToken.None);

        var post = Assert.Single(posts);
        Assert.Equal(10, post.Id);
        Assert.Equal("A title", post.Title);
        Assert.Equal("A body", post.BodyPreview);
        Assert.Equal("/posts", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Null(handler.LastRequest.Headers.Authorization);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetPosts_rejects_unbounded_limits_before_http(int limit)
    {
        var client = CreateClient(StubHttpMessageHandler.Unused());

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(limit, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Validation, error.Category);
    }

    [Fact]
    public async Task GetPosts_maps_upstream_failure_without_copying_body()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(
            "provider-secret-details",
            HttpStatusCode.InternalServerError));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.DoesNotContain("provider-secret-details", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPosts_maps_transport_failure_to_safe_upstream_error()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("dns-secret-marker"))));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.DoesNotContain("dns-secret-marker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPosts_maps_malformed_json_to_safe_upstream_error()
    {
        var client = CreateClient(StubHttpMessageHandler.Json("not-json-secret-marker"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.DoesNotContain("not-json-secret-marker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPosts_rejects_payload_missing_required_fields()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(
            "[{\"userId\":1,\"id\":10,\"title\":\"A title\"}]"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }

    [Fact]
    public async Task GetPosts_rejects_null_collection_entries()
    {
        var client = CreateClient(StubHttpMessageHandler.Json("[null]"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }

    [Fact]
    public async Task GetPosts_maps_resilience_timeout_to_timeout_error()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TimeoutRejectedException("timeout"))));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Timeout, error.Category);
    }

    [Fact]
    public async Task GetPosts_maps_open_circuit_to_safe_upstream_error()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new BrokenCircuitException("open"))));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }

    [Fact]
    public async Task GetPosts_maps_local_rate_limit_rejection_to_throttled_error()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new RateLimiterRejectedException(
                "busy",
                TimeSpan.FromSeconds(4)))));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Throttled, error.Category);
        Assert.Equal(TimeSpan.FromSeconds(4), error.RetryAfter);
    }

    [Fact]
    public async Task GetPosts_preserves_retry_after_from_final_throttled_response()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromSeconds(17));
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(response)));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetPostsAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Throttled, error.Category);
        Assert.Equal(TimeSpan.FromSeconds(17), error.RetryAfter);
    }

    [Fact]
    public async Task GetPosts_propagates_caller_cancellation_and_records_cancelled_outcome()
    {
        using var activities = TelemetryTestListener.ListenTo(ApiTelemetry.ActivitySourceName);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = CreateClient(new StubHttpMessageHandler((_, token) =>
            Task.FromCanceled<HttpResponseMessage>(token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetPostsAsync(10, cancellation.Token));

        // Other xUnit classes run in parallel and can emit their own integration spans. Isolate the
        // cancelled span before checking that this request was not misclassified as an upstream error.
        var cancelledActivity = Assert.Single(
            activities.SerializedActivities.Split('\n'),
            activity => activity.Contains("outcome=cancelled", StringComparison.Ordinal));
        Assert.DoesNotContain("error.type=upstream", cancelledActivity, StringComparison.Ordinal);
    }

    private static PublicApiClient CreateClient(HttpMessageHandler handler)
    {
        return new PublicApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://jsonplaceholder.typicode.com/") },
            new ApiTelemetry(),
            NullLogger<PublicApiClient>.Instance);
    }
}
