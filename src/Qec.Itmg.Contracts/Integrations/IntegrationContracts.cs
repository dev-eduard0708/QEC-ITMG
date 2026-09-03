namespace Qec.Itmg.Contracts.Integrations;

public enum IntegrationProvider
{
    Veeam = 1,
    SonicWallCaptureClient = 2,
    Synology = 3,
}

public enum IntegrationRuntimeMode
{
    Disabled = 0,
    Stub = 1,
}

public sealed record IntegrationReadiness(
    IntegrationProvider Provider,
    bool Enabled,
    bool Configured,
    IntegrationRuntimeMode RuntimeMode,
    bool ApprovalRequired);
