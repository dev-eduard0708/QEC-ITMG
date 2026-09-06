namespace Qec.Itmg.Contracts.Evidence;

public sealed record EvidenceCoverageSnapshot(
    int ControlsWithAvailableEvidence,
    int ControlsMissingEvidence,
    int ControlsWithExpiredOnlyEvidence);

public sealed record EvidenceControlCoverageItem(
    Guid InternalControlId,
    bool HasAvailableEvidence,
    bool HasExpiredOnlyEvidence);

/// <summary>Implemented by Evidence module; consumed by Compliance coverage/readiness.</summary>
public interface IEvidenceCoverageQuery
{
    Task<EvidenceCoverageSnapshot> GetForControlsAsync(
        IReadOnlyCollection<Guid> internalControlIds,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvidenceControlCoverageItem>> GetPerControlAsync(
        IReadOnlyCollection<Guid> internalControlIds,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);
}
