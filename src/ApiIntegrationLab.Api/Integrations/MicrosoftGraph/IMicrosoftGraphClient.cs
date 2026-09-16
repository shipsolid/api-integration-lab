using System.Security.Claims;

namespace ApiIntegrationLab.Api.Integrations.MicrosoftGraph;

public interface IMicrosoftGraphClient
{
    Task<GraphUser> GetMeAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GraphUser>> GetUsersAsync(
        int top,
        CancellationToken cancellationToken);
}
