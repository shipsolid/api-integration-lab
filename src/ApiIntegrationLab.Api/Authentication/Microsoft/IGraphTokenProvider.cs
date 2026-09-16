using System.Security.Claims;

namespace ApiIntegrationLab.Api.Authentication.Microsoft;

public interface IGraphTokenProvider
{
    Task<string> GetDelegatedTokenAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken);

    Task<string> GetApplicationTokenAsync(CancellationToken cancellationToken);
}
