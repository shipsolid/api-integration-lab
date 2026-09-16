using System.Net;
using System.Security.Claims;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.IntegrationTests.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.IntegrationTests.Integrations;

public sealed class MicrosoftGraphEndpointTests
{
    [Fact]
    public async Task Me_returns_delegated_profile_for_authenticated_session()
    {
        using var factory = CreateFactory();
        var authentication = factory.Services
            .GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value;
        Assert.Equal(
            TestAuthenticationHandler.AuthenticationScheme,
            authentication.DefaultAuthenticateScheme);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/microsoft/me");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"displayName\":\"Delegated User\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Users_returns_app_only_results_without_using_user_identity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/microsoft/users?top=10");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"displayName\":\"Application User\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Users_rejects_top_outside_documented_bounds()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/microsoft/users?top=51");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new ApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMicrosoftGraphClient>();
                services.AddSingleton<IMicrosoftGraphClient>(new FakeGraphClient());
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
    }

    private sealed class FakeGraphClient : IMicrosoftGraphClient
    {
        public Task<GraphUser> GetMeAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphUser(
                "1",
                "Delegated User",
                null,
                "delegated@example.com"));

        public Task<IReadOnlyList<GraphUser>> GetUsersAsync(
            int top,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<GraphUser> users =
                [new GraphUser("2", "Application User", "app@example.com", "app@example.com")];
            return Task.FromResult(users);
        }
    }
}
