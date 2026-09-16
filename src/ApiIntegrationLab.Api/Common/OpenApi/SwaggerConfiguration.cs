using System.Reflection;
using Microsoft.OpenApi;

namespace ApiIntegrationLab.Api.Common.OpenApi;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddLabSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "API Integration Lab",
                Version = "v1",
                Description = """
                    An executable guide to no-auth REST, outbound Basic Auth, GitHub bearer tokens,
                    Microsoft OAuth 2.0 Authorization Code (delegated identity), Microsoft Client
                    Credentials (workload identity), and inbound HMAC-SHA256 authentication.

                    Demo order: start with /health and /api/public/posts; configure Basic/GitHub
                    secrets; register the documented Microsoft redirect URI and permissions; call
                    /api/microsoft/login before delegated /me or /api/demo; then sign webhook bytes
                    exactly as documented. Provider credentials remain server-side and are never
                    entered into or persisted by Swagger UI.
                    """
            });

            var xmlName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlName));
        });
        return services;
    }

    public static WebApplication UseLabSwagger(this WebApplication app)
    {
        // Keep the learning surface available in every environment used by this local lab. A real
        // internet-facing service should authorize documentation or disable it outside development.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Integration Lab v1");
            options.DocumentTitle = "API Integration Lab";
        });
        return app;
    }
}
