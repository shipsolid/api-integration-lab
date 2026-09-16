namespace ApiIntegrationLab.Api.Integrations.GitHub;

public sealed record GitHubProfile(
    string Login,
    string? DisplayName,
    int PublicRepositoryCount,
    Uri ProfileUrl);

public sealed record GitHubRepository(
    string Name,
    string? Description,
    bool IsPrivate,
    Uri Url,
    DateTimeOffset UpdatedAt);

public sealed record GitHubRateLimit(
    int Limit,
    int Remaining,
    int Used,
    DateTimeOffset ResetAt,
    string Resource);

public sealed record GitHubRepositoryPage(
    IReadOnlyList<GitHubRepository> Repositories,
    int PagesFetched,
    GitHubRateLimit RateLimit);
