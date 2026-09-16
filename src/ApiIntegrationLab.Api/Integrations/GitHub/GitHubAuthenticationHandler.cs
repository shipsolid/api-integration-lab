using System.Net.Http.Headers;
using ApiIntegrationLab.Api.Common.Errors;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.Api.Integrations.GitHub;

public sealed class GitHubAuthenticationHandler : DelegatingHandler
{
    private readonly IOptions<GitHubOptions> _options;

    public GitHubAuthenticationHandler(IOptions<GitHubOptions> options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (request.RequestUri?.Scheme != Uri.UriSchemeHttps)
        {
            throw new IntegrationException(
                "github",
                IntegrationErrorCategory.Configuration,
                "The GitHub API URL must use HTTPS.");
        }
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd(options.UserAgent);
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", options.ApiVersion);

        // GitHub requires an identifiable User-Agent and versioned media headers. Authorization is
        // intentionally omitted when no token exists so the lab can demonstrate the real 401 first.
        if (!string.IsNullOrWhiteSpace(options.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);

        return base.SendAsync(request, cancellationToken);
    }
}
