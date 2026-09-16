using System.Net;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.BasicAuth;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class BasicAuthClientTests
{
    [Fact]
    public async Task GetProfile_returns_normalized_authentication_state()
    {
        var client = CreateClient(StubHttpMessageHandler.Json("{\"authenticated\":true}"));

        var profile = await client.GetProfileAsync(CancellationToken.None);

        Assert.Equal("postman-echo", profile.Provider);
        Assert.True(profile.Authenticated);
    }

    [Fact]
    public async Task GetProfile_maps_unauthorized_without_exposing_body()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(
            "credential-marker",
            HttpStatusCode.Unauthorized));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetProfileAsync(CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Authentication, error.Category);
        Assert.DoesNotContain("credential-marker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProfile_maps_transport_failure_to_safe_upstream_error()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("credential-marker"))));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetProfileAsync(CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.DoesNotContain("credential-marker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProfile_rejects_payload_missing_authenticated_field()
    {
        var client = CreateClient(StubHttpMessageHandler.Json("{}"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetProfileAsync(CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }

    private static BasicAuthClient CreateClient(HttpMessageHandler handler)
    {
        return new BasicAuthClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postman-echo.com/") },
            new ApiTelemetry(),
            NullLogger<BasicAuthClient>.Instance);
    }
}
