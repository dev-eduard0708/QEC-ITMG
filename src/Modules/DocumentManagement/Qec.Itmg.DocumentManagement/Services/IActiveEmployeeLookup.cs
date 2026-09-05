namespace Qec.Itmg.DocumentManagement.Services;

public sealed record ActiveEmployeeInfo(Guid Id, string Upn, string DisplayName);

/// <summary>Resolves active ITMG users for policy assignment fan-out (implemented in Host/Identity).</summary>
public interface IActiveEmployeeLookup
{
    Task<IReadOnlyList<ActiveEmployeeInfo>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<ActiveEmployeeInfo?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
