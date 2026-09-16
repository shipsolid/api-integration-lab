using System.Text.Json.Serialization;

namespace ApiIntegrationLab.Api.Integrations.GitHub;

internal sealed record GitHubProfileDto(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("public_repos")] int PublicRepositories,
    [property: JsonPropertyName("html_url")] string HtmlUrl);

internal sealed record GitHubRepositoryDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("private")] bool IsPrivate,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

internal sealed record GitHubRateLimitEnvelope(
    [property: JsonPropertyName("resources")] GitHubRateLimitResources? Resources);

internal sealed record GitHubRateLimitResources(
    [property: JsonPropertyName("core")] GitHubRateLimitDto? Core);

internal sealed record GitHubRateLimitDto(
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("remaining")] int Remaining,
    [property: JsonPropertyName("used")] int Used,
    [property: JsonPropertyName("reset")] long Reset);
