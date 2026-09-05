namespace Qec.Itmg.Contracts.Continuity;

public sealed record DrTestCoverageSnapshot(int CriticalServicesMissingRecentDrTest);

/// <summary>Implemented by BCM module; consumed by audit readiness.</summary>
public interface IDrTestCoverageQuery
{
    Task<DrTestCoverageSnapshot> GetMissingForCriticalServicesAsync(
        IReadOnlyCollection<(Guid ServiceId, string Criticality)> services,
        DateTimeOffset asOfUtc,
        int recentDays = 365,
        CancellationToken cancellationToken = default);
}
