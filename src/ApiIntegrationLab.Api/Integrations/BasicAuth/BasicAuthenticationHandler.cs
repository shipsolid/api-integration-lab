using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using ApiIntegrationLab.Api.Common.Configuration;
using ApiIntegrationLab.Api.Common.Errors;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.Api.Integrations.BasicAuth;

public sealed class BasicAuthenticationHandler : DelegatingHandler
{
    private readonly IOptions<BasicAuthOptions> _options;

    public BasicAuthenticationHandler(IOptions<BasicAuthOptions> options)
    {
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        OptionsValidator.Require(
            "basic",
            ("BasicAuth:Username", options.Username),
            ("BasicAuth:Password", options.Password));
        if (request.RequestUri?.Scheme != Uri.UriSchemeHttps)
        {
            throw new IntegrationException(
                "basic",
                IntegrationErrorCategory.Configuration,
                "The Basic Auth provider URL must use HTTPS.");
        }

        var credentialBytes = Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}");
        try
        {
            // Base64 is reversible encoding, not encryption. HTTPS is what protects these credentials
            // in transit, so the configured provider URL must never use plaintext HTTP.
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(credentialBytes));
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialBytes);
        }
    }
}
