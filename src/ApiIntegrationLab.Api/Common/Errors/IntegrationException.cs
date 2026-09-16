namespace ApiIntegrationLab.Api.Common.Errors;

public sealed class IntegrationException : Exception
{
    public IntegrationException(
        string provider,
        IntegrationErrorCategory category,
        string safeMessage,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        Provider = provider;
        Category = category;
        RetryAfter = retryAfter;
    }

    public string Provider { get; }
    public IntegrationErrorCategory Category { get; }
    public TimeSpan? RetryAfter { get; }
    public int StatusCode => StatusFor(Category);

    public static int StatusFor(IntegrationErrorCategory category)
    {
        return category switch
        {
            IntegrationErrorCategory.Authentication => StatusCodes.Status401Unauthorized,
            IntegrationErrorCategory.Authorization => StatusCodes.Status403Forbidden,
            IntegrationErrorCategory.Throttled => StatusCodes.Status429TooManyRequests,
            IntegrationErrorCategory.Timeout => StatusCodes.Status504GatewayTimeout,
            IntegrationErrorCategory.Configuration => StatusCodes.Status503ServiceUnavailable,
            IntegrationErrorCategory.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status502BadGateway
        };
    }
}
