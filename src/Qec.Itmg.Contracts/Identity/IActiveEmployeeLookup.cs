namespace Qec.Itmg.Contracts.Identity;

public sealed record ActiveEmployeeInfo(Guid Id, string Upn, string DisplayName);

/// <summary>Resolves active ITMG users for assignment fan-out (awareness, policies, etc.).</summary>
public interface IActiveEmployeeLookup
{
    Task<IReadOnlyList<ActiveEmployeeInfo>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<ActiveEmployeeInfo?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
