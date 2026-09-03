namespace Qec.Itmg.Platform.Integrations;

public sealed class IntegrationVendorOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Name or path of the secret-store reference that holds credentials.
    /// Never store an actual API key, token, username, or password here.
    /// </summary>
    public string CredentialReference { get; set; } = string.Empty;

    /// <summary>
    /// Returns true when required non-secret settings are present and non-empty.
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(CredentialReference);
}

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    public IntegrationVendorOptions Veeam { get; set; } = new();

    public IntegrationVendorOptions SonicWallCaptureClient { get; set; } = new();

    public IntegrationVendorOptions Synology { get; set; } = new();
}
