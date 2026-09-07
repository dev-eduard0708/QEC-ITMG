namespace Qec.Itmg.Contracts.Organization;

public sealed record HeadOfficeAudienceMember(
    Guid UserId,
    string DisplayName,
    string Upn,
    string? DepartmentName,
    Guid? DepartmentId,
    string? PositionNames,
    Guid? PrimaryPositionId);

public sealed record HeadOfficeAudiencePreview(
    int DepartmentRuleCount,
    int PositionRuleCount,
    int SpecificUserRuleCount,
    int UniqueEmployees,
    IReadOnlyList<HeadOfficeAudienceMember> Members);

/// <summary>
/// Resolves Head Office–eligible employees for Security Awareness V1 audience targeting.
/// Eligibility: active Employee users with at least one active DepartmentMembership.
/// </summary>
public interface IHeadOfficeAudienceResolver
{
    Task<IReadOnlyList<HeadOfficeAudienceMember>> ListEligibleEmployeesAsync(CancellationToken ct);

    Task<HeadOfficeAudiencePreview> PreviewAsync(
        bool allHeadOffice,
        IReadOnlyList<Guid>? departmentIds,
        IReadOnlyList<Guid>? positionIds,
        IReadOnlyList<Guid>? userIds,
        CancellationToken ct);
}
