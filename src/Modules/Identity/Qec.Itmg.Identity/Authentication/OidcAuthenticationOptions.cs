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

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            missing.Add(nameof(ClientId));
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
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
                + ". Set Authentication:Oidc values or environment variables Authentication__Oidc__*.");
        }
    }
}
