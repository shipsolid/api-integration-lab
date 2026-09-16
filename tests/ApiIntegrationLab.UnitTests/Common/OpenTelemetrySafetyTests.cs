using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.UnitTests.Common;

public sealed class OpenTelemetrySafetyTests
{
    [Fact]
    public async Task Integration_activity_contains_bounded_context_but_not_authorization_or_token()
    {
        const string markerToken = "never-export-this-token";
        using var listener = TelemetryTestListener.ListenTo(ApiTelemetry.ActivitySourceName);
        var capture = StubHttpMessageHandler.Json(
            "{\"login\":\"demo\",\"name\":\"Demo\",\"public_repos\":1,\"html_url\":\"https://github.com/demo\"}");
        var authentication = new GitHubAuthenticationHandler(Options.Create(new GitHubOptions
        {
            Token = markerToken,
            UserAgent = "api-integration-lab-tests",
            ApiVersion = "2022-11-28"
        }))
        {
            InnerHandler = capture
        };
        var client = new GitHubClient(
            new HttpClient(authentication) { BaseAddress = new Uri("https://api.github.com/") },
            new ApiTelemetry(),
            NullLogger<GitHubClient>.Instance);

        await client.GetProfileAsync(CancellationToken.None);

        Assert.Contains("provider=github", listener.SerializedActivities, StringComparison.Ordinal);
        Assert.Contains("operation=profile", listener.SerializedActivities, StringComparison.Ordinal);
        Assert.DoesNotContain(markerToken, listener.SerializedActivities, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization", listener.SerializedActivities, StringComparison.OrdinalIgnoreCase);
    }
}
