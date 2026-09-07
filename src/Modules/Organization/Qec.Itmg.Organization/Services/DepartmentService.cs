using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Persistence;

namespace Qec.Itmg.Organization.Services;

public sealed record DepartmentDetailDto(
    Guid Id,
    string NameEn,
    string? NameAr,
    string Code,
    string? DescriptionEn,
    string? DescriptionAr,
    Guid? ParentDepartmentId,
    bool IsActive,
    int SortOrder,
    int MemberCount,
    int PositionCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion);

public sealed record CompanyDepartmentCardDto(
    Guid Id,
    string NameEn,
    string? NameAr,
    string Code,
    Guid? ParentDepartmentId,
    bool IsActive,
    int PeopleCount,
    int PositionCount,
    int SortOrder);

public sealed record DepartmentMemberDto(
    Guid UserId,
    string DisplayName,
    string Upn,
    string? AvatarUrl,
    bool IsPrimary,
    bool IsActiveUser,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset CreatedAtUtc);

public sealed record PeopleRowDto(
    Guid UserId,
    string DisplayName,
    string Upn,
    string? AvatarUrl,
    bool IsActive,
    Guid? PrimaryDepartmentId,
    string? PrimaryDepartmentName,
    int AdditionalDepartmentCount,
    IReadOnlyList<string> PositionNames);

public sealed record UserOrgProfileDto(
    Guid UserId,
    string DisplayName,
    string Upn,
    string? AvatarUrl,
    bool IsActive,
    Guid? PrimaryDepartmentId,
    string? PrimaryDepartmentName,
    IReadOnlyList<DepartmentMemberShipSummaryDto> Departments,
    IReadOnlyList<UserPositionDto> Positions);

public sealed record DepartmentMemberShipSummaryDto(
    Guid DepartmentId,
    string NameEn,
    string? NameAr,
    string Code,
    bool IsPrimary);

