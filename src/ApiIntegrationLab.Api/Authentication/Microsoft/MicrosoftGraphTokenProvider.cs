using System.Security.Claims;
using ApiIntegrationLab.Api.Common.Configuration;
using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Telemetry;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace ApiIntegrationLab.Api.Authentication.Microsoft;

public sealed class MicrosoftGraphTokenProvider : IGraphTokenProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IOptions<GraphOptions> _graphOptions;
    private readonly IConfiguration _configuration;
    private readonly ApiTelemetry _telemetry;

    public MicrosoftGraphTokenProvider(
        ITokenAcquisition tokenAcquisition,
        IOptions<GraphOptions> graphOptions,
        IConfiguration configuration,
        ApiTelemetry telemetry)
    {
        _tokenAcquisition = tokenAcquisition;
        _graphOptions = graphOptions;
        _configuration = configuration;
        _telemetry = telemetry;
    }

    public async Task<string> GetDelegatedTokenAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMicrosoftConfiguration();

        try
        {
            // Authorization Code represents the signed-in user. The library redeems/caches tokens;
            // application code receives only the access token needed for this Graph request.
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync(
                _graphOptions.Value.DelegatedScopes,
                user: user,
                tokenAcquisitionOptions: new TokenAcquisitionOptions
                {
                    CancellationToken = cancellationToken
                });
            _telemetry.RecordTokenRequest("delegated", "success");
            return token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _telemetry.RecordTokenRequest("delegated", "cancelled");
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _telemetry.RecordTokenRequest("delegated", "error");
            throw new IntegrationException(
                "microsoft",
                IntegrationErrorCategory.Authentication,
                "Microsoft delegated token acquisition failed. Sign in again and verify User.Read consent.",
                innerException: exception);
        }
    }

    public async Task<string> GetApplicationTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMicrosoftConfiguration();

        try
        {
            // Client Credentials represents the workload, not a person. The .default scope uses the
            // application permissions granted by an Entra administrator, including User.Read.All.
            var token = await _tokenAcquisition.GetAccessTokenForAppAsync(
                _graphOptions.Value.ApplicationScope,
                tokenAcquisitionOptions: new TokenAcquisitionOptions
                {
                    CancellationToken = cancellationToken
                });
            _telemetry.RecordTokenRequest("application", "success");
            return token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _telemetry.RecordTokenRequest("application", "cancelled");
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _telemetry.RecordTokenRequest("application", "error");
            throw new IntegrationException(
                "microsoft",
                IntegrationErrorCategory.Authentication,
                "Microsoft application token acquisition failed. Verify credentials and admin consent.",
                innerException: exception);
        }
    }

    private void ValidateMicrosoftConfiguration()
    {
        // Validation happens at endpoint use so public, health, and Swagger routes remain available
        // before local secrets are configured.
        OptionsValidator.Require(
            "microsoft",
            ("AzureAd:TenantId", _configuration["AzureAd:TenantId"]),
            ("AzureAd:ClientId", _configuration["AzureAd:ClientId"]),
            ("AzureAd:ClientSecret", _configuration["AzureAd:ClientSecret"]));
    }
}
