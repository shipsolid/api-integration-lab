namespace ApiIntegrationLab.Api.Integrations.PublicApi;

public interface IPublicApiClient
{
    Task<IReadOnlyList<PublicPost>> GetPostsAsync(
        int limit,
        CancellationToken cancellationToken);
}
