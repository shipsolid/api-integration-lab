using System.Net;
using ApiIntegrationLab.Api.Integrations.PublicApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiIntegrationLab.IntegrationTests.Integrations;

public sealed class PublicApiEndpointTests
{
    [Fact]
    public async Task Posts_returns_normalized_public_data()
    {
        using var factory = CreateFactory(new FakePublicApiClient());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/posts?limit=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"id\":42", body, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Public demo\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Posts_rejects_limit_outside_documented_bounds()
    {
        using var factory = CreateFactory(new FakePublicApiClient());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/public/posts?limit=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    private static WebApplicationFactory<Program> CreateFactory(IPublicApiClient client)
    {
        return new ApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPublicApiClient>();
                services.AddSingleton(client);
            });
        });
    }

    private sealed class FakePublicApiClient : IPublicApiClient
    {
        public Task<IReadOnlyList<PublicPost>> GetPostsAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<PublicPost> posts =
                [new PublicPost(42, "Public demo", "No credentials required")];
            return Task.FromResult(posts);
        }
    }
}
