using System.Diagnostics;
using System.Security.Claims;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Models;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.Api.Integrations.PublicApi;

namespace ApiIntegrationLab.Api.Integrations.Demo;

public sealed class DemoAggregator : IDemoAggregator
{
    private readonly IPublicApiClient _publicApi;
    private readonly IGitHubClient _gitHub;
    private readonly IMicrosoftGraphClient _microsoft;

    public DemoAggregator(
        IPublicApiClient publicApi,
        IGitHubClient gitHub,
        IMicrosoftGraphClient microsoft)
    {
        _publicApi = publicApi;
        _gitHub = gitHub;
        _microsoft = microsoft;
    }

    public async Task<DemoResponse> GetAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        using var activity = ApiTelemetry.Activities.StartActivity("demo.aggregate");

        // Start every independent provider before awaiting any one of them. Slow providers overlap,
        // and a known integration failure becomes result data instead of discarding other successes.
        var publicTask = CaptureAsync(
            () => _publicApi.GetPostsAsync(5, cancellationToken));
        var gitHubTask = CaptureAsync(
            () => _gitHub.GetRepositoriesAsync(perPage: 5, maxPages: 1, cancellationToken));
        var microsoftTask = CaptureAsync(
            () => _microsoft.GetMeAsync(user, cancellationToken));

        await Task.WhenAll(publicTask, gitHubTask, microsoftTask);

        var response = new DemoResponse(
            await publicTask,
            await gitHubTask,
            await microsoftTask);
        AddProviderEvent(activity, "public", response.PublicApi.Status);
        AddProviderEvent(activity, "github", response.GitHub.Status);
        AddProviderEvent(activity, "microsoft", response.Microsoft.Status);
        return response;
    }

    private static async Task<IntegrationResult<T>> CaptureAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return IntegrationResult<T>.Success(await operation());
        }
        catch (IntegrationException exception)
        {
            return IntegrationResult<T>.Failure(exception);
        }
    }

    private static void AddProviderEvent(Activity? activity, string provider, string status)
    {
        activity?.AddEvent(new ActivityEvent(
            "provider.completed",
            tags: new ActivityTagsCollection
            {
                { "provider", provider },
                { "status", status }
            }));
    }
}
