using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.Contracts.Secrets;
using Qec.Itmg.RemoteSupport.Services.MeshCentral;

namespace Qec.Itmg.RemoteSupport.Services;

/// <summary>
/// MeshCentral adapter using the documented control.ashx WebSocket protocol (MeshCtrl-compatible)
/// and native <c>/meshagents</c> agent URLs. Does not call invented REST paths such as api/mesh/sessions.
/// </summary>
public sealed class MeshCentralRemoteSupportEngine(
    IOptions<RemoteSupportOptions> options,
    ISecretResolver secrets,
    RemoteEngineHealthState health,
    ILogger<MeshCentralRemoteSupportEngine> logger) : IRemoteSupportEngine
{
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
        return new(
            opts.Enabled,
            opts.IsConfigured,
            opts.ProviderKind,
            status,
            h.Ok,
            h.Fail,
            h.Err,
            opts.UnattendedEnabled);
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
        if (!TryParseSessionId(engineSessionId, out string nodeId, out DateTimeOffset started))
            return new RemoteEngineSessionInfo(engineSessionId, "unknown", null, null, null, null, null, null);

        RemoteSupportOptions opts = options.Value;
        if (!opts.Enabled || !opts.IsConfigured)
            return null;

        try
        {
            await using MeshCentralControlClient client = await ConnectAsync(cancellationToken);
            IReadOnlyList<MeshCentralNode> nodes = await client.ListNodesAsync(opts.MeshDeviceGroupId, cancellationToken);
            MeshCentralNode? node = nodes.FirstOrDefault(n =>
                string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                || n.NodeId.EndsWith(nodeId, StringComparison.OrdinalIgnoreCase));
            health.RecordSuccess("remote", DateTimeOffset.UtcNow);
            if (node is null)
                return new RemoteEngineSessionInfo(engineSessionId, "ended", started, DateTimeOffset.UtcNow, "Failed", "Node not found", null, null);
            return new RemoteEngineSessionInfo(
                engineSessionId,
                node.Online ? "active" : "ended",
                started,
                node.Online ? null : DateTimeOffset.UtcNow,
                node.Online ? null : "Completed",
                node.Online ? null : "Node offline",
                null,
                null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeshCentral GetSession presence check failed");
            health.RecordFailure("remote", DateTimeOffset.UtcNow, ex.Message);
            return null;
        }
    }

    public Task<RemoteEngineSessionResult> EndSessionAsync(
        string engineSessionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        // MeshCentral desktop sessions are browser-side; ITMG remains the authorization record.
        // Ending is acknowledged locally — presence polling updates InSession → Ended.
        _ = reason;
        _ = cancellationToken;
        return Task.FromResult(new RemoteEngineSessionResult(true, engineSessionId, null, null));
    }

    public async Task ProbeHealthAsync(CancellationToken cancellationToken = default)
    {
        RemoteSupportOptions opts = options.Value;
        if (!opts.Enabled || !opts.IsConfigured)
            return;
        try
        {
            await using MeshCentralControlClient client = await ConnectAsync(cancellationToken);
            await client.ProbeAsync(cancellationToken);
            health.RecordSuccess("remote", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            health.RecordFailure("remote", DateTimeOffset.UtcNow, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<MeshCentralNode>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        await using MeshCentralControlClient client = await ConnectAsync(cancellationToken);
        IReadOnlyList<MeshCentralNode> nodes = await client.ListNodesAsync(options.Value.MeshDeviceGroupId, cancellationToken);
        health.RecordSuccess("remote", DateTimeOffset.UtcNow);
        return nodes;
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
        if (string.IsNullOrWhiteSpace(request.EngineNodeId))
            return new(false, null, null, "Engine node id is required.");
        if (unattended && !opts.UnattendedEnabled)
            return new(false, null, null, "Unattended remote support is disabled.");

        try
        {
            await using MeshCentralControlClient client = await ConnectAsync(cancellationToken);
            IReadOnlyList<MeshCentralNode> nodes = await client.ListNodesAsync(opts.MeshDeviceGroupId, cancellationToken);
            MeshCentralNode? node = nodes.FirstOrDefault(n =>
                string.Equals(n.NodeId, request.EngineNodeId, StringComparison.OrdinalIgnoreCase)
                || n.NodeId.EndsWith(request.EngineNodeId, StringComparison.OrdinalIgnoreCase)
                || request.EngineNodeId.EndsWith(n.NodeId, StringComparison.OrdinalIgnoreCase));

            if (node is null)
            {
                health.RecordFailure("remote", DateTimeOffset.UtcNow, "Node not found in MeshCentral");
                return new(false, null, null, "Device was not found in MeshCentral.");
            }

            if (!node.Online)
            {
                health.RecordFailure("remote", DateTimeOffset.UtcNow, "Node offline");
                return new(false, null, null, "Device is offline in MeshCentral.");
            }

            string gotoNode = ExtractGotoNode(node.NodeId);
            string joinUrl = $"{opts.BaseUrl.TrimEnd('/')}/?viewmode=11&gotonode={Uri.EscapeDataString(gotoNode)}";
            string sessionId = $"mc:{gotoNode}:{request.RequestId:N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
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

    private async Task<MeshCentralControlClient> ConnectAsync(CancellationToken cancellationToken)
    {
        RemoteSupportOptions opts = options.Value;
        string? raw = await secrets.ResolveAsync(opts.CredentialReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("RemoteSupport CredentialReference could not be resolved.");

        MeshCentralCredentials credentials = MeshCentralCredentialParser.Parse(raw);
        Uri httpBase = new(opts.BaseUrl.TrimEnd('/') + "/");
        string wsScheme = string.Equals(httpBase.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var wsUri = new Uri($"{wsScheme}://{httpBase.Authority}/control.ashx");

        var client = new MeshCentralControlClient(logger);
        await client.ConnectAsync(wsUri, credentials, cancellationToken);
        return client;
    }

    private static string ExtractGotoNode(string nodeId)
    {
        // MeshCentral UI uses the trailing id segment for gotonode=
        string[] parts = nodeId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? nodeId : parts[^1];
    }

    private static bool TryParseSessionId(string engineSessionId, out string nodeId, out DateTimeOffset started)
    {
        nodeId = string.Empty;
        started = DateTimeOffset.UtcNow;
        // mc:{gotoNode}:{requestId}:{unix}
        string[] parts = engineSessionId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !string.Equals(parts[0], "mc", StringComparison.OrdinalIgnoreCase))
            return false;
        nodeId = parts[1];
        if (long.TryParse(parts[^1], out long unix))
            started = DateTimeOffset.FromUnixTimeSeconds(unix);
        return true;
    }
}
