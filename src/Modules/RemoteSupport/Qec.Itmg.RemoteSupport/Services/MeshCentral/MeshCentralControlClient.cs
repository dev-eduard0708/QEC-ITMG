using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Qec.Itmg.RemoteSupport.Services.MeshCentral;

/// <summary>
/// Minimal MeshCentral control-channel client using the documented <c>control.ashx</c> WebSocket protocol
/// (same mechanism as MeshCtrl). Does not invent REST session endpoints.
/// </summary>
public sealed class MeshCentralControlClient : IAsyncDisposable
{
    private readonly ILogger _logger;
    private ClientWebSocket? _ws;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveLoop;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MeshCentralControlClient(ILogger logger) => _logger = logger;

    public async Task ConnectAsync(
        Uri controlWsUri,
        MeshCentralCredentials credentials,
        CancellationToken cancellationToken)
    {
        _ws = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(credentials.Username) && !string.IsNullOrWhiteSpace(credentials.Password))
        {
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.Username))
                + ","
                + Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.Password));
            _ws.Options.SetRequestHeader("x-meshauth", auth);
        }

        await _ws.ConnectAsync(controlWsUri, cancellationToken);
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), CancellationToken.None);

        // Wait briefly for serverinfo / auth confirmation.
        await Task.Delay(300, cancellationToken);
        if (_ws.State != WebSocketState.Open)
            throw new InvalidOperationException("MeshCentral control channel closed during connect.");
    }

    public async Task<IReadOnlyList<MeshCentralNode>> ListNodesAsync(
        string? meshId,
        CancellationToken cancellationToken)
    {
        string responseId = "itmg-" + Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[responseId] = tcs;

        object cmd = string.IsNullOrWhiteSpace(meshId)
            ? new { action = "nodes", responseid = responseId }
            : new { action = "nodes", meshid = meshId.Trim(), responseid = responseId };

        await SendAsync(cmd, cancellationToken);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        JsonElement data;
        try
        {
            data = await tcs.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            _pending.TryRemove(responseId, out _);
        }

        return ParseNodes(data);
    }

    public async Task<MeshCentralInviteLink?> CreateInviteLinkAsync(
        string meshId,
        int expireHours,
        CancellationToken cancellationToken)
    {
        string responseId = "itmg-" + Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[responseId] = tcs;

        await SendAsync(new
        {
            action = "createInviteLink",
            meshid = meshId.Trim(),
            expire = Math.Max(0, expireHours),
            flags = 0,
            responseid = responseId,
        }, cancellationToken);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            JsonElement data = await tcs.Task.WaitAsync(timeout.Token);
            string? url = data.TryGetProperty("url", out JsonElement u) ? u.GetString()
                : data.TryGetProperty("link", out JsonElement l) ? l.GetString()
                : null;
            string? code = data.TryGetProperty("invite", out JsonElement i) ? i.GetString()
                : data.TryGetProperty("code", out JsonElement c) ? c.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(code))
                return null;
            return new MeshCentralInviteLink(url, code);
        }
        finally
        {
            _pending.TryRemove(responseId, out _);
        }
    }

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        string responseId = "itmg-" + Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[responseId] = tcs;
        await SendAsync(new { action = "serverinfo", responseid = responseId }, cancellationToken);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            _ = await tcs.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            _pending.TryRemove(responseId, out _);
        }
    }

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("MeshCentral control channel is not connected.");
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var message = new MemoryStream();
        try
        {
            while (_ws is { State: WebSocketState.Open } && !cancellationToken.IsCancellationRequested)
            {
                message.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string text = Encoding.UTF8.GetString(message.ToArray());
                HandleMessage(text);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MeshCentral receive loop ended");
            foreach (var pending in _pending)
                pending.Value.TrySetException(ex);
        }
    }

    private void HandleMessage(string text)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            JsonElement root = doc.RootElement.Clone();
            string? action = root.TryGetProperty("action", out JsonElement a) ? a.GetString() : null;
            string? responseId = root.TryGetProperty("responseid", out JsonElement r) ? r.GetString() : null;

            if (string.Equals(action, "close", StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("cause", out JsonElement cause)
                && string.Equals(cause.GetString(), "noauth", StringComparison.OrdinalIgnoreCase))
            {
                string msg = root.TryGetProperty("msg", out JsonElement m) ? (m.GetString() ?? "noauth") : "noauth";
                foreach (var pending in _pending)
                    pending.Value.TrySetException(new InvalidOperationException("MeshCentral authentication failed: " + msg));
                return;
            }

            if (!string.IsNullOrWhiteSpace(responseId) && _pending.TryRemove(responseId, out var tcs))
            {
                tcs.TrySetResult(root);
                return;
            }

            // MeshCentral sometimes returns nodes without responseid when meshes+nodes were requested.
            if (string.Equals(action, "nodes", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var kv in _pending.ToArray())
                {
                    if (kv.Key.StartsWith("itmg-", StringComparison.Ordinal)
                        && kv.Value.TrySetResult(root))
                    {
                        _pending.TryRemove(kv.Key, out _);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse MeshCentral control message");
        }
    }

    private static IReadOnlyList<MeshCentralNode> ParseNodes(JsonElement data)
    {
        List<MeshCentralNode> list = [];
        if (!data.TryGetProperty("nodes", out JsonElement nodesEl))
            return list;

        if (nodesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty mesh in nodesEl.EnumerateObject())
            {
                if (mesh.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (JsonElement n in mesh.Value.EnumerateArray())
                    TryAddNode(list, n, mesh.Name);
            }
        }
        else if (nodesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement n in nodesEl.EnumerateArray())
                TryAddNode(list, n, null);
        }

        return list;
    }

    private static void TryAddNode(List<MeshCentralNode> list, JsonElement n, string? meshId)
    {
        string? id = n.TryGetProperty("_id", out JsonElement idEl) ? idEl.GetString() : null;
        string? name = n.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return;
        int conn = n.TryGetProperty("conn", out JsonElement connEl) && connEl.TryGetInt32(out int c) ? c : 0;
        list.Add(new MeshCentralNode(id!, name ?? id!, meshId, conn != 0));
    }

    public async ValueTask DisposeAsync()
    {
        try { _receiveCts?.Cancel(); } catch { /* ignore */ }
        if (_receiveLoop is not null)
        {
            try { await _receiveLoop; } catch { /* ignore */ }
        }

        if (_ws is not null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { /* ignore */ }
            _ws.Dispose();
        }

        _receiveCts?.Dispose();
    }
}

