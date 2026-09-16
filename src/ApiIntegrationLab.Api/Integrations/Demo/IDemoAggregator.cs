using System.Security.Claims;

namespace ApiIntegrationLab.Api.Integrations.Demo;

public interface IDemoAggregator
{
    Task<DemoResponse> GetAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}
