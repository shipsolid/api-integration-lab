namespace ApiIntegrationLab.Api.Authentication.Microsoft;

public sealed class GraphOptions
{
    public const string SectionName = "MicrosoftGraph";

    public string BaseUrl { get; init; } = "https://graph.microsoft.com/v1.0/";
    public string[] DelegatedScopes { get; init; } = ["User.Read"];
    public string ApplicationScope { get; init; } = "https://graph.microsoft.com/.default";
}
