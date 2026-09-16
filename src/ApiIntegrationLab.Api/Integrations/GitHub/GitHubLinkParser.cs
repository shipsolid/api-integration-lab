using ApiIntegrationLab.Api.Common.Errors;

namespace ApiIntegrationLab.Api.Integrations.GitHub;

internal static class GitHubLinkParser
{
    public static Uri? GetNext(HttpResponseMessage response, Uri allowedBaseAddress)
    {
        if (!response.Headers.TryGetValues("Link", out var linkHeaders))
            return null;

        foreach (var link in linkHeaders.SelectMany(value => value.Split(',')))
        {
            var sections = link.Split(';', StringSplitOptions.TrimEntries);
            if (sections.Length < 2 || !sections.Skip(1).Any(value => value == "rel=\"next\""))
                continue;

            var target = sections[0].Trim();
            if (target.Length < 3 || target[0] != '<' || target[^1] != '>')
                throw InvalidLink();

            if (!Uri.TryCreate(target[1..^1], UriKind.Absolute, out var next))
                throw InvalidLink();

            // A bearer token is added by the shared handler. Following an arbitrary host supplied in
            // Link would leak that token, so pagination is restricted to the configured API origin.
            if (next.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(next.Host, allowedBaseAddress.Host, StringComparison.OrdinalIgnoreCase) ||
                next.Port != allowedBaseAddress.Port)
            {
                throw InvalidLink();
            }

            return next;
        }

        return null;
    }

    private static IntegrationException InvalidLink()
    {
        return new IntegrationException(
            "github",
            IntegrationErrorCategory.Upstream,
            "GitHub returned an invalid pagination link.");
    }
}
