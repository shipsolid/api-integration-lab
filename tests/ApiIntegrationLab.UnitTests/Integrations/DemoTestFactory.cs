using System.Security.Claims;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Integrations.Demo;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.Api.Integrations.PublicApi;
using ApiIntegrationLab.UnitTests.TestDoubles;

namespace ApiIntegrationLab.UnitTests.Integrations;

internal static class DemoTestFactory
{
    public static DemoAggregator Create(
        IReadOnlyList<PublicPost>? publicResult = null,
        IntegrationException? publicError = null,
        GitHubRepositoryPage? githubResult = null,
        IntegrationException? githubError = null,
        GraphUser? graphResult = null,
        IntegrationException? graphError = null) =>
        new(
            new PublicClient(publicResult ?? [], publicError),
            new GitHubClient(githubResult ?? EmptyGitHubPage(), githubError),
            new GraphClient(graphResult ?? new GraphUser("1", "Demo User", null, null), graphError));

    public static DemoAggregator CreateWithProbe(ConcurrentStartProbe probe) =>
        new(
            new PublicClient([], probe),
            new GitHubClient(EmptyGitHubPage(), probe),
            new GraphClient(new GraphUser("1", "Demo User", null, null), probe));

    private static GitHubRepositoryPage EmptyGitHubPage() =>
        new([], 0, new GitHubRateLimit(60, 60, 0, DateTimeOffset.UnixEpoch, "core"));

    private sealed class PublicClient : IPublicApiClient
    {
        private readonly IReadOnlyList<PublicPost> _result;
        private readonly IntegrationException? _error;
        private readonly ConcurrentStartProbe? _probe;

        public PublicClient(IReadOnlyList<PublicPost> result, IntegrationException? error)
        {
            _result = result;
            _error = error;
        }

        public PublicClient(IReadOnlyList<PublicPost> result, ConcurrentStartProbe probe)
        {
            _result = result;
            _probe = probe;
        }

        public Task<IReadOnlyList<PublicPost>> GetPostsAsync(int limit, CancellationToken cancellationToken) =>
            _error is not null
                ? Task.FromException<IReadOnlyList<PublicPost>>(_error)
                : _probe?.StartAsync(_result, cancellationToken) ?? Task.FromResult(_result);
    }

    private sealed class GitHubClient : IGitHubClient
    {
        private readonly GitHubRepositoryPage _result;
        private readonly IntegrationException? _error;
        private readonly ConcurrentStartProbe? _probe;

        public GitHubClient(GitHubRepositoryPage result, IntegrationException? error)
        {
            _result = result;
            _error = error;
        }

        public GitHubClient(GitHubRepositoryPage result, ConcurrentStartProbe probe)
        {
            _result = result;
            _probe = probe;
        }

        public Task<GitHubRepositoryPage> GetRepositoriesAsync(int perPage, int maxPages, CancellationToken cancellationToken) =>
            _error is not null
                ? Task.FromException<GitHubRepositoryPage>(_error)
                : _probe?.StartAsync(_result, cancellationToken) ?? Task.FromResult(_result);

        public Task<GitHubProfile> GetProfileAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GitHubRateLimit> GetRateLimitAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class GraphClient : IMicrosoftGraphClient
    {
        private readonly GraphUser _result;
        private readonly IntegrationException? _error;
        private readonly ConcurrentStartProbe? _probe;

        public GraphClient(GraphUser result, IntegrationException? error)
        {
            _result = result;
            _error = error;
        }

        public GraphClient(GraphUser result, ConcurrentStartProbe probe)
        {
            _result = result;
            _probe = probe;
        }

        public Task<GraphUser> GetMeAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
            _error is not null
                ? Task.FromException<GraphUser>(_error)
                : _probe?.StartAsync(_result, cancellationToken) ?? Task.FromResult(_result);

        public Task<IReadOnlyList<GraphUser>> GetUsersAsync(int top, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
