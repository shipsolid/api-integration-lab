namespace ApiIntegrationLab.Api.Integrations.BasicAuth;

public sealed class BasicAuthOptions
{
    public const string SectionName = "BasicAuth";

    public string BaseUrl { get; init; } = "https://postman-echo.com/";
    public string? Username { get; init; }
    public string? Password { get; init; }
}
