using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Read-only Veeam Enterprise Manager / REST adapter.
/// Disabled by default. Never executes backup/restore/delete/config changes.
/// </summary>
public sealed class VeeamHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    ILogger<VeeamHttpClient> logger,
    IntegrationHealthState health) : IVeeamClient
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.Veeam,
            options.Value.Veeam,
            lastSuccess: health.Get(IntegrationProvider.Veeam)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.Veeam)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.Veeam)?.LastError,
            processed: health.Get(IntegrationProvider.Veeam)?.LastProcessed,
            unmatched: health.Get(IntegrationProvider.Veeam)?.LastUnmatched);

    public async Task<IReadOnlyList<VeeamJobRunSnapshot>> GetRecentJobRunsAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        // Enterprise Manager style query path; responses vary by version — normalize defensively.
        HttpResponseMessage response = await client.GetAsync($"/api/query?type=JobSession&pageSize={Math.Clamp(maxResults, 1, 200)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        List<VeeamJobRunSnapshot> items = [];
        if (doc.RootElement.TryGetProperty("Refs", out JsonElement refs) || doc.RootElement.TryGetProperty("data", out refs))
        {
            foreach (JsonElement el in refs.EnumerateArray().Take(maxResults))
            {
                items.Add(new VeeamJobRunSnapshot(
                    JobId: ReadString(el, "UID", "id", "JobUid") ?? Guid.NewGuid().ToString("N"),
                    JobName: ReadString(el, "Name", "JobName", "name") ?? "unknown",
                    Status: ReadString(el, "Result", "Status", "state") ?? "Unknown",
                    StartTime: ReadTime(el, "CreationTimeUTC", "StartTime", "started"),
                    EndTime: ReadTime(el, "EndTimeUTC", "EndTime", "ended"),
                    ProcessedObjects: ReadLong(el, "ProcessedObjects", "processedObjects"),
                    TransferredBytes: ReadLong(el, "TransferredSize", "transferredBytes")));
            }
        }
        return items;
    }

    public async Task<IReadOnlyList<VeeamProtectedWorkload>> GetProtectedWorkloadsAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync("/api/query?type=VmRestorePoint", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Veeam protected workloads query returned {Status}", (int)response.StatusCode);
            return [];
        }
        return [];
    }

    public async Task<IReadOnlyList<VeeamRepositorySnapshot>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync("/api/repositories", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];
        return [];
    }

    public async Task<IReadOnlyList<VeeamRestorePoint>> GetRestorePointsAsync(
        string? objectName = null,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        string path = string.IsNullOrWhiteSpace(objectName)
            ? "/api/query?type=VmRestorePoint"
            : $"/api/query?type=VmRestorePoint&filter=Name==\"{Uri.EscapeDataString(objectName)}\"";
        HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];
        return [];
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        IntegrationVendorOptions opts = options.Value.Veeam;
        IntegrationReadinessHelper.EnsureCallable(opts, "Veeam");
        string? token = await secrets.ResolveAsync(opts.CredentialReference, ct);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("Veeam CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("integrations-veeam");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string? ReadString(JsonElement el, params string[] names)
    {
        foreach (string n in names)
        {
            if (el.TryGetProperty(n, out JsonElement p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        return null;
    }

    private static DateTimeOffset? ReadTime(JsonElement el, params string[] names)
    {
        string? raw = ReadString(el, names);
        return DateTimeOffset.TryParse(raw, out DateTimeOffset dto) ? dto : null;
    }

    private static long ReadLong(JsonElement el, params string[] names)
    {
        foreach (string n in names)
        {
            if (el.TryGetProperty(n, out JsonElement p) && p.TryGetInt64(out long v))
                return v;
        }
        return 0;
    }
}
