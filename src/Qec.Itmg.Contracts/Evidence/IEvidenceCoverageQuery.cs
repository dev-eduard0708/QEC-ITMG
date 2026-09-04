namespace Qec.Itmg.Contracts.Evidence;

public sealed record EvidenceCoverageSnapshot(
    int ControlsWithAvailableEvidence,
    int ControlsMissingEvidence,
    int ControlsWithExpiredOnlyEvidence);

/// <summary>Implemented by Evidence module; consumed by Compliance coverage.</summary>
public interface IEvidenceCoverageQuery
{
    Task<EvidenceCoverageSnapshot> GetForControlsAsync(
        IReadOnlyCollection<Guid> internalControlIds,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);
}
