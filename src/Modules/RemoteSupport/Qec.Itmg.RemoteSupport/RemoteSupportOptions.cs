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

    public bool IsConfigured =>
        Enabled
        && !string.Equals(ProviderKind, "Disabled", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(CredentialReference);
}
