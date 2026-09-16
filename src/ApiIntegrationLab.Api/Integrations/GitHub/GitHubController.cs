using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ApiIntegrationLab.Api.Integrations.GitHub;

[ApiController]
[Route("api/github")]
public sealed class GitHubController : ControllerBase
{
    private readonly IGitHubClient _client;

    public GitHubController(IGitHubClient client)
    {
        _client = client;
    }

    /// <summary>Returns the GitHub user represented by the configured bearer token.</summary>
    /// <remarks>
    /// With no GitHub__Token the request intentionally has no Authorization header and demonstrates
    /// the provider's 401. Configure a PAT and repeat to see the authenticated 200 response.
    /// </remarks>
    [HttpGet("profile")]
    [ProducesResponseType<GitHubProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GitHubProfile>> GetProfile(CancellationToken cancellationToken) =>
        Ok(await _client.GetProfileAsync(cancellationToken));

    /// <summary>Returns bounded GitHub repository pages and rate-limit metadata.</summary>
    /// <remarks>Pagination follows GitHub's rel="next" link and never exceeds maxPages.</remarks>
    [HttpGet("repos")]
    [ProducesResponseType<GitHubRepositoryPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GitHubRepositoryPage>> GetRepositories(
        [FromQuery, Range(1, 100)] int perPage = 30,
        [FromQuery, Range(1, 10)] int maxPages = 3,
        CancellationToken cancellationToken = default) =>
        Ok(await _client.GetRepositoriesAsync(perPage, maxPages, cancellationToken));

    /// <summary>Returns the authenticated token's GitHub core rate-limit budget.</summary>
    /// <remarks>
    /// Configure GitHub__Token first. The response normalizes limit, used, remaining, reset time,
    /// and resource headers so throttling can be understood without inspecting raw provider headers.
    /// </remarks>
    [HttpGet("rate-limit")]
    [ProducesResponseType<GitHubRateLimit>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GitHubRateLimit>> GetRateLimit(CancellationToken cancellationToken) =>
        Ok(await _client.GetRateLimitAsync(cancellationToken));
}
