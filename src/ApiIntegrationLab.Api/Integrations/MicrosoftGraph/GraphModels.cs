namespace ApiIntegrationLab.Api.Integrations.MicrosoftGraph;

public sealed record GraphUser(
    string Id,
    string DisplayName,
    string? Mail,
    string? UserPrincipalName);
