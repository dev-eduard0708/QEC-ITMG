using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>Read-only vCenter / Hyper-V enrichment adapter. No VM power commands.</summary>
public sealed class VirtualizationHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    ILogger<VirtualizationHttpClient> logger,
    IntegrationHealthState health) : IVirtualizationEnrichmentClient
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.Virtualization,
            options.Value.Virtualization,
            lastSuccess: health.Get(IntegrationProvider.Virtualization)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.Virtualization)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.Virtualization)?.LastError,
            processed: health.Get(IntegrationProvider.Virtualization)?.LastProcessed,
            unmatched: health.Get(IntegrationProvider.Virtualization)?.LastUnmatched);

    public async Task<IReadOnlyList<VirtualMachineSnapshot>> ListVirtualMachinesAsync(CancellationToken cancellationToken = default)
    {
        IntegrationVendorOptions opts = options.Value.Virtualization;
        IntegrationReadinessHelper.EnsureCallable(opts, "Virtualization");
        string kind = string.IsNullOrWhiteSpace(opts.ProviderKind) ? "vCenter" : opts.ProviderKind.Trim();
        using HttpClient client = await CreateClientAsync(cancellationToken);

        string path = kind.Equals("HyperV", StringComparison.OrdinalIgnoreCase)
            ? "/api/hyperv/vms"
            : "/rest/vcenter/vm";

        HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Virtualization list returned {Status} for {Kind}", (int)response.StatusCode, kind);
            return [];
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        List<VirtualMachineSnapshot> vms = [];
        JsonElement root = doc.RootElement.TryGetProperty("value", out JsonElement value) ? value : doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
            return vms;

        foreach (JsonElement el in root.EnumerateArray())
        {
            string? id = Read(el, "vm", "id", "Id", "Name");
            string? name = Read(el, "name", "Name", "ElementName") ?? id;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                continue;
            vms.Add(new VirtualMachineSnapshot(
                ExternalId: id,
                Name: name,
                Provider: kind,
                HostName: Read(el, "host_name", "HostName", "host"),
                PowerState: Read(el, "power_state", "PowerState", "state") ?? "Unknown",
                CpuCount: ReadInt(el, "cpu_count", "CpuCount"),
                MemoryMb: ReadLong(el, "memory_size_MiB", "MemoryMb"),
                DatastoreName: Read(el, "datastore", "Datastore"),
                PrimaryIp: Read(el, "ip_address", "PrimaryIp"),
                BiosUuid: Read(el, "identity.bios_uuid", "BiosUuid", "uuid")));
        }
        return vms;
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        IntegrationVendorOptions opts = options.Value.Virtualization;
        string? token = await secrets.ResolveAsync(opts.CredentialReference, ct);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("Virtualization CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("integrations-virtualization");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string? Read(JsonElement el, params string[] names)
    {
        foreach (string n in names)
        {
            if (n.Contains('.') && TryNested(el, n, out string? nested))
                return nested;
            if (el.TryGetProperty(n, out JsonElement p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        return null;
    }

    private static bool TryNested(JsonElement el, string path, out string? value)
    {
        value = null;
        string[] parts = path.Split('.');
        JsonElement current = el;
        foreach (string part in parts)
        {
            if (!current.TryGetProperty(part, out current))
                return false;
        }
        if (current.ValueKind == JsonValueKind.String)
        {
            value = current.GetString();
            return true;
        }
        return false;
    }

    private static int? ReadInt(JsonElement el, params string[] names)
    {
        foreach (string n in names)
        {
            if (el.TryGetProperty(n, out JsonElement p) && p.TryGetInt32(out int v))
                return v;
        }
        return null;
    }

    private static long? ReadLong(JsonElement el, params string[] names)
    {
        foreach (string n in names)
        {
            if (el.TryGetProperty(n, out JsonElement p) && p.TryGetInt64(out long v))
                return v;
        }
        return null;
    }
}
