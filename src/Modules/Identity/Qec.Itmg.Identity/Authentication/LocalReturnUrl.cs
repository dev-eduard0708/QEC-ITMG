namespace Qec.Itmg.Identity.Authentication;

/// <summary>
/// Restricts OIDC login return URLs to local application paths.
/// </summary>
public static class LocalReturnUrl
{
    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        string candidate = returnUrl.Trim();
        if (!IsLocalPath(candidate))
        {
            return "/";
        }

        return candidate;
    }

    public static bool IsLocalPath(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        // Absolute URLs and scheme-relative URLs are rejected.
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
        {
            return false;
        }

        if (returnUrl[0] != '/')
        {
            return false;
        }

        // "/\" and "//..." are not local application paths.
        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
        {
            return false;
        }

        if (returnUrl.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
