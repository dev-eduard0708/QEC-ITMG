namespace Qec.Itmg.Contracts.Integrations;

public sealed record VirtualMachineSnapshot(
    string ExternalId,
    string Name,
    string Provider,
    string? HostName,
    string PowerState,
    int? CpuCount,
    long? MemoryMb,
    string? DatastoreName,
    string? PrimaryIp,
    string? BiosUuid);

public enum VirtualizationMatchStatus
{
    Matched = 1,
    Unmatched = 2,
    Ambiguous = 3,
}

public sealed record VirtualizationEnrichmentResult(
    VirtualMachineSnapshot Snapshot,
    VirtualizationMatchStatus MatchStatus,
    Guid? ConfigurationItemId,
    string? MatchReason);

/// <summary>
/// Read-only virtualization enrichment (vCenter / Hyper-V). No power/snapshot commands.
/// </summary>
public interface IVirtualizationEnrichmentClient
{
    IntegrationReadiness GetReadiness();

    Task<IReadOnlyList<VirtualMachineSnapshot>> ListVirtualMachinesAsync(
        CancellationToken cancellationToken = default);
}
