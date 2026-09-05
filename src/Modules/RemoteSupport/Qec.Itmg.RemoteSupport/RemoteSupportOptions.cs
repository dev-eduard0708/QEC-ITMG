namespace Qec.Itmg.RemoteSupport;

public sealed class RemoteSupportOptions
{
    public const string SectionName = "RemoteSupport";

    public bool Enabled { get; set; }

    /// <summary>Disabled | MeshCentral</summary>
    public string ProviderKind { get; set; } = "MeshCentral";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Secret-store reference only — never a password/token value.</summary>
    public string CredentialReference { get; set; } = string.Empty;

    /// <summary>HMAC secret reference for engine webhooks.</summary>
    public string WebhookSignatureReference { get; set; } = string.Empty;

    /// <summary>UNATTENDED MUST remain false in production by default.</summary>
    public bool UnattendedEnabled { get; set; }

    /// <summary>When true, unattended start requires MFA/step-up claims when present.</summary>
    public bool RequireMfaForUnattended { get; set; } = true;

    /// <summary>Comma-separated CI type keys allowed for unattended (e.g. server,kiosk).</summary>
    public string UnattendedAllowedCiTypeKeys { get; set; } = "server,kiosk";

    /// <summary>Critical CIs require a linked Change for unattended.</summary>
    public bool RequireChangeForCriticalUnattended { get; set; } = true;

    public int DefaultConsentExpiryMinutes { get; set; } = 60;

    public int WebhookTimestampSkewSeconds { get; set; } = 300;

    public int MaxWebhookPayloadBytes { get; set; } = 262144;

    /// <summary>
    /// Optional HTTPS URL for the endpoint agent installer (admin-configured).
    /// Never put secrets here — public download link only.
    /// </summary>
    public string AgentDownloadUrl { get; set; } = string.Empty;

    /// <summary>Optional employee-facing install instructions (plain text / markdown-lite).</summary>
    public string AgentInstallInstructions { get; set; } = string.Empty;

    /// <summary>Optional HTTPS URL for the QEC Support Helper bootstrap package (built/signed outside git).</summary>
    public string HelperDownloadUrl { get; set; } = string.Empty;

    /// <summary>Optional helper install instructions for employees.</summary>
    public string HelperInstallInstructions { get; set; } = string.Empty;

    /// <summary>One-time enrollment token lifetime (minutes). Default 10.</summary>
    public int EnrollmentTokenLifetimeMinutes { get; set; } = 10;

    /// <summary>How long temporary endpoints remain associated after last activity (hours).</summary>
    public int TemporaryEndpointRetentionHours { get; set; } = 72;

    /// <summary>
    /// Development only: when helper binary is unavailable, allow mock endpoint registration
    /// labelled as development. Must stay false in production.
    /// </summary>
    public bool AllowDevelopmentMockEnrollment { get; set; }

    public bool HasAgentDownload =>
        !string.IsNullOrWhiteSpace(AgentDownloadUrl)
        && Uri.TryCreate(AgentDownloadUrl.Trim(), UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public bool HasHelperDownload =>
        !string.IsNullOrWhiteSpace(HelperDownloadUrl)
        && Uri.TryCreate(HelperDownloadUrl.Trim(), UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public bool IsConfigured =>
        Enabled
        && !string.Equals(ProviderKind, "Disabled", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(CredentialReference);
}
