using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>Read-only Synology DSM monitor adapter. Disabled by default.</summary>
public sealed class SynologyHttpMonitor(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    ILogger<SynologyHttpMonitor> logger,
    IntegrationHealthState health) : ISynologyMonitor
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.Synology,
            options.Value.Synology,
            lastSuccess: health.Get(IntegrationProvider.Synology)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.Synology)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.Synology)?.LastError,
            processed: health.Get(IntegrationProvider.Synology)?.LastProcessed,
            unmatched: health.Get(IntegrationProvider.Synology)?.LastUnmatched);

    public async Task<SynologySystemSnapshot?> GetSystemSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync(
            "/webapi/entry.cgi?api=SYNO.Core.System&method=info&version=1", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Synology system info returned {Status}", (int)response.StatusCode);
            return null;
        }
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement data = doc.RootElement.TryGetProperty("data", out JsonElement d) ? d : doc.RootElement;
        return new SynologySystemSnapshot(
            DeviceId: Read(data, "serial", "device_id") ?? "synology",
            Hostname: Read(data, "hostname", "model") ?? "synology",
            DsmVersion: Read(data, "version_string", "firmware_ver") ?? "unknown",
            SystemStatus: Read(data, "status", "system_status") ?? "Unknown",
            TotalCapacityBytes: 0,
            UsedCapacityBytes: 0,
            FreeCapacityBytes: 0);
    }

    public async Task<IReadOnlyList<SynologyVolumeSnapshot>> GetVolumesAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        _ = await client.GetAsync("/webapi/entry.cgi?api=SYNO.Storage.CGI.Storage&method=load_info&version=1", cancellationToken);
        return [];
    }

    public async Task<IReadOnlyList<SynologyDiskSnapshot>> GetDisksAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        _ = await client.GetAsync("/webapi/entry.cgi?api=SYNO.Storage.CGI.Storage&method=load_info&version=1", cancellationToken);
        return [];
    }

    public async Task<IReadOnlyList<SynologyReplicationSnapshot>> GetReplicationTasksAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        _ = await client.GetAsync("/webapi/entry.cgi?api=SYNO.Backup.Replication.Task&method=list&version=1", cancellationToken);
        return [];
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        IntegrationVendorOptions opts = options.Value.Synology;
        IntegrationReadinessHelper.EnsureCallable(opts, "Synology");
        string? sid = await secrets.ResolveAsync(opts.CredentialReference, ct);
        if (string.IsNullOrEmpty(sid))
            throw new InvalidOperationException("Synology CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("integrations-synology");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("X-SYNO-TOKEN", sid);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string? Read(JsonElement el, params string[] names)
    {
        foreach (string n in names)
        {
            if (el.TryGetProperty(n, out JsonElement p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        return null;
    }
}

/// <summary>Read-only SonicWall Capture Client adapter (endpoint/detection telemetry — not vuln scanning).</summary>
public sealed class SonicWallHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    ILogger<SonicWallHttpClient> logger,
    IntegrationHealthState health) : ISonicWallCaptureClient
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.SonicWallCaptureClient,
            options.Value.SonicWallCaptureClient,
            lastSuccess: health.Get(IntegrationProvider.SonicWallCaptureClient)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.SonicWallCaptureClient)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.SonicWallCaptureClient)?.LastError,
            processed: health.Get(IntegrationProvider.SonicWallCaptureClient)?.LastProcessed,
            unmatched: health.Get(IntegrationProvider.SonicWallCaptureClient)?.LastUnmatched);

    public async Task<IReadOnlyList<SonicWallEndpointSnapshot>> GetEndpointsAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync("/api/v1/endpoints", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("SonicWall endpoints returned {Status}", (int)response.StatusCode);
            return [];
        }
        return [];
    }

    public async Task<IReadOnlyList<SonicWallDetectionSnapshot>> GetRecentDetectionsAsync(
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync($"/api/v1/detections?limit={Math.Clamp(maxResults, 1, 500)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];
        return [];
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        IntegrationVendorOptions opts = options.Value.SonicWallCaptureClient;
        IntegrationReadinessHelper.EnsureCallable(opts, "SonicWall Capture Client");
        string? token = await secrets.ResolveAsync(opts.CredentialReference, ct);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("SonicWall CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("integrations-sonicwall");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }
}
