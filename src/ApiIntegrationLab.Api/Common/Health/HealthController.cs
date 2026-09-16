using Microsoft.AspNetCore.Mvc;

namespace ApiIntegrationLab.Api.Common.Health;

[ApiController]
public sealed class HealthController : ControllerBase
{
    /// <summary>Reports whether the API process can serve requests.</summary>
    /// <remarks>
    /// External provider credentials are intentionally excluded from liveness so GitHub or Microsoft
    /// configuration cannot make the local API container unhealthy.
    /// </remarks>
    [HttpGet("/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new { status = "Healthy" });
    }
}