public sealed record MeshCentralCredentials(string? Username, string? Password, string? LoginKeyHex);

public sealed record MeshCentralNode(string NodeId, string Name, string? MeshId, bool Online);

public sealed record MeshCentralInviteLink(string? Url, string? Code);

public static class MeshCentralCredentialParser
{
    public static MeshCentralCredentials Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        string trimmed = raw.Trim();
        if (trimmed.StartsWith('{'))
        {
            using JsonDocument doc = JsonDocument.Parse(trimmed);
            JsonElement root = doc.RootElement;
            string? user = root.TryGetProperty("username", out JsonElement u) ? u.GetString()
                : root.TryGetProperty("user", out JsonElement u2) ? u2.GetString() : null;
            string? pass = root.TryGetProperty("password", out JsonElement p) ? p.GetString()
                : root.TryGetProperty("pass", out JsonElement p2) ? p2.GetString() : null;
            string? key = root.TryGetProperty("loginKeyHex", out JsonElement k) ? k.GetString()
                : root.TryGetProperty("loginkey", out JsonElement k2) ? k2.GetString() : null;
            return new MeshCentralCredentials(user, pass, key);
        }

        int idx = trimmed.IndexOf(':');
        if (idx > 0)
            return new MeshCentralCredentials(trimmed[..idx], trimmed[(idx + 1)..], null);

        throw new InvalidOperationException(
            "RemoteSupport CredentialReference must resolve to username:password or JSON {username,password}.");
    }
}
