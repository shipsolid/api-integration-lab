using System.Text.Json.Serialization;

namespace ApiIntegrationLab.Api.Integrations.MicrosoftGraph;

internal sealed record GraphUserDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("mail")] string? Mail,
    [property: JsonPropertyName("userPrincipalName")] string? UserPrincipalName);

internal sealed record GraphUsersEnvelope(
    [property: JsonPropertyName("value")] IReadOnlyList<GraphUserDto?>? Value);
