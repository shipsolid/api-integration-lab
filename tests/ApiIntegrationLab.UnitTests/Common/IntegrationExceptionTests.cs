using ApiIntegrationLab.Api.Common.Errors;
using ApiIntegrationLab.Api.Common.Models;

namespace ApiIntegrationLab.UnitTests.Common;

public sealed class IntegrationExceptionTests
{
    [Theory]
    [InlineData(IntegrationErrorCategory.Authentication, 401)]
    [InlineData(IntegrationErrorCategory.Authorization, 403)]
    [InlineData(IntegrationErrorCategory.Throttled, 429)]
    [InlineData(IntegrationErrorCategory.Timeout, 504)]
    [InlineData(IntegrationErrorCategory.Configuration, 503)]
    [InlineData(IntegrationErrorCategory.Validation, 400)]
    [InlineData(IntegrationErrorCategory.Upstream, 502)]
    public void Error_categories_have_stable_http_mapping(
        IntegrationErrorCategory category,
        int expectedStatus)
    {
        Assert.Equal(expectedStatus, IntegrationException.StatusFor(category));
    }

    [Fact]
    public void Failure_result_contains_only_safe_exception_fields()
    {
        var error = new IntegrationException(
            "github",
            IntegrationErrorCategory.Authentication,
            "GitHub rejected the request.");

        var result = IntegrationResult<string>.Failure(error);

        Assert.Equal("failed", result.Status);
        Assert.Null(result.Data);
        Assert.Equal("github", result.Error!.Provider);
        Assert.Equal("authentication", result.Error.Category);
        Assert.Equal("GitHub rejected the request.", result.Error.Message);
    }
}
