using System.Net;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class GitHubAuthenticationHandlerTests
{
    [Fact]
    public async Task Handler_omits_authorization_when_token_is_absent()
    {
        var capture = StubHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized);
        var handler = CreateHandler(token: null, capture);
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://api.github.com/user");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(capture.LastRequest!.Headers.Authorization);
        Assert.NotEmpty(capture.LastRequest.Headers.UserAgent);
        Assert.True(capture.LastRequest.Headers.Contains("X-GitHub-Api-Version"));
        Assert.True(capture.LastRequest.Headers.Contains("Accept"));
    }

    [Fact]
    public async Task Handler_sends_configured_token_as_bearer()
    {
        var capture = StubHttpMessageHandler.Json("{}");
        var handler = CreateHandler("github-token", capture);
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://api.github.com/user");

        Assert.Equal("Bearer", capture.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("github-token", capture.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Handler_rejects_plaintext_http_before_attaching_token()
    {
        var handler = CreateHandler("github-token", StubHttpMessageHandler.Unused());
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetAsync("http://api.github.com/user"));

        Assert.Equal(IntegrationErrorCategory.Configuration, error.Category);
    }

    private static GitHubAuthenticationHandler CreateHandler(
        string? token,
        HttpMessageHandler innerHandler)
    {
        return new GitHubAuthenticationHandler(Options.Create(new GitHubOptions
        {
            Token = token,
            UserAgent = "api-integration-lab-tests",
            ApiVersion = "2022-11-28"
        }))
        {
            InnerHandler = innerHandler
        };
    }
}
