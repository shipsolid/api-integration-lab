using ApiIntegrationLab.Api.Common.Errors;

namespace ApiIntegrationLab.Api.Common.Models;

public sealed record IntegrationError(string Provider, string Category, string Message);

public sealed record IntegrationResult<T>(string Status, T? Data, IntegrationError? Error)
{
    public static IntegrationResult<T> Success(T data)
    {
        return new IntegrationResult<T>("success", data, null);
    }

    public static IntegrationResult<T> Failure(IntegrationException error)
    {
        return new IntegrationResult<T>(
            "failed",
            default,
            new IntegrationError(
                error.Provider,
                error.Category.ToString().ToLowerInvariant(),
                error.Message));
    }
}
