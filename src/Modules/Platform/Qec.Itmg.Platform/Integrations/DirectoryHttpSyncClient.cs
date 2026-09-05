using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.Platform.Integrations;

/// <summary>
/// Directory sync / JML adapter (Microsoft Graph or LDAP via BaseUrl + CredentialReference).
/// Disabled by default. Never stores passwords. Never bypasses AccessCase approvals.
/// </summary>
public sealed class DirectoryHttpSyncClient(
    IHttpClientFactory httpClientFactory,
    IOptions<IntegrationOptions> options,
    ISecretResolver secrets,
    ILogger<DirectoryHttpSyncClient> logger,
    IntegrationHealthState health) : IDirectorySyncClient
{
    public IntegrationReadiness GetReadiness() =>
        IntegrationReadinessHelper.FromOptions(
            IntegrationProvider.Directory,
            options.Value.Directory,
            lastSuccess: health.Get(IntegrationProvider.Directory)?.LastSuccessUtc,
            lastFailure: health.Get(IntegrationProvider.Directory)?.LastFailureUtc,
            lastError: health.Get(IntegrationProvider.Directory)?.LastError,
            processed: health.Get(IntegrationProvider.Directory)?.LastProcessed,
            unmatched: health.Get(IntegrationProvider.Directory)?.LastUnmatched);

    public async Task<IReadOnlyList<DirectoryUserSnapshot>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync(
            "/v1.0/users?$select=id,userPrincipalName,displayName,accountEnabled,department,jobTitle",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        List<DirectoryUserSnapshot> users = [];
        if (doc.RootElement.TryGetProperty("value", out JsonElement value))
        {
            foreach (JsonElement el in value.EnumerateArray())
            {
                string? id = el.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
                string? upn = el.TryGetProperty("userPrincipalName", out JsonElement upnEl) ? upnEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(upn))
                    continue;
                users.Add(new DirectoryUserSnapshot(
                    DirectoryObjectId: id,
                    Upn: upn,
                    DisplayName: el.TryGetProperty("displayName", out JsonElement dn) ? dn.GetString() ?? upn : upn,
                    Enabled: !el.TryGetProperty("accountEnabled", out JsonElement ae) || ae.ValueKind != JsonValueKind.False,
                    Department: el.TryGetProperty("department", out JsonElement dept) ? dept.GetString() : null,
                    JobTitle: el.TryGetProperty("jobTitle", out JsonElement jt) ? jt.GetString() : null,
                    GroupIds: [],
                    LastDirectoryChangeUtc: null));
            }
        }
        return users;
    }

    public async Task<DirectoryUserSnapshot?> GetUserAsync(string directoryObjectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryObjectId);
        using HttpClient client = await CreateClientAsync(cancellationToken);
        HttpResponseMessage response = await client.GetAsync(
            $"/v1.0/users/{Uri.EscapeDataString(directoryObjectId)}?$select=id,userPrincipalName,displayName,accountEnabled,department,jobTitle",
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement el = doc.RootElement;
        string? id = el.GetProperty("id").GetString();
        string? upn = el.GetProperty("userPrincipalName").GetString();
        if (id is null || upn is null) return null;
        return new DirectoryUserSnapshot(
            id, upn,
            el.TryGetProperty("displayName", out JsonElement dn) ? dn.GetString() ?? upn : upn,
            !el.TryGetProperty("accountEnabled", out JsonElement ae) || ae.ValueKind != JsonValueKind.False,
            el.TryGetProperty("department", out JsonElement dept) ? dept.GetString() : null,
            el.TryGetProperty("jobTitle", out JsonElement jt) ? jt.GetString() : null,
            [],
            null);
    }

    public async Task<DirectoryJmlActionResult> ExecuteJmlActionAsync(
        DirectoryJmlActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntegrationVendorOptions opts = options.Value.Directory;
        if (!opts.Enabled)
        {
            return new(false, true, "Directory", null,
                "Directory integration disabled — JML action not executed. AccessCase remains authoritative.");
        }
        if (!opts.IsConfigured)
        {
            return new(false, true, "Directory", null,
                "Directory integration not configured — JML action not executed.");
        }

        using HttpClient client = await CreateClientAsync(cancellationToken);
        string externalRef = $"{request.AccessCaseId:N}:{request.Action}:{request.EntitlementKey ?? "-"}";
        try
        {
            HttpResponseMessage response = request.Action switch
            {
                DirectoryJmlActionKind.DisableUser => await client.PatchAsync(
                    $"/v1.0/users/{Uri.EscapeDataString(request.TargetDirectoryObjectId)}",
                    new StringContent("""{"accountEnabled":false}""", System.Text.Encoding.UTF8, "application/json"),
                    cancellationToken),
                DirectoryJmlActionKind.EnableUser => await client.PatchAsync(
                    $"/v1.0/users/{Uri.EscapeDataString(request.TargetDirectoryObjectId)}",
                    new StringContent("""{"accountEnabled":true}""", System.Text.Encoding.UTF8, "application/json"),
                    cancellationToken),
                DirectoryJmlActionKind.AddGroupMembership when !string.IsNullOrWhiteSpace(request.ExternalGroupId) =>
                    await client.PostAsync(
                        $"/v1.0/groups/{Uri.EscapeDataString(request.ExternalGroupId)}/members/$ref",
                        new StringContent(
                            $"{{\"@odata.id\":\"https://graph.microsoft.com/v1.0/directoryObjects/{request.TargetDirectoryObjectId}\"}}",
                            System.Text.Encoding.UTF8,
                            "application/json"),
                        cancellationToken),
                DirectoryJmlActionKind.RemoveGroupMembership when !string.IsNullOrWhiteSpace(request.ExternalGroupId) =>
                    await client.DeleteAsync(
                        $"/v1.0/groups/{Uri.EscapeDataString(request.ExternalGroupId)}/members/{Uri.EscapeDataString(request.TargetDirectoryObjectId)}/$ref",
                        cancellationToken),
                DirectoryJmlActionKind.SyncMetadata => new HttpResponseMessage(System.Net.HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    ReasonPhrase = "Unsupported or incomplete JML action.",
                },
            };

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent
                || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return new(true, false, "Directory", externalRef, "JML action accepted by directory provider.");
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Directory JML action failed for case {Case} status {Status}", request.AccessCaseNumber, (int)response.StatusCode);
            return new(false, false, "Directory", externalRef,
                $"Directory rejected action ({(int)response.StatusCode}). Details omitted from logs/response for safety.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Directory JML action error for case {Case}", request.AccessCaseNumber);
            return new(false, false, "Directory", externalRef, "Directory JML action failed.");
        }
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        IntegrationVendorOptions opts = options.Value.Directory;
        IntegrationReadinessHelper.EnsureCallable(opts, "Directory");
        string? token = await secrets.ResolveAsync(opts.CredentialReference, ct);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("Directory CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("integrations-directory");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }
}
