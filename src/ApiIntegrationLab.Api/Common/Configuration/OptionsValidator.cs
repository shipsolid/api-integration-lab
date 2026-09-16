using ApiIntegrationLab.Api.Common.Errors;

namespace ApiIntegrationLab.Api.Common.Configuration;

public static class OptionsValidator
{
    public static void Require(
        string provider,
        params (string Key, string? Value)[] values)
    {
        var missingKeys = values
            .Where(value => string.IsNullOrWhiteSpace(value.Value))
            .Select(value => value.Key)
            .ToArray();

        if (missingKeys.Length == 0)
            return;

        // Only key names are safe here. Echoing values would put credentials in errors and logs.
        throw new IntegrationException(
            provider,
            IntegrationErrorCategory.Configuration,
            $"Configure the following settings before using this integration: {string.Join(", ", missingKeys)}.");
    }
}
