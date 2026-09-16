using System.Security.Claims;

namespace ApiIntegrationLab.UnitTests.TestDoubles;

internal static class TestUsers
{
    public static ClaimsPrincipal Authenticated { get; } = new(
        new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            authenticationType: "test"));
}
