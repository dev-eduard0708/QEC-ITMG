namespace Qec.Itmg.Platform.Integrations;

public sealed class IntegrationVendorOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Name of the secret-store reference that holds credentials.
    /// Never store an actual API key, token, username, or password here.
    /// </summary>
    public string CredentialReference { get; set; } = string.Empty;

    public string ProviderKind { get; set; } = string.Empty;

    /// <summary>Optional shared mailbox / from address for mail integrations.</summary>
    public string? MailboxAddress { get; set; }

    /// <summary>Webhook HMAC secret reference (not the secret itself).</summary>
    public string? WebhookSignatureReference { get; set; }

    /// <summary>Max accepted webhook payload bytes.</summary>
    public int MaxPayloadBytes { get; set; } = 256 * 1024;

    /// <summary>Allowed timestamp skew seconds for webhook freshness.</summary>
    public int TimestampSkewSeconds { get; set; } = 300;

    public bool IsConfigured =>
        Enabled
        && (!RequiresBaseUrl || !string.IsNullOrWhiteSpace(BaseUrl))
        && !string.IsNullOrWhiteSpace(CredentialReference);

    /// <summary>Mail/SIEM/webhook may not require BaseUrl depending on ProviderKind.</summary>
    public bool RequiresBaseUrl { get; set; } = true;

    public bool IsConfiguredRelaxed =>
        Enabled
        && !string.IsNullOrWhiteSpace(CredentialReference)
        && (RequiresBaseUrl ? !string.IsNullOrWhiteSpace(BaseUrl) : true);
}

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    public IntegrationVendorOptions Veeam { get; set; } = new() { RequiresBaseUrl = true };
    public IntegrationVendorOptions SonicWallCaptureClient { get; set; } = new() { RequiresBaseUrl = true };
    public IntegrationVendorOptions Synology { get; set; } = new() { RequiresBaseUrl = true };
    public IntegrationVendorOptions Directory { get; set; } = new() { RequiresBaseUrl = true, ProviderKind = "Graph" };
    public IntegrationVendorOptions Mail { get; set; } = new() { RequiresBaseUrl = true, ProviderKind = "Graph" };
    public IntegrationVendorOptions Virtualization { get; set; } = new() { RequiresBaseUrl = true, ProviderKind = "vCenter" };
    public IntegrationVendorOptions VulnerabilityScanner { get; set; } = new() { RequiresBaseUrl = true };
    public IntegrationVendorOptions Siem { get; set; } = new() { RequiresBaseUrl = true, ProviderKind = "HttpsJson" };
    public IntegrationVendorOptions Webhook { get; set; } = new()
    {
        RequiresBaseUrl = false,
        Enabled = false,
        CredentialReference = "",
    };

    /// <summary>Comma-separated allowlist of inbound webhook provider keys.</summary>
    public string WebhookProviderAllowlist { get; set; } = "veeam,sonicwall,synology,vulnscanner,directory";
}
