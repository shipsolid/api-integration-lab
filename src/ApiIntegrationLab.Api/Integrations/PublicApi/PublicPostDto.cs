using System.Text.Json.Serialization;

namespace ApiIntegrationLab.Api.Integrations.PublicApi;

internal sealed record PublicPostDto(
    [property: JsonPropertyName("userId")] int UserId,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);
