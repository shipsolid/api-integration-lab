using System.Net;
using System.Text;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class GitHubPaginationTests
{
    [Fact]
    public async Task Repositories_follow_next_link_until_max_pages()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            RepositoryResponse(
                1,
                "<https://api.github.com/user/repos?per_page=1&page=2>; rel=\"next\", <https://api.github.com/user/repos?per_page=1&page=2>; rel=\"last\""),
            RepositoryResponse(2, link: null)
        ]);
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(responses.Dequeue()));
        var client = CreateClient(handler);

        var result = await client.GetRepositoriesAsync(1, 2, CancellationToken.None);

        Assert.Equal(2, result.Repositories.Count);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Repositories_stop_at_max_pages_even_when_next_link_exists()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(RepositoryResponse(
            1,
            "<https://api.github.com/user/repos?per_page=1&page=2>; rel=\"next\"")));
        var client = CreateClient(handler);

        var result = await client.GetRepositoriesAsync(1, 1, CancellationToken.None);

        Assert.Single(result.Repositories);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Repositories_reject_cross_host_next_link_before_sending_token()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(RepositoryResponse(
            1,
            "<https://attacker.example/repos?page=2>; rel=\"next\"")));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetRepositoriesAsync(1, 2, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(101, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 11)]
    public async Task Repositories_reject_unbounded_query_values(int perPage, int maxPages)
    {
        var client = CreateClient(StubHttpMessageHandler.Unused());

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetRepositoriesAsync(perPage, maxPages, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Validation, error.Category);
    }

    [Fact]
    public async Task Repositories_map_malformed_json_to_safe_upstream_error()
    {
        var client = CreateClient(StubHttpMessageHandler.Json("repository-secret-marker"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetRepositoriesAsync(1, 1, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
        Assert.DoesNotContain("repository-secret-marker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repositories_reject_null_collection_entries()
    {
        var client = CreateClient(StubHttpMessageHandler.Json("[null]"));

        var error = await Assert.ThrowsAsync<IntegrationException>(() =>
            client.GetRepositoriesAsync(1, 1, CancellationToken.None));

        Assert.Equal(IntegrationErrorCategory.Upstream, error.Category);
    }

    private static GitHubClient CreateClient(HttpMessageHandler handler)
    {
        return new GitHubClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            new ApiTelemetry(),
            NullLogger<GitHubClient>.Instance);
    }

    private static HttpResponseMessage RepositoryResponse(int id, string? link)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""
                [{"id":{{id}},"name":"repo-{{id}}","description":"demo","private":false,
                  "html_url":"https://github.com/demo/repo-{{id}}","updated_at":"2026-09-16T00:00:00Z"}]
                """, Encoding.UTF8, "application/json")
        };
        if (link is not null)
            response.Headers.TryAddWithoutValidation("Link", link);
        response.Headers.TryAddWithoutValidation("X-RateLimit-Limit", "5000");
        response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "4999");
        response.Headers.TryAddWithoutValidation("X-RateLimit-Used", "1");
        response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", "1789516800");
        response.Headers.TryAddWithoutValidation("X-RateLimit-Resource", "core");
        return response;
    }
}
