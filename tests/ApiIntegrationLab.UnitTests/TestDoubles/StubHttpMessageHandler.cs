using System.Net;
using System.Text;

namespace ApiIntegrationLab.UnitTests.TestDoubles;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        _send = send;
    }

    public int CallCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return _send(request, cancellationToken);
    }

    public static StubHttpMessageHandler Json(
        string json,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        return new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
    }

    public static StubHttpMessageHandler Unused()
    {
        return new StubHttpMessageHandler((_, _) =>
            throw new Xunit.Sdk.XunitException("The HTTP handler must not be called."));
    }
}
