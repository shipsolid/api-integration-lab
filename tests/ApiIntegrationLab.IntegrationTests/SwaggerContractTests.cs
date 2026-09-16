using System.Net;
using System.Text.Json;

namespace ApiIntegrationLab.IntegrationTests;

public sealed class SwaggerContractTests
{
    [Fact]
    public async Task Swagger_contains_every_demo_route_and_explanation()
    {
        using var factory = new ApplicationFactory();
        using var client = factory.CreateClient();
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        string[] routes =
        [
            "/api/public/posts",
            "/api/basic/profile",
            "/api/github/profile",
            "/api/github/repos",
            "/api/github/rate-limit",
            "/api/microsoft/login",
            "/api/microsoft/me",
            "/api/microsoft/users",
            "/api/microsoft/logout",
            "/api/webhooks/events",
            "/api/demo",
            "/health"
        ];

        foreach (var route in routes)
            Assert.Contains(route, document, StringComparison.Ordinal);

        Assert.Contains("Authorization Code", document, StringComparison.Ordinal);
        Assert.Contains("Client Credentials", document, StringComparison.Ordinal);
        Assert.Contains("HMAC", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_operation_has_inline_summary_and_description()
    {
        using var factory = new ApplicationFactory();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(operation.Value.GetProperty("summary").GetString()),
                    $"{operation.Name.ToUpperInvariant()} {path.Name} is missing a summary.");
                Assert.False(
                    string.IsNullOrWhiteSpace(operation.Value.GetProperty("description").GetString()),
                    $"{operation.Name.ToUpperInvariant()} {path.Name} is missing a description.");
            }
        }
    }

    [Fact]
    public async Task Swagger_ui_is_available_in_the_testing_environment()
    {
        using var factory = new ApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
