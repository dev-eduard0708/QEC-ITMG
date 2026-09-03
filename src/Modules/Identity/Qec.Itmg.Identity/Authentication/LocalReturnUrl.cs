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
        return IsLocalPath(candidate) ? candidate : "/";
    }

    public static bool IsLocalPath(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        // Match ASP.NET Core local-URL rules: allow "/..." but not "//..." or "/\...".
        // Do not use UriKind.Absolute — on Linux "/employee" is treated as an absolute file URI.
        if (returnUrl[0] != '/')
        {
            return false;
        }

        if (returnUrl.Length == 1)
        {
            return true;
        }

        if (returnUrl[1] is '/' or '\\')
        {
            return false;
        }

        if (returnUrl.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        if (returnUrl.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
