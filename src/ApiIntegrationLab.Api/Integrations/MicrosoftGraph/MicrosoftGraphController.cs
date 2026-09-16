using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiIntegrationLab.Api.Integrations.MicrosoftGraph;

[ApiController]
[Route("api/microsoft")]
public sealed class MicrosoftGraphController : ControllerBase
{
    private readonly IMicrosoftGraphClient _client;

    public MicrosoftGraphController(IMicrosoftGraphClient client)
    {
        _client = client;
    }

    /// <summary>Calls Microsoft Graph /me with a delegated Authorization Code token.</summary>
    /// <remarks>Sign in through /api/microsoft/login first. The server keeps tokens out of the browser response.</remarks>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<GraphUser>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphUser>> GetMe(CancellationToken cancellationToken) =>
        Ok(await _client.GetMeAsync(User, cancellationToken));

    /// <summary>Calls Microsoft Graph /users with an application Client Credentials token.</summary>
    /// <remarks>
    /// Requires Microsoft Graph application permission User.Read.All with tenant admin consent. This
    /// route represents the workload itself and does not use the signed-in person's identity.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<GraphUser>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GraphUser>>> GetUsers(
        [FromQuery, Range(1, 50)] int top = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await _client.GetUsersAsync(top, cancellationToken));
}
