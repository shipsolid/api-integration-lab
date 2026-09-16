using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiIntegrationLab.Api.Common.Configuration;

namespace ApiIntegrationLab.Api.Authentication.Microsoft;

[ApiController]
[Route("api/microsoft")]
public sealed class MicrosoftAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MicrosoftAuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Starts Microsoft OAuth 2.0 Authorization Code sign-in.</summary>
    /// <remarks>
    /// Register http://localhost:8080/auth/microsoft/callback as a Web redirect URI and grant the
    /// delegated User.Read permission. Entra returns a one-time code to middleware, which validates
    /// state/nonce, redeems the code server-side, and creates an encrypted local session cookie.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string returnUrl = "/swagger")
    {
        ValidateMicrosoftConfiguration();
        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/swagger";
        return Challenge(
            new AuthenticationProperties { RedirectUri = safeReturnUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>Clears both the local cookie and Microsoft provider session.</summary>
    /// <remarks>
    /// Call this after delegated Graph demos. The response signs out the encrypted local cookie and
    /// redirects through Entra so the upstream browser session is cleared as well.
    /// </remarks>
    [Authorize]
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/swagger" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private void ValidateMicrosoftConfiguration()
    {
        // Report an actionable 503 instead of asking ASP.NET to challenge an OIDC scheme that is
        // intentionally absent on an unconfigured clone.
        OptionsValidator.Require(
            "microsoft",
            ("AzureAd:TenantId", _configuration["AzureAd:TenantId"]),
            ("AzureAd:ClientId", _configuration["AzureAd:ClientId"]),
            ("AzureAd:ClientSecret", _configuration["AzureAd:ClientSecret"]));
    }
}
