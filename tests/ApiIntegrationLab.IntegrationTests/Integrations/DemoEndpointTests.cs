using System.Net;
using System.Security.Claims;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Models;
using ApiIntegrationLab.Api.Integrations.Demo;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.Api.Integrations.PublicApi;
using ApiIntegrationLab.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.IntegrationTests.Integrations;

public sealed class DemoEndpointTests
{
    [Fact]
    public async Task Demo_requires_an_authenticated_session()
    {
        using var factory = new ApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/demo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Demo_returns_partial_success_as_200()
    {
        using var factory = CreateAuthenticatedFactory(PartialSuccess());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/demo");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"hasAnySuccess\":true", body, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"failed\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Demo_maps_all_provider_failures_to_problem_details()
    {
        using var factory = CreateAuthenticatedFactory(AllFailed());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/demo");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("All demo integrations failed.", body, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateAuthenticatedFactory(DemoResponse response) =>
        new ApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDemoAggregator>();
                services.AddSingleton<IDemoAggregator>(new FakeAggregator(response));
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.AuthenticationScheme,
                        _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = null;
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                });
            }));

    private static DemoResponse PartialSuccess()
    {
        var failure = new IntegrationException(
            "github",
            IntegrationErrorCategory.Authentication,
            "GitHub rejected the token.");
        return new DemoResponse(
            IntegrationResult<IReadOnlyList<PublicPost>>.Success([new PublicPost(1, "Public", "Body")]),
            IntegrationResult<GitHubRepositoryPage>.Failure(failure),
            IntegrationResult<GraphUser>.Success(new GraphUser("1", "User", null, null)));
    }

    private static DemoResponse AllFailed()
    {
        var failure = new IntegrationException(
            "provider",
            IntegrationErrorCategory.Upstream,
            "Provider unavailable.");
        return new DemoResponse(
            IntegrationResult<IReadOnlyList<PublicPost>>.Failure(failure),
            IntegrationResult<GitHubRepositoryPage>.Failure(failure),
            IntegrationResult<GraphUser>.Failure(failure));
    }

    private sealed class FakeAggregator : IDemoAggregator
    {
        private readonly DemoResponse _response;

        public FakeAggregator(DemoResponse response)
        {
            _response = response;
        }

        public Task<DemoResponse> GetAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }
}
