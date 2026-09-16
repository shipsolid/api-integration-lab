using ApiIntegrationLab.Api.Common.Configuration;
using ApiIntegrationLab.Api.Common.Errors;

namespace ApiIntegrationLab.UnitTests.Common;

public sealed class OptionsValidatorTests
{
    [Fact]
    public void Require_lists_missing_keys_without_exposing_configured_values()
    {
        var error = Assert.Throws<IntegrationException>(() =>
            OptionsValidator.Require(
                "github",
                ("GitHub:Token", null),
                ("GitHub:UserAgent", "secret-marker-value")));

        Assert.Equal(IntegrationErrorCategory.Configuration, error.Category);
        Assert.Contains("GitHub:Token", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-marker-value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Require_accepts_nonempty_values()
    {
        var error = Record.Exception(() =>
            OptionsValidator.Require("github", ("GitHub:Token", "configured")));

        Assert.Null(error);
    }
}
