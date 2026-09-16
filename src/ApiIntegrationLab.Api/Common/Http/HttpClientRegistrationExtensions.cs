using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace ApiIntegrationLab.Api.Common.Http;

public static class HttpClientRegistrationExtensions
{
    public static IHttpClientBuilder AddIntegrationResilience(this IHttpClientBuilder builder)
    {
        builder.ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);
        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(250);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;

            // The standard predicate covers timeouts, 408, 429, and 5xx. Authentication and
            // validation failures are deliberately excluded because another attempt cannot fix them.
        });

        return builder;
    }
}
