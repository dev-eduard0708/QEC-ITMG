namespace Qec.Itmg.Contracts.Governance;

public sealed record InternalControlRefDto(
    Guid Id,
    string ControlNumber,
    string Title,
    string Status,
    Guid? PrimaryOwnerUserId);

/// <summary>Implemented by Governance; consumed by Compliance readiness detail.</summary>
public interface IInternalControlLookup
{
    Task<IReadOnlyDictionary<Guid, InternalControlRefDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> internalControlIds,
        CancellationToken cancellationToken = default);
}
