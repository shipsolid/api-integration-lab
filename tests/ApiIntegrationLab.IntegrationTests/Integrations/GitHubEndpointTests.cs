using System.Net;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Integrations.GitHub;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiIntegrationLab.IntegrationTests.Integrations;

public sealed class GitHubEndpointTests
{
    [Fact]
    public async Task Profile_returns_normalized_authenticated_user()
    {
        using var factory = CreateFactory(new SuccessfulGitHubClient());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/github/profile");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"login\":\"demo-user\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Profile_preserves_authentication_failure_for_no_token_demo()
    {
        using var factory = CreateFactory(new FailingGitHubClient());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/github/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Repositories_reject_query_outside_safety_bounds()
    {
        using var factory = CreateFactory(new SuccessfulGitHubClient());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/github/repos?perPage=101&maxPages=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(IGitHubClient githubClient)
    {
        return new ApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGitHubClient>();
                services.AddSingleton(githubClient);
            }));
    }

    private sealed class SuccessfulGitHubClient : IGitHubClient
    {
        private static readonly GitHubRateLimit RateLimit =
            new(5000, 4999, 1, DateTimeOffset.FromUnixTimeSeconds(1789516800), "core");

        public Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new GitHubProfile(
                "demo-user",
                "Demo User",
                1,
                new Uri("https://github.com/demo-user")));

        public Task<GitHubRepositoryPage> GetRepositoriesAsync(
            int perPage,
            int maxPages,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GitHubRepositoryPage([], 1, RateLimit));

        public Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken) =>
            Task.FromResult(RateLimit);
    }

    private sealed class FailingGitHubClient : IGitHubClient
    {
        public Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken) =>
            throw new IntegrationException(
                "github",
                IntegrationErrorCategory.Authentication,
                "GitHub rejected the request. Configure GitHub:Token and retry.");

        public Task<GitHubRepositoryPage> GetRepositoriesAsync(
            int perPage,
            int maxPages,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
