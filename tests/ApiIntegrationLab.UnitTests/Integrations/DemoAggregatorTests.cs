using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.Api.Integrations.PublicApi;
using ApiIntegrationLab.UnitTests.TestDoubles;

namespace ApiIntegrationLab.UnitTests.Integrations;

public sealed class DemoAggregatorTests
{
    [Fact]
    public async Task GetAsync_preserves_success_when_another_provider_fails()
    {
        var aggregator = DemoTestFactory.Create(
            publicResult: [new PublicPost(1, "Public", "Body")],
            githubError: new IntegrationException(
                "github", IntegrationErrorCategory.Authentication, "GitHub rejected the token."),
            graphResult: new GraphUser("1", "Demo User", null, "demo@example.com"));

        var result = await aggregator.GetAsync(TestUsers.Authenticated, CancellationToken.None);

        Assert.Equal("success", result.PublicApi.Status);
        Assert.Equal("failed", result.GitHub.Status);
        Assert.Equal("GitHub rejected the token.", result.GitHub.Error?.Message);
        Assert.Equal("success", result.Microsoft.Status);
        Assert.True(result.HasAnySuccess);
    }

    [Fact]
    public async Task GetAsync_starts_all_provider_calls_before_awaiting_completion()
    {
        var probe = new ConcurrentStartProbe(expectedStarts: 3);
        var aggregator = DemoTestFactory.CreateWithProbe(probe);

        await aggregator.GetAsync(TestUsers.Authenticated, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(probe.AllStartedBeforeRelease);
    }

    [Fact]
    public async Task GetAsync_returns_success_envelopes_when_all_providers_succeed()
    {
        var result = await DemoTestFactory.Create().GetAsync(
            TestUsers.Authenticated,
            CancellationToken.None);

        Assert.All(
            [result.PublicApi.Status, result.GitHub.Status, result.Microsoft.Status],
            status => Assert.Equal("success", status));
        Assert.True(result.HasAnySuccess);
    }

    [Fact]
    public async Task GetAsync_marks_response_unsuccessful_when_all_providers_fail()
    {
        var error = new IntegrationException("provider", IntegrationErrorCategory.Upstream, "Safe failure.");
        var result = await DemoTestFactory.Create(
            publicError: error,
            githubError: error,
            graphError: error).GetAsync(TestUsers.Authenticated, CancellationToken.None);

        Assert.All(
            [result.PublicApi.Status, result.GitHub.Status, result.Microsoft.Status],
            status => Assert.Equal("failed", status));
        Assert.False(result.HasAnySuccess);
    }

    [Fact]
    public async Task GetAsync_propagates_caller_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DemoTestFactory.CreateWithProbe(new ConcurrentStartProbe(3)).GetAsync(
                TestUsers.Authenticated,
                cancellation.Token));
    }
}
