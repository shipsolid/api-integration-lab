using ApiIntegrationLab.Api.Common.Models;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.Api.Integrations.PublicApi;

namespace ApiIntegrationLab.Api.Integrations.Demo;

public sealed record DemoResponse(
    IntegrationResult<IReadOnlyList<PublicPost>> PublicApi,
    IntegrationResult<GitHubRepositoryPage> GitHub,
    IntegrationResult<GraphUser> Microsoft)
{
    public bool HasAnySuccess =>
        PublicApi.Status == "success" ||
        GitHub.Status == "success" ||
        Microsoft.Status == "success";
}
