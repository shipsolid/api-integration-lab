namespace ApiIntegrationLab.Api.Common.Errors;

public enum IntegrationErrorCategory
{
    Authentication,
    Authorization,
    Configuration,
    Throttled,
    Timeout,
    Upstream,
    Validation
}
