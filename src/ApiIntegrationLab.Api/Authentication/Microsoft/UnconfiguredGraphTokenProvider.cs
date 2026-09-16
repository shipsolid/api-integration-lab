using System.Security.Claims;
using ApiIntegrationLab.Api.Common.Errors;

namespace ApiIntegrationLab.Api.Authentication.Microsoft;

public sealed class UnconfiguredGraphTokenProvider : IGraphTokenProvider
{
    public Task<string> GetDelegatedTokenAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        Task.FromException<string>(MissingConfiguration());

    public Task<string> GetApplicationTokenAsync(CancellationToken cancellationToken) =>
        Task.FromException<string>(MissingConfiguration());

    private static IntegrationException MissingConfiguration() =>
        new(
            "microsoft",
            IntegrationErrorCategory.Configuration,
            "Microsoft integration is not configured. Set AzureAd:TenantId, ClientId, and ClientSecret.");
}
