using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Disabled adapter. Makes no outbound calls. Registered until QEC authorises production integration.
/// </summary>
public sealed class DisabledSynologyMonitor(IOptions<IntegrationOptions> options) : ISynologyMonitor
{
    public IntegrationReadiness GetReadiness() => BuildReadiness(options.Value.Synology);

    public Task<SynologySystemSnapshot?> GetSystemSnapshotAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<SynologySystemSnapshot?>();

    public Task<IReadOnlyList<SynologyVolumeSnapshot>> GetVolumesAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<SynologyVolumeSnapshot>>();

    public Task<IReadOnlyList<SynologyDiskSnapshot>> GetDisksAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<SynologyDiskSnapshot>>();

    public Task<IReadOnlyList<SynologyReplicationSnapshot>> GetReplicationTasksAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<SynologyReplicationSnapshot>>();

    private static IntegrationReadiness BuildReadiness(IntegrationVendorOptions opts) =>
        new(
            IntegrationProvider.Synology,
            Enabled: opts.Enabled,
            Configured: opts.IsConfigured,
            RuntimeMode: IntegrationRuntimeMode.Disabled,
            ApprovalRequired: true);

    private static Task<T> ThrowDisabled<T>() =>
        Task.FromException<T>(
            new InvalidOperationException(
                "Synology integration is disabled. Production connections require QEC authorization."));
}
