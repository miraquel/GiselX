namespace GiselX.Service;

public static class PowerBiPublishUrlValidator
{
    public static bool TryNormalize(string? value, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate) || candidate.Any(char.IsControl))
            return Fail("A Power BI Publish-to-web URL is required.", out error);
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return Fail("The report URL must be an absolute HTTPS URL.", out error);
        if (!string.Equals(uri.Host, "app.powerbi.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            return Fail("The report URL must use app.powerbi.com.", out error);
        if (!string.Equals(uri.AbsolutePath.TrimEnd('/'), "/view", StringComparison.OrdinalIgnoreCase))
            return Fail("Use a Power BI Publish-to-web viewer URL.", out error);
        if (!string.IsNullOrEmpty(uri.Fragment) || !HasReportToken(uri.Query))
            return Fail("The Publish-to-web URL must contain a non-empty r parameter and no fragment.", out error);

        normalizedUrl = uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.PathAndQuery,
            UriFormat.UriEscaped);
        return true;
    }

    private static bool HasReportToken(string query) => query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Any(pair => pair.Length == 2 &&
            string.Equals(Uri.UnescapeDataString(pair[0]), "r", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(Uri.UnescapeDataString(pair[1])));

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
