using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Http;
using ApiIntegrationLab.Api.Common.OpenApi;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.Api.Common.Time;
using ApiIntegrationLab.Api.Integrations.BasicAuth;
using ApiIntegrationLab.Api.Integrations.Demo;
using ApiIntegrationLab.Api.Integrations.GitHub;
using ApiIntegrationLab.Api.Authentication.Microsoft;
using ApiIntegrationLab.Api.Authentication.Webhooks;
using ApiIntegrationLab.Api.Integrations.MicrosoftGraph;
using ApiIntegrationLab.Api.Integrations.PublicApi;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddLabSwagger();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<IntegrationExceptionHandler>();
builder.Services.AddSingleton<ApiTelemetry>();
builder.Services.AddLabOpenTelemetry(builder.Configuration);
builder.Logging.AddLabOpenTelemetryLogging();
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddScoped<IDemoAggregator, DemoAggregator>();
builder.Services.Configure<WebhookOptions>(
    builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.AddSingleton<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
builder.Services.Configure<PublicApiOptions>(
    builder.Configuration.GetSection(PublicApiOptions.SectionName));
builder.Services
    .AddHttpClient<IPublicApiClient, PublicApiClient>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<PublicApiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddIntegrationResilience();
builder.Services.Configure<BasicAuthOptions>(
    builder.Configuration.GetSection(BasicAuthOptions.SectionName));
builder.Services.AddTransient<BasicAuthenticationHandler>();
builder.Services
    .AddHttpClient<IBasicAuthClient, BasicAuthClient>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<BasicAuthOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddHttpMessageHandler<BasicAuthenticationHandler>()
    .AddIntegrationResilience();
builder.Services.Configure<GitHubOptions>(
    builder.Configuration.GetSection(GitHubOptions.SectionName));
builder.Services.AddTransient<GitHubAuthenticationHandler>();
builder.Services
    .AddHttpClient<IGitHubClient, GitHubClient>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<GitHubOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddHttpMessageHandler<GitHubAuthenticationHandler>()
    .AddIntegrationResilience();
var azureAd = builder.Configuration.GetSection("AzureAd");
var microsoftAuthenticationConfigured =
    !string.IsNullOrWhiteSpace(azureAd["TenantId"]) &&
    !string.IsNullOrWhiteSpace(azureAd["ClientId"]) &&
    !string.IsNullOrWhiteSpace(azureAd["ClientSecret"]);
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});
if (microsoftAuthenticationConfigured)
{
    // OIDC is a request-handler scheme and ASP.NET initializes it on every request. Registering it
    // only when complete settings exist keeps health, Swagger, and non-Microsoft demos available
    // on a fresh clone while preserving the real Authorization Code flow once secrets are supplied.
    authentication
        .AddMicrosoftIdentityWebApp(azureAd)
        .EnableTokenAcquisitionToCallDownstreamApi(["User.Read"])
        .AddInMemoryTokenCaches();
    builder.Services.AddScoped<IGraphTokenProvider, MicrosoftGraphTokenProvider>();
}
else
{
    authentication.AddCookie();
    builder.Services.AddScoped<IGraphTokenProvider, UnconfiguredGraphTokenProvider>();
}
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
{
    // Microsoft.Identity.Web registers OIDC as its default. Restore the split explicitly after its
    // registration so anonymous requests authenticate only against the local cookie handler.
    options.DefaultScheme = null;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // Protected API routes challenge the cookie scheme and keep their 401 contract. Only the
    // explicit login action names OIDC, so enabling Microsoft cannot turn API calls into redirects.
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});
builder.Services.PostConfigure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options =>
    {
        // Browser redirects turn an API's 401/403 contract into a misleading 302. OAuth starts only
        // through the explicit /api/microsoft/login route, so protected API routes return status codes.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.Configure<GraphOptions>(
    builder.Configuration.GetSection(GraphOptions.SectionName));
builder.Services
    .AddHttpClient<IMicrosoftGraphClient, MicrosoftGraphClient>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<GraphOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddIntegrationResilience();

var app = builder.Build();

app.UseExceptionHandler();
app.UseLabSwagger();
app.UseMiddleware<TraceContextMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
