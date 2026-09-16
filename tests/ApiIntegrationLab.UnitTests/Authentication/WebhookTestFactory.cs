using System.Security.Cryptography;
using System.Text;
using ApiIntegrationLab.Api.Authentication.Webhooks;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.UnitTests.Authentication;

internal static class WebhookTestFactory
{
    public static WebhookSignatureVerifier Create(string secret) =>
        new(Options.Create(new WebhookOptions { Secret = secret }));

    public static string Sign(string secret, string timestamp, byte[] body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload, prefix.Length);
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        return $"sha256={Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}
