using System.Security.Claims;
using ApiIntegrationLab.Api.Authentication.Microsoft;

namespace ApiIntegrationLab.UnitTests.TestDoubles;

internal sealed class RecordingGraphTokenProvider : IGraphTokenProvider
{
    private readonly string _delegatedToken;
    private readonly string _applicationToken;

    public RecordingGraphTokenProvider(string delegatedToken, string applicationToken)
    {
        _delegatedToken = delegatedToken;
        _applicationToken = applicationToken;
    }

    public int DelegatedCalls { get; private set; }
    public int ApplicationCalls { get; private set; }

    public Task<string> GetDelegatedTokenAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        DelegatedCalls++;
        return Task.FromResult(_delegatedToken);
    }

    public Task<string> GetApplicationTokenAsync(CancellationToken cancellationToken)
    {
        ApplicationCalls++;
        return Task.FromResult(_applicationToken);
    }
}
