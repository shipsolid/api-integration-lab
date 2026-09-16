using System.Net;

namespace ApiIntegrationLab.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<ApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_healthy_without_external_credentials()
    {
        using var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }
}
