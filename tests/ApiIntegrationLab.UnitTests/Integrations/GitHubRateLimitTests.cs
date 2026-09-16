using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class GitHubRateLimitTests
{
    [Fact]
    public async Task RateLimit_maps_core_resource_payload()
    {
        var handler = StubHttpMessageHandler.Json("""
            {
              "resources": {
                "core": {"limit":5000,"remaining":4997,"reset":1789516800,"used":3}
              }
            }
            """);
        var client = new GitHubClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            new ApiTelemetry(),
            NullLogger<GitHubClient>.Instance);

        var rateLimit = await client.GetRateLimitAsync(CancellationToken.None);

        Assert.Equal(5000, rateLimit.Limit);
        Assert.Equal(4997, rateLimit.Remaining);
        Assert.Equal(3, rateLimit.Used);
        Assert.Equal("core", rateLimit.Resource);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1789516800), rateLimit.ResetAt);
    }

    [Fact]
    public async Task RateLimit_rejects_payload_missing_core_resource()
    {
        var handler = StubHttpMessageHandler.Json("{\"resources\":{}}");
        var client = new GitHubClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            new ApiTelemetry(),
            NullLogger<GitHubClient>.Instance);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetRateLimitAsync(CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }
}
