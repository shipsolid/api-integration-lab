using System.Net;
using ApiIntegrationLab.Api.Common.Http;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace ApiIntegrationLab.UnitTests.Common;

public sealed class HttpClientRegistrationExtensionsTests
{
    [Fact]
    public async Task Resilience_pipeline_retries_transient_5xx_until_success()
    {
        var call = 0;
        HttpStatusCode handlerCallStatus() => ++call < 3
            ? HttpStatusCode.ServiceUnavailable
            : HttpStatusCode.OK;
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(handlerCallStatus())));
        using var provider = BuildProvider(handler);

        using var response = await provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("test")
            .GetAsync("resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Resilience_pipeline_does_not_retry_authentication_failure()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var provider = BuildProvider(handler);

        using var response = await provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("test")
            .GetAsync("resource");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    private static ServiceProvider BuildProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddHttpClient("test", client =>
                client.BaseAddress = new Uri("https://provider.example/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddIntegrationResilience();
        return services.BuildServiceProvider();
    }
}
