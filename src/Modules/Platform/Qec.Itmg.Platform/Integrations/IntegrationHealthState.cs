using System.Collections.Concurrent;
using Qec.Itmg.Contracts.Integrations;

namespace Qec.Itmg.Platform.Integrations;

public sealed class IntegrationHealthSnapshot
{
    public DateTimeOffset? LastSuccessUtc { get; set; }
    public DateTimeOffset? LastFailureUtc { get; set; }
    public string? LastError { get; set; }
    public int? LastProcessed { get; set; }
    public int? LastUnmatched { get; set; }
}

/// <summary>In-memory last-run health hints used by readiness (persisted history lives in IntegrationRun).</summary>
public sealed class IntegrationHealthState
{
    private readonly ConcurrentDictionary<IntegrationProvider, IntegrationHealthSnapshot> _state = new();

    public IntegrationHealthSnapshot? Get(IntegrationProvider provider) =>
        _state.TryGetValue(provider, out IntegrationHealthSnapshot? snap) ? snap : null;

    public void RecordSuccess(IntegrationProvider provider, DateTimeOffset at, int processed, int unmatched = 0)
    {
        IntegrationHealthSnapshot snap = _state.GetOrAdd(provider, _ => new IntegrationHealthSnapshot());
        snap.LastSuccessUtc = at;
        snap.LastProcessed = processed;
        snap.LastUnmatched = unmatched;
        snap.LastError = null;
    }

    public void RecordFailure(IntegrationProvider provider, DateTimeOffset at, string error)
    {
        IntegrationHealthSnapshot snap = _state.GetOrAdd(provider, _ => new IntegrationHealthSnapshot());
        snap.LastFailureUtc = at;
        snap.LastError = error.Length > 500 ? error[..500] : error;
    }
}
