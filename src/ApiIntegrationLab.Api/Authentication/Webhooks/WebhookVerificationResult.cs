namespace ApiIntegrationLab.Api.Authentication.Webhooks;

public sealed record WebhookVerificationResult(bool IsValid, string? Failure)
{
    public static WebhookVerificationResult Valid() => new(true, null);
    public static WebhookVerificationResult Invalid(string failure) => new(false, failure);
}
