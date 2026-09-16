using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiIntegrationLab.IntegrationTests;

public sealed class ApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Tests use disposable cookie keys so running the suite never writes developer-home
            // state or leaves an unencrypted local key behind.
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }
}
