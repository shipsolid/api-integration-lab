using System.Net;

namespace ApiIntegrationLab.IntegrationTests;

public sealed class ProblemDetailsTests
{
    [Theory]
    [InlineData("/api/public/posts?limit=0", HttpStatusCode.BadRequest)]
    [InlineData("/api/basic/profile", HttpStatusCode.ServiceUnavailable)]
    public async Task Errors_use_problem_details_with_a_trace_identifier(
        string path,
        HttpStatusCode expectedStatus)
    {
        using var factory = new ApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("traceId", body, StringComparison.OrdinalIgnoreCase);
    }
}
