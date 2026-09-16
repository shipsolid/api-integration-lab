namespace ApiIntegrationLab.Api.Integrations.GitHub;

public interface IGitHubClient
{
    Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken);

    Task<GitHubRepositoryPage> GetRepositoriesAsync(
        int perPage,
        int maxPages,
        CancellationToken cancellationToken);

    Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken);
}
