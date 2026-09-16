using System.Net;
using System.Net.Http.Headers;
using ApiIntegrationLab.Api.Common.Http;

namespace ApiIntegrationLab.UnitTests.Common;

public sealed class RetryAfterParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Read_returns_delta_value()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(15));

        Assert.Equal(TimeSpan.FromSeconds(15), RetryAfterParser.Read(response, Now));
    }

    [Fact]
    public void Read_converts_http_date_to_nonnegative_delay()
    {
        using var future = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        future.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(30));
        using var past = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        past.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(-30));

        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterParser.Read(future, Now));
        Assert.Equal(TimeSpan.Zero, RetryAfterParser.Read(past, Now));
    }
}
