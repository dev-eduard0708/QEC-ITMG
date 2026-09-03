using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Disabled adapter. Makes no outbound calls. Registered until QEC authorises production integration.
/// NOTE: This is not the P2-03 attachment malware scanner (IMalwareScanner keeps a separate abstraction).
/// </summary>
public sealed class DisabledSonicWallClient(IOptions<IntegrationOptions> options) : ISonicWallCaptureClient
{
    public IntegrationReadiness GetReadiness() => BuildReadiness(options.Value.SonicWallCaptureClient);

    public Task<IReadOnlyList<SonicWallEndpointSnapshot>> GetEndpointsAsync(
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<SonicWallEndpointSnapshot>>();

    public Task<IReadOnlyList<SonicWallDetectionSnapshot>> GetRecentDetectionsAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default)
        => ThrowDisabled<IReadOnlyList<SonicWallDetectionSnapshot>>();

    private static IntegrationReadiness BuildReadiness(IntegrationVendorOptions opts) =>
        new(
            IntegrationProvider.SonicWallCaptureClient,
            Enabled: opts.Enabled,
            Configured: opts.IsConfigured,
            RuntimeMode: IntegrationRuntimeMode.Disabled,
            ApprovalRequired: true);

    private static Task<T> ThrowDisabled<T>() =>
        Task.FromException<T>(
            new InvalidOperationException(
                "SonicWall Capture Client integration is disabled. Production connections require QEC authorization."));
}
