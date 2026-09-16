using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Integrations.BasicAuth;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class BasicAuthenticationHandlerTests
{
    [Fact]
    public async Task Handler_sends_expected_basic_authorization_header()
    {
        var capture = StubHttpMessageHandler.Json("{\"authenticated\":true}");
        var handler = new BasicAuthenticationHandler(Options.Create(new BasicAuthOptions
        {
            Username = "demo",
            Password = "s3cret"
        }))
        {
            InnerHandler = capture
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://postman-echo.com/basic-auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Basic", capture.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("demo:s3cret")),
            capture.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Handler_rejects_missing_password_before_network_call()
    {
        var handler = new BasicAuthenticationHandler(Options.Create(new BasicAuthOptions
        {
            Username = "demo",
            Password = ""
        }))
        {
            InnerHandler = StubHttpMessageHandler.Unused()
        };
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetAsync("https://postman-echo.com/basic-auth"));

        Assert.Equal(IntegrationErrorCategory.Configuration, error.Category);
        Assert.Contains("BasicAuth:Password", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_rejects_plaintext_http_before_attaching_credentials()
    {
        var handler = new BasicAuthenticationHandler(Options.Create(new BasicAuthOptions
        {
            Username = "demo",
            Password = "s3cret"
        }))
        {
            InnerHandler = StubHttpMessageHandler.Unused()
        };
        using var client = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetAsync("http://postman-echo.com/basic-auth"));

        Assert.Equal(IntegrationErrorCategory.Configuration, error.Category);
    }
}
