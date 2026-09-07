using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Contracts.Organization;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Persistence;

namespace Qec.Itmg.Host.Organization;

public sealed class HeadOfficeAudienceResolver(
    IdentityDbContext identityDb,
    OrganizationDbContext orgDb) : IHeadOfficeAudienceResolver
{
    public async Task<IReadOnlyList<HeadOfficeAudienceMember>> ListEligibleEmployeesAsync(CancellationToken ct)
    {
        Dictionary<Guid, MemberCore> eligible = await LoadEligibleCoresAsync(ct);
        return BuildMembers(eligible.Values).OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<HeadOfficeAudiencePreview> PreviewAsync(
        bool allHeadOffice,
        IReadOnlyList<Guid>? departmentIds,
        IReadOnlyList<Guid>? positionIds,
        IReadOnlyList<Guid>? userIds,
        CancellationToken ct)
    {
        List<Guid> deptIds = (departmentIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        List<Guid> posIds = (positionIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        List<Guid> specificIds = (userIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();

        Dictionary<Guid, MemberCore> eligible = await LoadEligibleCoresAsync(ct);
        HashSet<Guid> selected = [];

        if (allHeadOffice)
        {
            foreach (Guid id in eligible.Keys)
                selected.Add(id);
        }

        if (deptIds.Count > 0)
        {
            foreach (MemberCore m in eligible.Values)
            {
                if (m.DepartmentIds.Any(d => deptIds.Contains(d)))
                    selected.Add(m.UserId);
            }
        }

        if (posIds.Count > 0)
        {
            foreach (MemberCore m in eligible.Values)
            {
                if (m.PositionIds.Any(p => posIds.Contains(p)))
                    selected.Add(m.UserId);
            }
        }

        if (specificIds.Count > 0)
        {
            foreach (Guid id in specificIds)
            {
                if (eligible.ContainsKey(id))
                    selected.Add(id);
            }
        }

        List<HeadOfficeAudienceMember> members = BuildMembers(
                selected.Select(id => eligible[id]))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HeadOfficeAudiencePreview(
            deptIds.Count,
            posIds.Count,
            specificIds.Count,
            members.Count,
            members);
    }

    private async Task<Dictionary<Guid, MemberCore>> LoadEligibleCoresAsync(CancellationToken ct)
    {
        List<(Guid Id, string Upn, string DisplayName)> users = await identityDb.Users.AsNoTracking()
            .Where(x => x.Status == UserStatus.Active && x.UserType == UserType.Employee)
            .Select(x => new ValueTuple<Guid, string, string>(x.Id, x.Upn, x.DisplayName))
            .ToListAsync(ct);

        if (users.Count == 0)
            return new Dictionary<Guid, MemberCore>();

        HashSet<Guid> userIds = users.Select(x => x.Id).ToHashSet();

        List<DepartmentMembership> memberships = await orgDb.DepartmentMemberships.AsNoTracking()
            .Where(x => x.EffectiveTo == null && userIds.Contains(x.UserId))
            .ToListAsync(ct);

        HashSet<Guid> withMembership = memberships.Select(x => x.UserId).ToHashSet();
        if (withMembership.Count == 0)
            return new Dictionary<Guid, MemberCore>();

        users = users.Where(x => withMembership.Contains(x.Id)).ToList();
        userIds = withMembership;

        HashSet<Guid> departmentIds = memberships.Select(x => x.DepartmentId).ToHashSet();
        Dictionary<Guid, string> departments = await orgDb.Departments.AsNoTracking()
            .Where(x => departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        List<PositionAssignment> assignments = await orgDb.PositionAssignments.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && (x.EffectiveTo == null || x.EffectiveTo > DateTimeOffset.UtcNow))
            .ToListAsync(ct);

        HashSet<Guid> positionIds = assignments.Select(x => x.PositionId).ToHashSet();
        Dictionary<Guid, Position> positions = positionIds.Count == 0
            ? new Dictionary<Guid, Position>()
            : await orgDb.Positions.AsNoTracking()
                .Where(x => positionIds.Contains(x.Id) && x.IsActive)
                .ToDictionaryAsync(x => x.Id, ct);

        Dictionary<Guid, MemberCore> result = new();
        foreach ((Guid id, string upn, string displayName) in users)
        {
            List<DepartmentMembership> userMemberships = memberships.Where(m => m.UserId == id).ToList();
            DepartmentMembership? primaryMembership =
                userMemberships.FirstOrDefault(m => m.IsPrimary) ?? userMemberships.FirstOrDefault();
            Guid? departmentId = primaryMembership?.DepartmentId;
            string? departmentName = departmentId is Guid did && departments.TryGetValue(did, out string? dn)
                ? dn
                : null;

            List<PositionAssignment> userPositions = assignments
                .Where(a => a.UserId == id && positions.ContainsKey(a.PositionId))
                .ToList();
            PositionAssignment? primaryPos =
                userPositions.FirstOrDefault(a => a.IsPrimary) ?? userPositions.FirstOrDefault();
            Guid? primaryPositionId = primaryPos?.PositionId;
            string? positionNames = userPositions.Count == 0
                ? null
                : string.Join(", ",
                    userPositions
                        .Select(a => positions[a.PositionId].NameEn)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            result[id] = new MemberCore(
                id,
                displayName,
                upn,
                departmentName,
                departmentId,
                positionNames,
                primaryPositionId,
                userMemberships.Select(m => m.DepartmentId).Distinct().ToList(),
                userPositions.Select(a => a.PositionId).Distinct().ToList());
        }

        return result;
    }

    private static IEnumerable<HeadOfficeAudienceMember> BuildMembers(IEnumerable<MemberCore> cores) =>
        cores.Select(m => new HeadOfficeAudienceMember(
            m.UserId, m.DisplayName, m.Upn,
            m.DepartmentName, m.DepartmentId,
            m.PositionNames, m.PrimaryPositionId));

    private sealed record MemberCore(
        Guid UserId,
        string DisplayName,
        string Upn,
        string? DepartmentName,
        Guid? DepartmentId,
        string? PositionNames,
        Guid? PrimaryPositionId,
        IReadOnlyList<Guid> DepartmentIds,
        IReadOnlyList<Guid> PositionIds);
}
