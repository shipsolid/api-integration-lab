namespace ApiIntegrationLab.Api.Integrations.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string BaseUrl { get; init; } = "https://api.github.com/";
    public string? Token { get; init; }
    public string UserAgent { get; init; } = "api-integration-lab";
    public string ApiVersion { get; init; } = "2022-11-28";
}
