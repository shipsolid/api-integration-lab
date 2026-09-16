namespace ApiIntegrationLab.Api.Integrations.PublicApi;

public sealed class PublicApiOptions
{
    public const string SectionName = "PublicApi";

    public string BaseUrl { get; init; } = "https://jsonplaceholder.typicode.com/";
}