public sealed class DepartmentService(
    OrganizationDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction,
    IUserDisplayLookup userDisplayLookup,
    IActiveEmployeeLookup employees)
{
    public async Task<IReadOnlyList<DepartmentDetailDto>> ListDetailedAsync(bool activeOnly, CancellationToken ct)
    {
        IQueryable<Department> query = db.Departments.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        List<Department> departments = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        return await MapDetailsAsync(departments, ct);
    }

    public async Task<IReadOnlyList<CompanyDepartmentCardDto>> CompanyViewAsync(CancellationToken ct)
    {
        IReadOnlyList<DepartmentDetailDto> details = await ListDetailedAsync(activeOnly: true, ct);
        return details
            .Select(d => new CompanyDepartmentCardDto(
                d.Id,
                d.NameEn,
                d.NameAr,
                d.Code,
                d.ParentDepartmentId,
                d.IsActive,
                d.MemberCount,
                d.PositionCount,
                d.SortOrder))
            .ToList();
    }

    public async Task<DepartmentDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        Department? department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (department is null)
        {
            return null;
        }

        IReadOnlyList<DepartmentDetailDto> mapped = await MapDetailsAsync([department], ct);
        return mapped[0];
    }

    public async Task<DepartmentDetailDto> CreateAsync(
        string nameEn,
        string? nameAr,
        string code,
        string? descriptionEn,
        string? descriptionAr,
        Guid? parentDepartmentId,
        int sortOrder,
        CancellationToken ct)
    {
        string normalizedCode = Department.NormalizeCode(code);
        if (await db.Departments.AnyAsync(x => x.Code == normalizedCode, ct))
        {
            throw new InvalidOperationException("Department code already exists.");
        }

        if (await db.Departments.AnyAsync(x => x.Name == nameEn.Trim(), ct))
        {
            throw new InvalidOperationException("Department name already exists.");
        }

        if (parentDepartmentId is Guid parentId)
        {
            bool parentExists = await db.Departments.AnyAsync(x => x.Id == parentId, ct);
            if (!parentExists)
            {
                throw new InvalidOperationException("Parent department was not found.");
            }
        }

        Department entity = Department.Create(
            nameEn,
            clock.UtcNow,
            descriptionEn,
            normalizedCode,
            nameAr,
            descriptionAr,
            parentDepartmentId,
            sortOrder);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            db.Departments.Add(entity);
            await businessAudit.AppendAsync(DepartmentAudit.Created(entity), innerCt);
        }, ct);

        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<DepartmentDetailDto> UpdateAsync(
        Guid id,
        string nameEn,
        string? nameAr,
        string code,
        string? descriptionEn,
        string? descriptionAr,
        Guid? parentDepartmentId,
        int sortOrder,
        bool isActive,
        CancellationToken ct)
    {
        Department entity = await db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Department was not found.");

        string normalizedCode = Department.NormalizeCode(code);
        if (await db.Departments.AnyAsync(x => x.Code == normalizedCode && x.Id != id, ct))
        {
            throw new InvalidOperationException("Department code already exists.");
        }

        if (await db.Departments.AnyAsync(x => x.Name == nameEn.Trim() && x.Id != id, ct))
        {
            throw new InvalidOperationException("Department name already exists.");
        }

        if (parentDepartmentId is Guid parentId)
        {
            if (parentId == id)
            {
                throw new InvalidOperationException("A department cannot be its own parent.");
            }

            bool parentExists = await db.Departments.AnyAsync(x => x.Id == parentId, ct);
            if (!parentExists)
            {
                throw new InvalidOperationException("Parent department was not found.");
            }

            await EnsureNoDepartmentParentCycleAsync(id, parentId, ct);
        }

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            entity.UpdateDetails(
                nameEn,
                nameAr,
                normalizedCode,
                descriptionEn,
                descriptionAr,
                parentDepartmentId,
                sortOrder,
                isActive,
                clock.UtcNow);
            await businessAudit.AppendAsync(DepartmentAudit.Updated(entity), innerCt);
        }, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task<DepartmentDetailDto> DeactivateAsync(Guid id, CancellationToken ct)
    {
        Department entity = await db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Department was not found.");

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            entity.Deactivate(clock.UtcNow);
            await businessAudit.AppendAsync(DepartmentAudit.Deactivated(entity), innerCt);
        }, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task<IReadOnlyList<DepartmentMemberDto>> ListMembersAsync(Guid departmentId, CancellationToken ct)
    {
        bool exists = await db.Departments.AsNoTracking().AnyAsync(x => x.Id == departmentId, ct);
        if (!exists)
        {
            throw new InvalidOperationException("Department was not found.");
        }

        List<DepartmentMembership> memberships = await db.DepartmentMemberships.AsNoTracking()
            .Where(x => x.DepartmentId == departmentId && x.EffectiveTo == null)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        IReadOnlyDictionary<Guid, UserDisplayInfo> users =
            await userDisplayLookup.GetManyAsync(memberships.Select(m => m.UserId), ct);

        return memberships.Select(m =>
        {
            users.TryGetValue(m.UserId, out UserDisplayInfo? info);
            return new DepartmentMemberDto(
                m.UserId,
                info?.DisplayName ?? m.UserId.ToString(),
                info?.Upn ?? string.Empty,
                info?.AvatarUrl,
                m.IsPrimary,
                info?.IsActive ?? false,
                m.EffectiveFrom,
                m.CreatedAtUtc);
        }).ToList();
    }

    public async Task<DepartmentMemberDto> AddMemberAsync(
        Guid departmentId,
        Guid userId,
        bool isPrimary,
        CancellationToken ct)
    {
        Department department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Department was not found.");

        if (!department.IsActive)
        {
            throw new InvalidOperationException("Inactive departments do not accept new members.");
        }

        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        bool duplicate = await db.DepartmentMemberships.AnyAsync(
            x => x.DepartmentId == departmentId && x.UserId == userId && x.EffectiveTo == null,
            ct);
        if (duplicate)
        {
            throw new InvalidOperationException("User is already a member of this department.");
        }

        DepartmentMembership membership = DepartmentMembership.Create(
            departmentId, userId, clock.UtcNow, isPrimary);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            if (isPrimary)
            {
                await ClearPrimaryForUserAsync(userId, exceptMembershipId: null, innerCt);
                membership.SetPrimary(true, clock.UtcNow);
            }

            db.DepartmentMemberships.Add(membership);
            await businessAudit.AppendAsync(DepartmentAudit.MemberAdded(department, userId), innerCt);
        }, ct);

        IReadOnlyList<DepartmentMemberDto> members = await ListMembersAsync(departmentId, ct);
        return members.First(m => m.UserId == userId);
    }

    public async Task RemoveMemberAsync(Guid departmentId, Guid userId, CancellationToken ct)
    {
        Department department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Department was not found.");

        DepartmentMembership membership = await db.DepartmentMemberships
            .FirstOrDefaultAsync(
                x => x.DepartmentId == departmentId && x.UserId == userId && x.EffectiveTo == null,
                ct)
            ?? throw new InvalidOperationException("Membership was not found.");

        int openPositions = await (
            from assignment in db.PositionAssignments.AsNoTracking()
            join position in db.Positions.AsNoTracking() on assignment.PositionId equals position.Id
            where assignment.UserId == userId && position.DepartmentId == departmentId
            select assignment.Id).CountAsync(ct);

        if (openPositions > 0)
        {
            throw new InvalidOperationException(
                $"This user still holds {openPositions} position(s) in {department.Name}. Resolve position assignments before removing department membership.");
        }

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            membership.EndMembership(clock.UtcNow);
            await businessAudit.AppendAsync(DepartmentAudit.MemberRemoved(department, userId), innerCt);
        }, ct);
    }

    public async Task SetPrimaryAsync(Guid departmentId, Guid userId, CancellationToken ct)
    {
        Department department = await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Department was not found.");

        DepartmentMembership membership = await db.DepartmentMemberships
            .FirstOrDefaultAsync(
                x => x.DepartmentId == departmentId && x.UserId == userId && x.EffectiveTo == null,
                ct)
            ?? throw new InvalidOperationException("Membership was not found.");

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await ClearPrimaryForUserAsync(userId, exceptMembershipId: membership.Id, innerCt);
            membership.SetPrimary(true, clock.UtcNow);
            await businessAudit.AppendAsync(DepartmentAudit.PrimaryChanged(department, userId), innerCt);
        }, ct);
    }

    public async Task EnsureMembershipAsync(Guid departmentId, Guid userId, CancellationToken ct)
    {
        bool exists = await db.DepartmentMemberships.AnyAsync(
            x => x.DepartmentId == departmentId && x.UserId == userId && x.EffectiveTo == null,
            ct);
        if (exists)
        {
            return;
        }

        await AddMemberAsync(departmentId, userId, isPrimary: false, ct);
    }

    public async Task<bool> IsMemberAsync(Guid departmentId, Guid userId, CancellationToken ct) =>
        await db.DepartmentMemberships.AsNoTracking().AnyAsync(
            x => x.DepartmentId == departmentId && x.UserId == userId && x.EffectiveTo == null,
            ct);

    public async Task<IReadOnlyList<PeopleRowDto>> ListPeopleAsync(
        string? search,
        Guid? departmentId,
        bool? activeOnly,
        CancellationToken ct)
    {
        IReadOnlyList<ActiveEmployeeInfo> all = await employees.ListActiveAsync(ct);
        IEnumerable<ActiveEmployeeInfo> filtered = all;

        if (activeOnly == false)
        {
            // ActiveEmployeeLookup only returns active; inactive filter is a no-op here.
            filtered = [];
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            filtered = filtered.Where(u =>
                u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.Upn.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        List<ActiveEmployeeInfo> candidates = filtered.Take(200).ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        HashSet<Guid> userIds = candidates.Select(c => c.Id).ToHashSet();
        List<DepartmentMembership> memberships = await db.DepartmentMemberships.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.EffectiveTo == null)
            .ToListAsync(ct);

        if (departmentId is Guid deptId)
        {
            HashSet<Guid> inDept = memberships
                .Where(m => m.DepartmentId == deptId)
                .Select(m => m.UserId)
                .ToHashSet();
            candidates = candidates.Where(c => inDept.Contains(c.Id)).ToList();
            userIds = candidates.Select(c => c.Id).ToHashSet();
            memberships = memberships.Where(m => userIds.Contains(m.UserId)).ToList();
        }

        Dictionary<Guid, Department> departments = await db.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, ct);

        List<PositionAssignment> assignments = await db.PositionAssignments.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId))
            .ToListAsync(ct);
        HashSet<Guid> positionIds = assignments.Select(a => a.PositionId).ToHashSet();
        Dictionary<Guid, Position> positions = await db.Positions.AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        IReadOnlyDictionary<Guid, UserDisplayInfo> displays =
            await userDisplayLookup.GetManyAsync(userIds, ct);

        return candidates.Select(user =>
        {
            displays.TryGetValue(user.Id, out UserDisplayInfo? info);
            List<DepartmentMembership> userMemberships = memberships.Where(m => m.UserId == user.Id).ToList();
            DepartmentMembership? primary = userMemberships.FirstOrDefault(m => m.IsPrimary) ?? userMemberships.FirstOrDefault();
            string? primaryName = primary is not null && departments.TryGetValue(primary.DepartmentId, out Department? dept)
                ? dept.Name
                : null;
            int additional = Math.Max(0, userMemberships.Count - (primary is null ? 0 : 1));
            List<string> positionNames = assignments
                .Where(a => a.UserId == user.Id && positions.ContainsKey(a.PositionId))
                .Select(a => positions[a.PositionId].NameEn)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            return new PeopleRowDto(
                user.Id,
                info?.DisplayName ?? user.DisplayName,
                info?.Upn ?? user.Upn,
                info?.AvatarUrl,
                info?.IsActive ?? true,
                primary?.DepartmentId,
                primaryName,
                additional,
                positionNames);
        }).ToList();
    }

    public async Task<UserOrgProfileDto?> GetProfileSummaryAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, UserDisplayInfo> displays = await userDisplayLookup.GetManyAsync([userId], ct);
        if (!displays.TryGetValue(userId, out UserDisplayInfo? info))
        {
            return null;
        }

        List<DepartmentMembership> memberships = await db.DepartmentMemberships.AsNoTracking()
            .Where(x => x.UserId == userId && x.EffectiveTo == null)
            .ToListAsync(ct);
        Dictionary<Guid, Department> departments = await db.Departments.AsNoTracking()
            .Where(d => memberships.Select(m => m.DepartmentId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        List<DepartmentMemberShipSummaryDto> deptSummaries = memberships
            .Where(m => departments.ContainsKey(m.DepartmentId))
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => departments[m.DepartmentId].SortOrder)
            .Select(m =>
            {
                Department d = departments[m.DepartmentId];
                return new DepartmentMemberShipSummaryDto(d.Id, d.Name, d.NameAr, d.Code, m.IsPrimary);
            })
            .ToList();

        DepartmentMemberShipSummaryDto? primary = deptSummaries.FirstOrDefault(d => d.IsPrimary) ?? deptSummaries.FirstOrDefault();

        List<PositionAssignment> assignments = await db.PositionAssignments.AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);
        HashSet<Guid> positionIds = assignments.Select(a => a.PositionId).ToHashSet();
        Dictionary<Guid, Position> positions = await db.Positions.AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        List<UserPositionDto> userPositions = assignments
            .Where(a => positions.ContainsKey(a.PositionId))
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => positions[a.PositionId].SortOrder)
            .Select(a =>
            {
                Position p = positions[a.PositionId];
                return new UserPositionDto(
                    a.Id, p.Id, p.Key, p.NameEn, p.NameAr, p.DepartmentId, a.IsPrimary, p.IsActive, p.IsManagerial);
            })
            .ToList();

        return new UserOrgProfileDto(
            userId,
            info.DisplayName,
            info.Upn,
            info.AvatarUrl,
            info.IsActive,
            primary?.DepartmentId,
            primary?.NameEn,
            deptSummaries,
            userPositions);
    }

    private async Task EnsureNoDepartmentParentCycleAsync(
        Guid departmentId,
        Guid parentDepartmentId,
        CancellationToken ct)
    {
        Dictionary<Guid, Guid?> parents = await db.Departments.AsNoTracking()
            .Select(d => new { d.Id, d.ParentDepartmentId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentDepartmentId, ct);

        Guid? current = parentDepartmentId;
        HashSet<Guid> seen = [];
        while (current is Guid walkId)
        {
            if (walkId == departmentId)
            {
                throw new InvalidOperationException(
                    "Department parent would create a cycle in the organization hierarchy.");
            }

            if (!seen.Add(walkId))
            {
                throw new InvalidOperationException(
                    "Department parent would create a cycle in the organization hierarchy.");
            }

            if (!parents.TryGetValue(walkId, out Guid? next) || next is null)
            {
                break;
            }

            current = next;
        }
    }

    private async Task ClearPrimaryForUserAsync(Guid userId, Guid? exceptMembershipId, CancellationToken ct)
    {
        List<DepartmentMembership> primaries = await db.DepartmentMemberships
            .Where(x => x.UserId == userId && x.IsPrimary && x.EffectiveTo == null)
            .ToListAsync(ct);
        foreach (DepartmentMembership row in primaries)
        {
            if (exceptMembershipId is Guid exceptId && row.Id == exceptId)
            {
                continue;
            }

            row.SetPrimary(false, clock.UtcNow);
        }
    }

    private async Task<IReadOnlyList<DepartmentDetailDto>> MapDetailsAsync(
        IReadOnlyList<Department> departments,
        CancellationToken ct)
    {
        if (departments.Count == 0)
        {
            return [];
        }

        HashSet<Guid> ids = departments.Select(d => d.Id).ToHashSet();
        var memberCounts = await db.DepartmentMemberships.AsNoTracking()
            .Where(m => ids.Contains(m.DepartmentId) && m.EffectiveTo == null)
            .GroupBy(m => m.DepartmentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var positionCounts = await db.Positions.AsNoTracking()
            .Where(p => ids.Contains(p.DepartmentId))
            .GroupBy(p => p.DepartmentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        Dictionary<Guid, int> members = memberCounts.ToDictionary(x => x.Key, x => x.Count);
        Dictionary<Guid, int> positions = positionCounts.ToDictionary(x => x.Key, x => x.Count);

        return departments.Select(d => new DepartmentDetailDto(
            d.Id,
            d.Name,
            d.NameAr,
            d.Code,
            d.Description,
            d.DescriptionAr,
            d.ParentDepartmentId,
            d.IsActive,
            d.SortOrder,
            members.GetValueOrDefault(d.Id),
            positions.GetValueOrDefault(d.Id),
            d.CreatedAtUtc,
            d.UpdatedAtUtc,
            Convert.ToBase64String(d.RowVersion))).ToList();
    }
}

internal static class DepartmentAudit
{
    public static BusinessAuditEntry Created(Department department) =>
        Field(department, "DepartmentCreated", null, department.Name, BusinessAuditAction.Created);

    public static BusinessAuditEntry Updated(Department department) =>
        Field(department, "DepartmentUpdated", null, department.Name, BusinessAuditAction.Updated);

    public static BusinessAuditEntry Deactivated(Department department) =>
        Field(department, "DepartmentDeactivated", "Active", "Inactive", BusinessAuditAction.StatusChanged);

    public static BusinessAuditEntry MemberAdded(Department department, Guid userId) =>
        Field(department, "DepartmentMemberAdded", null, userId.ToString(), BusinessAuditAction.Assigned);

    public static BusinessAuditEntry MemberRemoved(Department department, Guid userId) =>
        Field(department, "DepartmentMemberRemoved", userId.ToString(), null, BusinessAuditAction.Unassigned);

    public static BusinessAuditEntry PrimaryChanged(Department department, Guid userId) =>
        Field(department, "PrimaryDepartmentChanged", null, userId.ToString(), BusinessAuditAction.Updated);

    private static BusinessAuditEntry Field(
        Department department,
        string fieldName,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action) =>
        new()
        {
            AggregateType = AuditAggregateType.OrganizationDepartment,
            AggregateId = department.Id,
            BusinessNumber = department.Code,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };
}
