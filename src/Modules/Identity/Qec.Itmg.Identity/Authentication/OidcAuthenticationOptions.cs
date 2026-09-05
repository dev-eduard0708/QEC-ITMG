namespace Qec.Itmg.Identity.Authentication;

public sealed class OidcAuthenticationOptions
{
    public const string SectionName = "Authentication:Oidc";

    public bool Enabled { get; set; }

    /// <summary>
    /// OIDC authority. Default Google accounts endpoint for the current primary provider.
    /// </summary>
    public string Authority { get; set; } = "https://accounts.google.com";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// When empty, any verified Google account is accepted (typical for Development).
    /// Production should list allowed Google Workspace domains (e.g. qehc.edu.sa).
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// Development-only: when true (default), first verified Google sign-in JIT-provisions an Active Employee.
    /// Ignored outside Development — production JIT remains governed by AllowedDomains + ops process.
    /// Never grants Admin/IT permissions.
    /// </summary>
    public bool DevelopmentAutoProvisionEmployee { get; set; } = true;

    public void EnsureValidWhenEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(Authority))
        {
            missing.Add(nameof(Authority));
        }

        if (string.IsNullOrWhiteSpace(ClientId) || IsPlaceholderSecret(ClientId))
        {
            missing.Add(nameof(ClientId));
        }

        if (string.IsNullOrWhiteSpace(ClientSecret) || IsPlaceholderSecret(ClientSecret))
        {
            missing.Add(nameof(ClientSecret));
        }

        if (string.IsNullOrWhiteSpace(CallbackPath))
        {
            missing.Add(nameof(CallbackPath));
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "OIDC authentication is enabled but required configuration is missing: "
                + string.Join(", ", missing)
                + ". For local Development, copy src/Qec.Itmg.Host/appsettings.Development.local.example.json "
                + "to appsettings.Development.local.json (gitignored) and paste your Google OAuth Client ID/Secret. "
                + "Authorized redirect URI must be exactly http://localhost:5173/signin-oidc. "
                + "See docs/01-foundation/GOOGLE-OAUTH-LOCAL-DEVELOPMENT.md. "
                + "Do not log or commit ClientSecret. "
                + "Production uses environment/secret store Authentication__Oidc__* values.");
        }
    }

    private static bool IsPlaceholderSecret(string value) =>
        value.Contains("PASTE_", StringComparison.OrdinalIgnoreCase)
        || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "changeme", StringComparison.OrdinalIgnoreCase);
}
