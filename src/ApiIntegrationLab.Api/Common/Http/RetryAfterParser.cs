namespace ApiIntegrationLab.Api.Common.Http;

public static class RetryAfterParser
{
    public static TimeSpan? Read(
        HttpResponseMessage response,
        DateTimeOffset? now = null)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;

        if (response.Headers.RetryAfter?.Date is not { } retryAt)
            return null;

        // HTTP-date Retry-After is an absolute instant. Clamp stale or skewed provider values to
        // zero so callers never receive a negative delay or an invalid response header.
        var delay = retryAt - (now ?? DateTimeOffset.UtcNow);
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }
}
