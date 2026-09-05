using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.Contracts.Secrets;

namespace Qec.Itmg.RemoteSupport.Services;

public sealed class RemoteEngineHealthState
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset? Ok, DateTimeOffset? Fail, string? Err)> _state = new();

    public void RecordSuccess(string key, DateTimeOffset at) =>
        _state.AddOrUpdate(key, _ => (at, null, null), (_, prev) => (at, prev.Fail, null));

    public void RecordFailure(string key, DateTimeOffset at, string error) =>
        _state.AddOrUpdate(key, _ => (null, at, Trunc(error)), (_, prev) => (prev.Ok, at, Trunc(error)));

    public (DateTimeOffset? Ok, DateTimeOffset? Fail, string? Err) Get(string key) =>
        _state.TryGetValue(key, out var v) ? v : default;

    private static string Trunc(string e) => e.Length <= 400 ? e : e[..400];
}

public sealed class DisabledRemoteSupportEngine(
    IOptions<RemoteSupportOptions> options,
    RemoteEngineHealthState health) : IRemoteSupportEngine
{
    public RemoteEngineStatus GetStatus()
    {
        RemoteSupportOptions opts = options.Value;
        var h = health.Get("remote");
        string status = !opts.Enabled ? "Disabled" : opts.IsConfigured ? "Configured" : "NotConfigured";
        if (!opts.Enabled) status = "Disabled";
        else if (!opts.IsConfigured) status = "NotConfigured";
        return new(opts.Enabled, opts.IsConfigured, opts.ProviderKind, status, h.Ok, h.Fail, h.Err, opts.UnattendedEnabled);
    }

    public Task<RemoteEngineSessionResult> CreateAttendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FailDisabled());

    public Task<RemoteEngineSessionResult> CreateUnattendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FailDisabled());

    public Task<RemoteEngineSessionInfo?> GetSessionAsync(
        string engineSessionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<RemoteEngineSessionInfo?>(null);

    public Task<RemoteEngineSessionResult> EndSessionAsync(
        string engineSessionId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FailDisabled());

    private static RemoteEngineSessionResult FailDisabled() =>
        new(false, null, null, "Remote support engine unavailable");
}

