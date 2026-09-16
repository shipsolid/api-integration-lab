using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Diagnostics;

namespace ApiIntegrationLab.Api.Common.Errors;

public sealed class IntegrationExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not IntegrationException integrationException)
            return false;

        httpContext.Response.StatusCode = integrationException.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";

        if (integrationException.RetryAfter is { } retryAfter)
        {
            httpContext.Response.Headers.RetryAfter =
                Math.Max(0, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        var problem = new ProblemDetails
        {
            Status = integrationException.StatusCode,
            Title = "Integration request failed",
            Detail = integrationException.Message,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["provider"] = integrationException.Provider;
        problem.Extensions["category"] = integrationException.Category.ToString().ToLowerInvariant();
        // Match Problem Details to the distributed trace so Grafana can move directly from the
        // client-visible failure to its server and outbound spans.
        problem.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        if (integrationException.RetryAfter is { } delay)
            problem.Extensions["retryAfterSeconds"] = Math.Max(0, (int)Math.Ceiling(delay.TotalSeconds));

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            JsonOptions,
            "application/problem+json",
            cancellationToken);
        return true;
    }
}
