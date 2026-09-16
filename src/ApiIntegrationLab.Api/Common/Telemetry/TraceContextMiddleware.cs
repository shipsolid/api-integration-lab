using System.Diagnostics;

namespace ApiIntegrationLab.Api.Common.Telemetry;

public sealed class TraceContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TraceContextMiddleware> _logger;

    public TraceContextMiddleware(RequestDelegate next, ILogger<TraceContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        // Correlation belongs in a log scope, not a metric label. Never add user identity, request
        // bodies, query strings, provider tokens, or signatures to telemetry dimensions.
        using (_logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId }))
            await _next(context);
    }
}
