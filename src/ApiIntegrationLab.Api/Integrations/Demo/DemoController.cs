using ApiIntegrationLab.Api.Common.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiIntegrationLab.Api.Integrations.Demo;

[ApiController]
[Authorize]
[Route("api/demo")]
public sealed class DemoController : ControllerBase
{
    private readonly IDemoAggregator _aggregator;

    public DemoController(IDemoAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    /// <summary>Calls the public, GitHub, and delegated Microsoft integrations concurrently.</summary>
    /// <remarks>
    /// Sign in through /api/microsoft/login first. Each provider has its own success/failure envelope,
    /// so one upstream outage does not hide healthy results. HTTP 502 means all three providers failed.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<DemoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<DemoResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await _aggregator.GetAsync(User, cancellationToken);
        if (!response.HasAnySuccess)
        {
            throw new IntegrationException(
                "demo",
                IntegrationErrorCategory.Upstream,
                "All demo integrations failed.");
        }

        return Ok(response);
    }
}
