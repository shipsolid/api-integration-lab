using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class GitHubClientPayloadTests
{
    [Fact]
    public async Task Profile_rejects_payload_missing_required_login()
    {
        var handler = StubHttpMessageHandler.Json(
            "{\"name\":\"Demo\",\"public_repos\":1,\"html_url\":\"https://github.com/demo\"}");
        var client = new GitHubClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            new ApiTelemetry(),
            NullLogger<GitHubClient>.Instance);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetProfileAsync(CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }
}
