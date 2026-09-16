using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ApiIntegrationLab.Api.Integrations.PublicApi;

[ApiController]
[Route("api/public")]
public sealed class PublicApiController : ControllerBase
{
    private readonly IPublicApiClient _client;

    public PublicApiController(IPublicApiClient client)
    {
        _client = client;
    }

    /// <summary>Calls a public REST API without authentication.</summary>
    /// <remarks>
    /// This is the no-auth baseline: the outbound request has no Authorization header. The provider
    /// response is deserialized and normalized before it is returned.
    /// </remarks>
    [HttpGet("posts")]
    [ProducesResponseType<IReadOnlyList<PublicPost>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PublicPost>>> GetPosts(
        [FromQuery, Range(1, 100)] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var posts = await _client.GetPostsAsync(limit, cancellationToken);
        return Ok(posts);
    }
}
