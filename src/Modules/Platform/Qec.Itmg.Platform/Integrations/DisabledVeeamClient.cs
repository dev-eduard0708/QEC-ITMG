using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Disabled adapter. Makes no outbound calls. Registered until QEC authorises production integration.
/// </summary>
public sealed class DisabledVeeamClient(IOptions<IntegrationOptions> options) : IVeeamClient
{
    public IntegrationReadiness GetReadiness() => BuildReadiness(options.Value.Veeam);

    public Task<IReadOnlyList<VeeamJobRunSnapshot>> GetRecentJobRunsAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<VeeamJobRunSnapshot>>();

    public Task<IReadOnlyList<VeeamProtectedWorkload>> GetProtectedWorkloadsAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<VeeamProtectedWorkload>>();

    public Task<IReadOnlyList<VeeamRepositorySnapshot>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<VeeamRepositorySnapshot>>();

    public Task<IReadOnlyList<VeeamRestorePoint>> GetRestorePointsAsync(
        string? objectName = null,
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<VeeamRestorePoint>>();

    private static IntegrationReadiness BuildReadiness(IntegrationVendorOptions opts) =>
        new(
            IntegrationProvider.Veeam,
            Enabled: opts.Enabled,
            Configured: opts.IsConfigured,
            RuntimeMode: IntegrationRuntimeMode.Disabled,
            ApprovalRequired: true);

    private static Task<T> ThrowDisabled<T>() =>
        Task.FromException<T>(
            new InvalidOperationException(
                "Veeam integration is disabled. Production connections require QEC authorization."));
}
