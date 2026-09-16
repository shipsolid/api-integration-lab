using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.Api.Authentication.Webhooks;

public sealed class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private const string SignaturePrefix = "sha256=";
    private readonly WebhookOptions _options;
    private readonly ConcurrentDictionary<string, long> _acceptedSignatures = new(StringComparer.Ordinal);

    public WebhookSignatureVerifier(IOptions<WebhookOptions> options)
    {
        _options = options.Value;
    }

    public WebhookVerificationResult Verify(
        byte[] body,
        string? timestamp,
        string? signature,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
            return WebhookVerificationResult.Invalid("webhook_not_configured");
        if (timestamp is null)
            return WebhookVerificationResult.Invalid("missing_timestamp");
        if (signature is null)
            return WebhookVerificationResult.Invalid("missing_signature");
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
            return WebhookVerificationResult.Invalid("invalid_timestamp");

        DateTimeOffset signedAt;
        try
        {
            signedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return WebhookVerificationResult.Invalid("invalid_timestamp");
        }

        // A valid MAC can be replayed verbatim, so accept it only inside a small symmetric clock
        // window. Production senders and receivers therefore need reasonably synchronized clocks.
        if ((signedAt - now).Duration() > _options.ReplayWindow)
            return WebhookVerificationResult.Invalid("timestamp_outside_replay_window");

        var digestText = signature.StartsWith(SignaturePrefix, StringComparison.Ordinal)
            ? signature[SignaturePrefix.Length..]
            : string.Empty;
        if (digestText.Length != 64 || digestText.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            return WebhookVerificationResult.Invalid("invalid_signature");

        var secretBytes = Encoding.UTF8.GetBytes(_options.Secret);
        var timestampBytes = Encoding.UTF8.GetBytes($"{timestamp}.");
        var signedPayload = new byte[timestampBytes.Length + body.Length];
        timestampBytes.CopyTo(signedPayload, 0);
        body.CopyTo(signedPayload, timestampBytes.Length);

        try
        {
            var expected = HMACSHA256.HashData(secretBytes, signedPayload);
            var provided = Convert.FromHexString(digestText);
            try
            {
                // Constant-time comparison prevents a remote sender from learning the expected MAC
                // one byte at a time from response timing differences.
                if (!CryptographicOperations.FixedTimeEquals(expected, provided))
                    return WebhookVerificationResult.Invalid("signature_mismatch");

                var nowSeconds = now.ToUnixTimeSeconds();
                foreach (var accepted in _acceptedSignatures)
                {
                    if (accepted.Value <= nowSeconds)
                        _acceptedSignatures.TryRemove(accepted.Key, out _);
                }

                // Freshness only narrows the replay window. Atomically remembering an accepted MAC
                // rejects an identical delivery racing another request inside that window; the cache
                // stores no body or secret and entries expire when the signed timestamp becomes stale.
                var replayKey = Convert.ToHexString(provided);
                var expiresAt = signedAt.Add(_options.ReplayWindow).ToUnixTimeSeconds();
                return _acceptedSignatures.TryAdd(replayKey, expiresAt)
                    ? WebhookVerificationResult.Valid()
                    : WebhookVerificationResult.Invalid("signature_replayed");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(provided);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(signedPayload);
        }
    }
}
