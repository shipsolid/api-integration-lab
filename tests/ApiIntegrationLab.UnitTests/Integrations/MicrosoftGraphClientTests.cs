using System.Security.Claims;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class MicrosoftGraphClientTests
{
    [Fact]
    public async Task GetMe_uses_delegated_token_and_me_resource()
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var capture = StubHttpMessageHandler.Json(
            "{\"id\":\"1\",\"displayName\":\"Demo User\",\"mail\":null,\"userPrincipalName\":\"demo@example.com\"}");
        var client = CreateClient(tokens, capture);

        var result = await client.GetMeAsync(
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            CancellationToken.None);

        Assert.Equal("Demo User", result.DisplayName);
        Assert.Equal(1, tokens.DelegatedCalls);
        Assert.Equal(0, tokens.ApplicationCalls);
        Assert.Equal("user-token", capture.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Equal("/v1.0/me", capture.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("$select=id,displayName,mail,userPrincipalName", capture.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetUsers_uses_application_token_and_bounded_top()
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var capture = StubHttpMessageHandler.Json("""
            {"value":[{"id":"2","displayName":"App Visible User","mail":"user@example.com",
                       "userPrincipalName":"user@example.com"}]}
            """);
        var client = CreateClient(tokens, capture);

        var users = await client.GetUsersAsync(10, CancellationToken.None);

        Assert.Single(users);
        Assert.Equal(0, tokens.DelegatedCalls);
        Assert.Equal(1, tokens.ApplicationCalls);
        Assert.Equal("app-token", capture.LastRequest!.Headers.Authorization!.Parameter);
        Assert.Contains("$top=10", capture.LastRequest.RequestUri!.Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetUsers_rejects_unbounded_top_before_token_or_http(int top)
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var client = CreateClient(tokens, StubHttpMessageHandler.Unused());

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetUsersAsync(top, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Validation, error.Category);
        Assert.Equal(0, tokens.ApplicationCalls);
    }

    [Fact]
    public async Task GetMe_rejects_invalid_provider_payload_without_exposing_it()
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var client = CreateClient(tokens, StubHttpMessageHandler.Json("tenant-secret-marker"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetMeAsync(new ClaimsPrincipal(), CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.DoesNotContain("tenant-secret-marker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMe_maps_resilience_timeout_to_timeout_error()
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var client = CreateClient(tokens, new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TimeoutRejectedException("timeout"))));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetMeAsync(new ClaimsPrincipal(), CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Timeout, error.Category);
    }

    [Fact]
    public async Task GetMe_rejects_plaintext_graph_base_url_before_requesting_a_token()
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var client = new MicrosoftGraphClient(
            new HttpClient(StubHttpMessageHandler.Unused())
            {
                BaseAddress = new Uri("http://graph.microsoft.com/v1.0/")
            },
            tokens,
            new ApiTelemetry(),
            NullLogger<MicrosoftGraphClient>.Instance);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetMeAsync(new ClaimsPrincipal(), CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Configuration, error.Category);
        Assert.Equal(0, tokens.DelegatedCalls);
    }

    [Fact]
    public async Task GetUsers_rejects_null_value_collection()
    {
        var tokens = new RecordingGraphTokenProvider("user-token", "app-token");
        var client = CreateClient(tokens, StubHttpMessageHandler.Json("{\"value\":null}"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetUsersAsync(10, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }

    private static MicrosoftGraphClient CreateClient(
        RecordingGraphTokenProvider tokens,
        HttpMessageHandler handler)
    {
        return new MicrosoftGraphClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") },
            tokens,
            new ApiTelemetry(),
            NullLogger<MicrosoftGraphClient>.Instance);
    }
}