/// <summary>
/// MeshCentral adapter using service-account credentials from ISecretResolver.
/// Does not implement screen transport — MeshCentral owns that.
/// </summary>
public sealed class MeshCentralRemoteSupportEngine(
    IHttpClientFactory httpClientFactory,
    IOptions<RemoteSupportOptions> options,
    ISecretResolver secrets,
    RemoteEngineHealthState health,
    ILogger<MeshCentralRemoteSupportEngine> logger) : IRemoteSupportEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public RemoteEngineStatus GetStatus()
    {
        RemoteSupportOptions opts = options.Value;
        var h = health.Get("remote");
        string status;
        if (!opts.Enabled) status = "Disabled";
        else if (!opts.IsConfigured) status = "NotConfigured";
        else if (h.Fail is not null && (h.Ok is null || h.Fail > h.Ok)) status = "Unhealthy";
        else if (h.Ok is not null) status = "Healthy";
        else status = "Configured";
        return new(opts.Enabled, opts.IsConfigured, opts.ProviderKind, status, h.Ok, h.Fail, h.Err, opts.UnattendedEnabled);
    }

    public Task<RemoteEngineSessionResult> CreateAttendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSessionCoreAsync(request, unattended: false, cancellationToken);

    public Task<RemoteEngineSessionResult> CreateUnattendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSessionCoreAsync(request, unattended: true, cancellationToken);

    public async Task<RemoteEngineSessionInfo?> GetSessionAsync(
        string engineSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineSessionId);
        RemoteSupportOptions opts = options.Value;
        if (!opts.Enabled || !opts.IsConfigured)
            return null;

        try
        {
            using HttpClient client = await CreateAuthorizedClientAsync(cancellationToken);
            using HttpResponseMessage response = await client.GetAsync(
                $"api/mesh/sessions/{Uri.EscapeDataString(engineSessionId.Trim())}",
                cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            health.RecordSuccess("remote", DateTimeOffset.UtcNow);
            return new RemoteEngineSessionInfo(
                engineSessionId.Trim(),
                ReadString(root, "status") ?? "unknown",
                ReadDate(root, "startedAt"),
                ReadDate(root, "endedAt"),
                ReadString(root, "outcome"),
                ReadString(root, "endReason"),
                ReadBool(root, "elevationUsed"),
                ReadString(root, "recordingReference"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeshCentral GetSession failed for {SessionId}", engineSessionId);
            health.RecordFailure("remote", DateTimeOffset.UtcNow, ex.Message);
            return null;
        }
    }

    public async Task<RemoteEngineSessionResult> EndSessionAsync(
        string engineSessionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineSessionId);
        RemoteSupportOptions opts = options.Value;
        if (!opts.Enabled || !opts.IsConfigured)
            return new(false, null, null, "Remote support engine unavailable");

        try
        {
            using HttpClient client = await CreateAuthorizedClientAsync(cancellationToken);
            var payload = new { reason };
            using HttpResponseMessage response = await client.PostAsync(
                $"api/mesh/sessions/{Uri.EscapeDataString(engineSessionId.Trim())}/end",
                new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                health.RecordFailure("remote", DateTimeOffset.UtcNow, Trunc(body));
                return new(false, engineSessionId, null, $"Engine end failed ({(int)response.StatusCode})");
            }

            health.RecordSuccess("remote", DateTimeOffset.UtcNow);
            return new(true, engineSessionId.Trim(), null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeshCentral EndSession failed for {SessionId}", engineSessionId);
            health.RecordFailure("remote", DateTimeOffset.UtcNow, ex.Message);
            return new(false, engineSessionId, null, "Remote support engine unavailable");
        }
    }

    private async Task<RemoteEngineSessionResult> CreateSessionCoreAsync(
        CreateRemoteEngineSessionRequest request,
        bool unattended,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RemoteSupportOptions opts = options.Value;
        if (!opts.Enabled || !opts.IsConfigured)
            return new(false, null, null, "Remote support engine unavailable");

        try
        {
            using HttpClient client = await CreateAuthorizedClientAsync(cancellationToken);
            var payload = new
            {
                nodeId = request.EngineNodeId,
                requestId = request.RequestId.ToString("D"),
                remoteNumber = request.RemoteNumber,
                sessionType = request.SessionType,
                technicianUserId = request.TechnicianUserId.ToString("D"),
                targetUserId = request.TargetUserId?.ToString("D"),
                reason = request.Reason,
                privileges = request.RequestedPrivileges,
                unattended,
            };

            using HttpResponseMessage response = await client.PostAsync(
                "api/mesh/sessions",
                new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                cancellationToken);

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                health.RecordFailure("remote", DateTimeOffset.UtcNow, Trunc(body));
                return new(false, null, null, $"Engine create failed ({(int)response.StatusCode})");
            }

            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            string? sessionId = ReadString(doc.RootElement, "sessionId")
                ?? ReadString(doc.RootElement, "id")
                ?? Guid.CreateVersion7().ToString("N");
            string? joinUrl = ReadString(doc.RootElement, "joinUrl")
                ?? ReadString(doc.RootElement, "url");
            health.RecordSuccess("remote", DateTimeOffset.UtcNow);
            return new(true, sessionId, joinUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeshCentral CreateSession failed for request {RequestId}", request.RequestId);
            health.RecordFailure("remote", DateTimeOffset.UtcNow, ex.Message);
            return new(false, null, null, "Remote support engine unavailable");
        }
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        RemoteSupportOptions opts = options.Value;
        string? token = await secrets.ResolveAsync(opts.CredentialReference, cancellationToken);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("RemoteSupport CredentialReference could not be resolved.");

        HttpClient client = httpClientFactory.CreateClient("remote-meshcentral");
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(el.GetString(), out DateTimeOffset dt)
            ? dt
            : null;

    private static bool? ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement el) && (el.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? el.GetBoolean()
            : null;

    private static string Trunc(string value) => value.Length <= 400 ? value : value[..400];
}

public sealed class ConfigurableRemoteSupportEngine(
    IOptions<RemoteSupportOptions> options,
    DisabledRemoteSupportEngine disabled,
    MeshCentralRemoteSupportEngine meshCentral) : IRemoteSupportEngine
{
    private IRemoteSupportEngine Resolve()
    {
        RemoteSupportOptions opts = options.Value;
        if (!opts.Enabled
            || string.Equals(opts.ProviderKind, "Disabled", StringComparison.OrdinalIgnoreCase)
            || !opts.IsConfigured)
        {
            return disabled;
        }

        if (string.Equals(opts.ProviderKind, "MeshCentral", StringComparison.OrdinalIgnoreCase))
            return meshCentral;

        return disabled;
    }

    public RemoteEngineStatus GetStatus() => Resolve().GetStatus();

    public Task<RemoteEngineSessionResult> CreateAttendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve().CreateAttendedSessionAsync(request, cancellationToken);

    public Task<RemoteEngineSessionResult> CreateUnattendedSessionAsync(
        CreateRemoteEngineSessionRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve().CreateUnattendedSessionAsync(request, cancellationToken);

    public Task<RemoteEngineSessionInfo?> GetSessionAsync(
        string engineSessionId,
        CancellationToken cancellationToken = default) =>
        Resolve().GetSessionAsync(engineSessionId, cancellationToken);

    public Task<RemoteEngineSessionResult> EndSessionAsync(
        string engineSessionId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        Resolve().EndSessionAsync(engineSessionId, reason, cancellationToken);
}
