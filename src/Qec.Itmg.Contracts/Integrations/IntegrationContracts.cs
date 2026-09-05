namespace Qec.Itmg.Contracts.Integrations;

public enum IntegrationProvider
{
    Veeam = 1,
    SonicWallCaptureClient = 2,
    Synology = 3,
    Directory = 4,
    Mail = 5,
    Virtualization = 6,
    VulnerabilityScanner = 7,
    Siem = 8,
    Webhook = 9,
}

/// <summary>
/// Runtime posture without implying a live vendor probe unless Status is Healthy/Unhealthy.
/// </summary>
public enum IntegrationRuntimeMode
{
    Disabled = 0,
    Stub = 1,
    NotConfigured = 2,
    Configured = 3,
    Healthy = 4,
    Unhealthy = 5,
}

public sealed record IntegrationReadiness(
    IntegrationProvider Provider,
    bool Enabled,
    bool Configured,
    IntegrationRuntimeMode RuntimeMode,
    bool ApprovalRequired,
    string Status = "Disabled",
    DateTimeOffset? LastSuccessfulSyncUtc = null,
    DateTimeOffset? LastFailureUtc = null,
    string? LastErrorSummary = null,
    int? LastProcessedCount = null,
    int? LastUnmatchedCount = null);

public static class IntegrationStatusNames
{
    public const string Disabled = "Disabled";
    public const string NotConfigured = "NotConfigured";
    public const string Configured = "Configured";
    public const string Healthy = "Healthy";
    public const string Unhealthy = "Unhealthy";
}
