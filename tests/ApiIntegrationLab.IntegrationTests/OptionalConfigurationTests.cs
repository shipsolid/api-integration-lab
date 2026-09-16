using System.Net;
using Microsoft.Extensions.Configuration;

namespace ApiIntegrationLab.IntegrationTests;

public sealed class OptionalConfigurationTests
{
    [Fact]
    public async Task Optional_credentials_do_not_break_health_or_swagger_and_never_leak()
    {
        const string markerSecret = "must-not-appear-marker-secret";
        using var factory = new ApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GitHub:Token"] = markerSecret,
                    ["BasicAuth:Username"] = "",
                    ["BasicAuth:Password"] = markerSecret,
                    ["AzureAd:TenantId"] = "",
                    ["AzureAd:ClientId"] = "",
                    ["AzureAd:ClientSecret"] = markerSecret
                })));
        using var client = factory.CreateClient();

        using var health = await client.GetAsync("/health");
        var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var basic = await client.GetAsync("/api/basic/profile");
        var error = await basic.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.DoesNotContain(markerSecret, swagger, StringComparison.Ordinal);
        Assert.DoesNotContain(markerSecret, error, StringComparison.Ordinal);
    }
}
