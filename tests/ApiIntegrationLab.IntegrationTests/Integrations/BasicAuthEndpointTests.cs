using System.Net;
using ApiIntegrationLab.Api.Integrations.BasicAuth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiIntegrationLab.IntegrationTests.Integrations;

public sealed class BasicAuthEndpointTests
{
    [Fact]
    public async Task Profile_returns_normalized_result_without_credentials()
    {
        using var factory = CreateFactory(new SuccessfulBasicClient());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/basic/profile");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"authenticated\":true", body, StringComparison.Ordinal);
        Assert.DoesNotContain("username", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Profile_reports_missing_credentials_without_crashing_host()
    {
        using var factory = new ApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/basic/profile");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("BasicAuth:Username", body, StringComparison.Ordinal);
        Assert.Contains("BasicAuth:Password", body, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(IBasicAuthClient basicClient)
    {
        return new ApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBasicAuthClient>();
                services.AddSingleton(basicClient);
            }));
    }

    private sealed class SuccessfulBasicClient : IBasicAuthClient
    {
        public Task<BasicAuthProfile> GetProfileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BasicAuthProfile("postman-echo", true));
        }
    }
}
