using Microsoft.AspNetCore.Mvc;

namespace ApiIntegrationLab.Api.Integrations.BasicAuth;

[ApiController]
[Route("api/basic")]
public sealed class BasicAuthController : ControllerBase
{
    private readonly IBasicAuthClient _client;

    public BasicAuthController(IBasicAuthClient client)
    {
        _client = client;
    }

    /// <summary>Calls an external API using HTTP Basic Authentication.</summary>
    /// <remarks>
    /// Configure BasicAuth__Username and BasicAuth__Password. The client sends username:password as
    /// Base64 in the Authorization header over HTTPS; neither credential is returned or logged.
    /// </remarks>
    [HttpGet("profile")]
    [ProducesResponseType<BasicAuthProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<BasicAuthProfile>> GetProfile(
        CancellationToken cancellationToken)
    {
        return Ok(await _client.GetProfileAsync(cancellationToken));
    }
}
