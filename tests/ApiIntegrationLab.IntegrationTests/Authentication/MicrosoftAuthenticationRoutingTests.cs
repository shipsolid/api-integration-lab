using System.Net;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ApiIntegrationLab.IntegrationTests.Authentication;

public sealed class MicrosoftAuthenticationRoutingTests
{
    [Fact]
    public async Task Configured_microsoft_auth_redirects_only_the_explicit_login_route()
    {
        using var factory = new ApplicationFactory().WithWebHostBuilder(builder =>
        {
            // Host settings are visible while top-level Program code chooses which schemes to
            // register; ConfigureAppConfiguration runs too late for that conditional registration.
            builder.UseSetting("AzureAd:TenantId", "11111111-1111-1111-1111-111111111111");
            builder.UseSetting("AzureAd:ClientId", "22222222-2222-2222-2222-222222222222");
            builder.UseSetting("AzureAd:ClientSecret", "test-only-client-secret");
            builder.ConfigureServices(services =>
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    options =>
                    {
                        var configuration = new OpenIdConnectConfiguration
                        {
                            AuthorizationEndpoint = "https://login.microsoftonline.com/test/oauth2/v2.0/authorize",
                            TokenEndpoint = "https://login.microsoftonline.com/test/oauth2/v2.0/token",
                            EndSessionEndpoint = "https://login.microsoftonline.com/test/oauth2/v2.0/logout",
                            Issuer = "https://login.microsoftonline.com/test/v2.0"
                        };
                        options.Configuration = configuration;
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                    }));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var protectedApi = await client.GetAsync("/api/microsoft/me");
        using var login = await client.GetAsync("/api/microsoft/login");

        Assert.Equal(HttpStatusCode.Unauthorized, protectedApi.StatusCode);
        Assert.Null(protectedApi.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("login.microsoftonline.com", login.Headers.Location?.Host);
    }
}
