using Qec.Itmg.Contracts.Integrations;

namespace Qec.Itmg.Platform.Integrations;

public static class IntegrationReadinessHelper
{
    public static IntegrationReadiness FromOptions(
        IntegrationProvider provider,
        IntegrationVendorOptions opts,
        bool requireBaseUrl = true,
        DateTimeOffset? lastSuccess = null,
        DateTimeOffset? lastFailure = null,
        string? lastError = null,
        int? processed = null,
        int? unmatched = null)
    {
        if (!opts.Enabled)
        {
            return new(
                provider,
                Enabled: false,
                Configured: false,
                RuntimeMode: IntegrationRuntimeMode.Disabled,
                ApprovalRequired: true,
                Status: IntegrationStatusNames.Disabled,
                LastSuccessfulSyncUtc: lastSuccess,
                LastFailureUtc: lastFailure,
                LastErrorSummary: lastError,
                LastProcessedCount: processed,
                LastUnmatchedCount: unmatched);
        }

        bool configured = requireBaseUrl ? opts.IsConfigured : opts.IsConfiguredRelaxed;
        if (!configured)
        {
            return new(
                provider,
                Enabled: true,
                Configured: false,
                RuntimeMode: IntegrationRuntimeMode.NotConfigured,
                ApprovalRequired: true,
                Status: IntegrationStatusNames.NotConfigured,
                LastSuccessfulSyncUtc: lastSuccess,
                LastFailureUtc: lastFailure,
                LastErrorSummary: lastError,
                LastProcessedCount: processed,
                LastUnmatchedCount: unmatched);
        }

        IntegrationRuntimeMode mode = lastFailure is not null && (lastSuccess is null || lastFailure > lastSuccess)
            ? IntegrationRuntimeMode.Unhealthy
            : lastSuccess is not null
                ? IntegrationRuntimeMode.Healthy
                : IntegrationRuntimeMode.Configured;

        string status = mode switch
        {
            IntegrationRuntimeMode.Healthy => IntegrationStatusNames.Healthy,
            IntegrationRuntimeMode.Unhealthy => IntegrationStatusNames.Unhealthy,
            _ => IntegrationStatusNames.Configured,
        };

        return new(
            provider,
            Enabled: true,
            Configured: true,
            RuntimeMode: mode,
            ApprovalRequired: true,
            Status: status,
            LastSuccessfulSyncUtc: lastSuccess,
            LastFailureUtc: lastFailure,
            LastErrorSummary: lastError,
            LastProcessedCount: processed,
            LastUnmatchedCount: unmatched);
    }

    public static void EnsureCallable(IntegrationVendorOptions opts, string name, bool requireBaseUrl = true)
    {
        if (!opts.Enabled)
            throw new InvalidOperationException($"{name} integration is disabled. Explicit QEC configuration is required.");
        bool configured = requireBaseUrl ? opts.IsConfigured : opts.IsConfiguredRelaxed;
        if (!configured)
            throw new InvalidOperationException($"{name} integration is enabled but not configured (BaseUrl/CredentialReference).");
    }
}
