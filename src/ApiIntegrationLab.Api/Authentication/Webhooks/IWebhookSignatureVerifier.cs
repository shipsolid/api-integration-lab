namespace ApiIntegrationLab.Api.Authentication.Webhooks;

public interface IWebhookSignatureVerifier
{
    WebhookVerificationResult Verify(
        byte[] body,
        string? timestamp,
        string? signature,
        DateTimeOffset now);
}
