namespace ApiIntegrationLab.Api.Integrations.BasicAuth;

public interface IBasicAuthClient
{
    Task<BasicAuthProfile> GetProfileAsync(CancellationToken cancellationToken);
}
