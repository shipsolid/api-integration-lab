namespace ApiIntegrationLab.Api.Authentication.Webhooks;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhook";

    public string Secret { get; init; } = string.Empty;
    public TimeSpan ReplayWindow { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxBodyBytes { get; init; } = 64 * 1024;
}
