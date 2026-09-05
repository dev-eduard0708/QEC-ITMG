using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Qec.Itmg.Contracts.RemoteSupport;

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
