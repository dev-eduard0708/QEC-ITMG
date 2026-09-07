using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Organization.Domain;
using Qec.Itmg.Organization.Persistence;

namespace Qec.Itmg.Organization.Services;

public sealed record PositionDto(
    Guid Id,
    Guid DepartmentId,
    Guid? ParentPositionId,
    string Key,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    bool IsManagerial,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion,
    int OccupantCount);

public sealed record PositionOccupantDto(
    Guid AssignmentId,
    Guid UserId,
    string DisplayName,
    string Upn,
    bool IsPrimary,
    bool IsActiveUser,
    string? AvatarUrl);

public sealed record PositionNodeDto(
    Guid Id,
    Guid DepartmentId,
    Guid? ParentPositionId,
    string Key,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    bool IsManagerial,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<PositionOccupantDto> Occupants,
    IReadOnlyList<PositionNodeDto> Children);

public sealed record PositionAssignmentDto(
    Guid Id,
    Guid PositionId,
    Guid UserId,
    string DisplayName,
    string Upn,
    bool IsPrimary,
    bool IsActiveUser,
    string? AvatarUrl,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record UserPositionDto(
    Guid AssignmentId,
    Guid PositionId,
    string PositionKey,
    string PositionNameEn,
    string PositionNameAr,
    Guid DepartmentId,
    bool IsPrimary,
    bool IsActive,
    bool IsManagerial);

public sealed record DepartmentSummaryDto(
    Guid Id,
    string Name,
    string? NameAr,
    string Code,
    bool IsActive,
    int SortOrder,
    int MemberCount,
    int PositionCount);

public sealed class PositionService(
    OrganizationDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction,
    IUserDisplayLookup userDisplayLookup)
{
    public async Task<IReadOnlyList<DepartmentSummaryDto>> ListDepartmentsAsync(CancellationToken ct)
    {
        List<Department> items = await db.Departments.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
        HashSet<Guid> ids = items.Select(x => x.Id).ToHashSet();
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
        return items.Select(x => new DepartmentSummaryDto(
            x.Id,
            x.Name,
            x.NameAr,
            x.Code,
            x.IsActive,
            x.SortOrder,
            members.GetValueOrDefault(x.Id),
            positions.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<IReadOnlyList<PositionDto>> ListPositionsAsync(
        Guid? departmentId,
        bool activeOnly,
        CancellationToken ct)
    {
        IQueryable<Position> query = db.Positions.AsNoTracking();
        if (departmentId is Guid deptId)
        {
            query = query.Where(x => x.DepartmentId == deptId);
        }

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        List<Position> positions = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameEn)
            .ToListAsync(ct);

        Dictionary<Guid, int> counts = await db.PositionAssignments.AsNoTracking()
            .GroupBy(x => x.PositionId)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PositionId, x => x.Count, ct);

        return positions.Select(p => MapPosition(p, counts.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<PositionHierarchyResult> GetHierarchyAsync(Guid? departmentId, CancellationToken ct)
    {
        Department? department = await ResolveDepartmentAsync(departmentId, ct);
        if (department is null)
        {
            return new PositionHierarchyResult(null, null, []);
        }

        List<Position> positions = await db.Positions.AsNoTracking()
            .Where(x => x.DepartmentId == department.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameEn)
            .ToListAsync(ct);

        List<Guid> positionIds = positions.Select(p => p.Id).ToList();
        List<PositionAssignment> assignments = positionIds.Count == 0
            ? []
            : await db.PositionAssignments.AsNoTracking()
                .Where(a => positionIds.Contains(a.PositionId))
                .ToListAsync(ct);

        IReadOnlyDictionary<Guid, UserDisplayInfo> users = await userDisplayLookup.GetManyAsync(
            assignments.Select(a => a.UserId).Distinct(),
            ct);

        Dictionary<Guid, List<PositionOccupantDto>> occupantsByPosition = assignments
            .GroupBy(a => a.PositionId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(a => a.IsPrimary)
                    .ThenBy(a => users.TryGetValue(a.UserId, out UserDisplayInfo? u) ? u.DisplayName : a.UserId.ToString())
                    .Select(a => MapOccupant(a, users))
                    .ToList());

        List<PositionNodeDto> roots = BuildTree(positions, occupantsByPosition, parentId: null);
        return new PositionHierarchyResult(department.Id, department.Name, roots);
    }

    public async Task<PositionDto> CreateAsync(
        Guid departmentId,
        string key,
        string nameEn,
        string nameAr,
        Guid? parentPositionId,
        string? descriptionEn,
        string? descriptionAr,
        bool isManagerial,
        int sortOrder,
        bool isActive,
        CancellationToken ct)
    {
        Department department = await db.Departments.FirstOrDefaultAsync(x => x.Id == departmentId, ct)
            ?? throw new InvalidOperationException("Department was not found.");

        string normalizedKey = Position.NormalizeKey(key);
        bool keyTaken = await db.Positions.AnyAsync(
            x => x.DepartmentId == departmentId && x.Key == normalizedKey,
            ct);
        if (keyTaken)
        {
            throw new InvalidOperationException($"Position key '{normalizedKey}' already exists in this department.");
        }

        if (parentPositionId is Guid parentId)
        {
            Position parent = await db.Positions.FirstOrDefaultAsync(x => x.Id == parentId, ct)
                ?? throw new InvalidOperationException("Parent position was not found.");
            if (parent.DepartmentId != departmentId)
            {
                throw new InvalidOperationException("Parent position must belong to the same department.");
            }
        }

        Position entity = Position.Create(
            department.Id,
            normalizedKey,
            nameEn,
            nameAr,
            clock.UtcNow,
            parentPositionId,
            descriptionEn,
            descriptionAr,
            isManagerial,
            sortOrder,
            isActive);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            db.Positions.Add(entity);
            await businessAudit.AppendAsync(
                PositionAudit.Created(entity),
                innerCt);
        }, ct);

        return MapPosition(entity, 0);
    }

    public async Task<PositionDto> UpdateAsync(
        Guid id,
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        bool isManagerial,
        int sortOrder,
        bool isActive,
        CancellationToken ct)
    {
        Position entity = await db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Position was not found.");

        entity.Update(nameEn, nameAr, descriptionEn, descriptionAr, isManagerial, sortOrder, isActive, clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await businessAudit.AppendAsync(PositionAudit.Updated(entity), innerCt);
        }, ct);

        int count = await db.PositionAssignments.AsNoTracking().CountAsync(x => x.PositionId == id, ct);
        return MapPosition(entity, count);
    }

    public async Task<PositionDto> ChangeParentAsync(Guid id, Guid? parentPositionId, CancellationToken ct)
    {
        Position entity = await db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Position was not found.");

        Guid? normalizedParent = parentPositionId is null || parentPositionId == Guid.Empty
            ? null
            : parentPositionId;

        if (normalizedParent == id)
        {
            throw new InvalidOperationException("A position cannot be its own parent.");
        }

        if (normalizedParent is Guid parentId)
        {
            Position parent = await db.Positions.FirstOrDefaultAsync(x => x.Id == parentId, ct)
                ?? throw new InvalidOperationException("Parent position was not found.");
            if (parent.DepartmentId != entity.DepartmentId)
            {
                throw new InvalidOperationException("Parent position must belong to the same department.");
            }

            if (await IsDescendantAsync(ancestorId: id, candidateId: parentId, ct))
            {
                throw new InvalidOperationException("Cannot set a descendant as parent (cycle).");
            }
        }

        string? oldParent = entity.ParentPositionId?.ToString();
        entity.SetParent(normalizedParent, clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await businessAudit.AppendAsync(
                PositionAudit.ParentChanged(entity, oldParent, normalizedParent?.ToString()),
                innerCt);
        }, ct);

        int count = await db.PositionAssignments.AsNoTracking().CountAsync(x => x.PositionId == id, ct);
        return MapPosition(entity, count);
    }

    public async Task<PositionDto> DeactivateAsync(Guid id, CancellationToken ct)
    {
        Position entity = await db.Positions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Position was not found.");

        entity.Deactivate(clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await businessAudit.AppendAsync(PositionAudit.Deactivated(entity), innerCt);
        }, ct);

        int count = await db.PositionAssignments.AsNoTracking().CountAsync(x => x.PositionId == id, ct);
        return MapPosition(entity, count);
    }

    public async Task<IReadOnlyList<PositionAssignmentDto>> ListAssignmentsAsync(Guid positionId, CancellationToken ct)
    {
        bool exists = await db.Positions.AsNoTracking().AnyAsync(x => x.Id == positionId, ct);
        if (!exists)
        {
            throw new InvalidOperationException("Position was not found.");
        }

        List<PositionAssignment> assignments = await db.PositionAssignments.AsNoTracking()
            .Where(x => x.PositionId == positionId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        IReadOnlyDictionary<Guid, UserDisplayInfo> users = await userDisplayLookup.GetManyAsync(
            assignments.Select(a => a.UserId),
            ct);

        return assignments.Select(a => MapAssignment(a, users)).ToList();
    }

    public async Task<PositionAssignmentDto> AssignUserAsync(
        Guid positionId,
        Guid userId,
        bool isPrimary,
        CancellationToken ct,
        bool addToDepartmentIfMissing = false)
    {
        Position position = await db.Positions.FirstOrDefaultAsync(x => x.Id == positionId, ct)
            ?? throw new InvalidOperationException("Position was not found.");

        if (!position.IsActive)
        {
            throw new InvalidOperationException("Inactive positions do not accept new assignments.");
        }

        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        bool isMember = await db.DepartmentMemberships.AnyAsync(
            x => x.DepartmentId == position.DepartmentId && x.UserId == userId && x.EffectiveTo == null,
            ct);
        if (!isMember && !addToDepartmentIfMissing)
        {
            Department? dept = await db.Departments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == position.DepartmentId, ct);
            string deptName = dept?.Name ?? "this department";
            throw new InvalidOperationException(
                $"User is not currently a member of {deptName}. Confirm adding them to the department before assigning.");
        }

        bool duplicate = await db.PositionAssignments.AnyAsync(
            x => x.PositionId == positionId && x.UserId == userId,
            ct);
        if (duplicate)
        {
            throw new InvalidOperationException("User is already assigned to this position.");
        }

        PositionAssignment assignment = PositionAssignment.Create(positionId, userId, clock.UtcNow, isPrimary);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            if (!isMember && addToDepartmentIfMissing)
            {
                db.DepartmentMemberships.Add(
                    DepartmentMembership.Create(position.DepartmentId, userId, clock.UtcNow, isPrimary: false));
                Department? deptForAudit = await db.Departments.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == position.DepartmentId, innerCt);
                if (deptForAudit is not null)
                {
                    await businessAudit.AppendAsync(
                        DepartmentAudit.MemberAdded(deptForAudit, userId),
                        innerCt);
                }
            }

            if (isPrimary)
            {
                await ClearPrimaryForUserAsync(userId, exceptAssignmentId: null, innerCt);
                assignment.SetPrimary(true, clock.UtcNow);
            }

            db.PositionAssignments.Add(assignment);
            await businessAudit.AppendAsync(
                PositionAudit.UserAssigned(position, userId),
                innerCt);
        }, ct);

        IReadOnlyDictionary<Guid, UserDisplayInfo> users = await userDisplayLookup.GetManyAsync([userId], ct);
        return MapAssignment(assignment, users);
    }

    public async Task RemoveAssignmentAsync(Guid positionId, Guid assignmentId, CancellationToken ct)
    {
        PositionAssignment? assignment = await db.PositionAssignments
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.PositionId == positionId, ct);
        if (assignment is null)
        {
            throw new InvalidOperationException("Assignment was not found.");
        }

        Position? position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == positionId, ct);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            db.PositionAssignments.Remove(assignment);
            if (position is not null)
            {
                await businessAudit.AppendAsync(
                    PositionAudit.UserRemoved(position, assignment.UserId),
                    innerCt);
            }
        }, ct);
    }

    public async Task<PositionAssignmentDto> SetPrimaryAsync(Guid positionId, Guid assignmentId, CancellationToken ct)
    {
        PositionAssignment assignment = await db.PositionAssignments
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.PositionId == positionId, ct)
            ?? throw new InvalidOperationException("Assignment was not found.");

        Position? position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == positionId, ct);

        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            await ClearPrimaryForUserAsync(assignment.UserId, exceptAssignmentId: assignment.Id, innerCt);
            assignment.SetPrimary(true, clock.UtcNow);
            if (position is not null)
            {
                await businessAudit.AppendAsync(
                    PositionAudit.PrimaryChanged(position, assignment.UserId),
                    innerCt);
            }
        }, ct);

        IReadOnlyDictionary<Guid, UserDisplayInfo> users = await userDisplayLookup.GetManyAsync([assignment.UserId], ct);
        return MapAssignment(assignment, users);
    }

    public async Task<IReadOnlyList<UserPositionDto>> ListUserPositionsAsync(Guid userId, CancellationToken ct)
    {
        List<PositionAssignment> assignments = await db.PositionAssignments.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        if (assignments.Count == 0)
        {
            return [];
        }

        HashSet<Guid> positionIds = assignments.Select(a => a.PositionId).ToHashSet();
        Dictionary<Guid, Position> positions = await db.Positions.AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        return assignments
            .Where(a => positions.ContainsKey(a.PositionId))
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => positions[a.PositionId].SortOrder)
            .Select(a =>
            {
                Position p = positions[a.PositionId];
                return new UserPositionDto(
                    a.Id,
                    p.Id,
                    p.Key,
                    p.NameEn,
                    p.NameAr,
                    p.DepartmentId,
                    a.IsPrimary,
                    p.IsActive,
                    p.IsManagerial);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ActiveEmployeeInfo>> SearchActiveUsersAsync(
        string? search,
        IActiveEmployeeLookup employees,
        CancellationToken ct,
        Guid? departmentId = null,
        bool searchAll = false)
    {
        IReadOnlyList<ActiveEmployeeInfo> all = await employees.ListActiveAsync(ct);
        IEnumerable<ActiveEmployeeInfo> filtered = all;

        if (departmentId is Guid deptId && !searchAll)
        {
            HashSet<Guid> memberIds = (await db.DepartmentMemberships.AsNoTracking()
                .Where(x => x.DepartmentId == deptId && x.EffectiveTo == null)
                .Select(x => x.UserId)
                .ToListAsync(ct)).ToHashSet();
            filtered = filtered.Where(u => memberIds.Contains(u.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            filtered = filtered.Where(u =>
                u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.Upn.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.Take(50).ToList();
    }

    private async Task ClearPrimaryForUserAsync(Guid userId, Guid? exceptAssignmentId, CancellationToken ct)
    {
        List<PositionAssignment> primaries = await db.PositionAssignments
            .Where(x => x.UserId == userId && x.IsPrimary)
            .ToListAsync(ct);

        foreach (PositionAssignment item in primaries)
        {
            if (exceptAssignmentId is Guid exceptId && item.Id == exceptId)
            {
                continue;
            }

            item.SetPrimary(false, clock.UtcNow);
        }
    }

    private async Task<bool> IsDescendantAsync(Guid ancestorId, Guid candidateId, CancellationToken ct)
    {
        Dictionary<Guid, Guid?> parents = await db.Positions.AsNoTracking()
            .Where(x => true)
            .Select(x => new { x.Id, x.ParentPositionId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentPositionId, ct);

        Guid? current = candidateId;
        HashSet<Guid> visited = [];
        while (current is Guid id)
        {
            if (!visited.Add(id))
            {
                break;
            }

            if (id == ancestorId)
            {
                return true;
            }

            if (!parents.TryGetValue(id, out Guid? parent))
            {
                break;
            }

            current = parent;
        }

        return false;
    }

    private async Task<Department?> ResolveDepartmentAsync(Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is Guid id)
        {
            return await db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        return await db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == "IT", ct)
            ?? await db.Departments.AsNoTracking().OrderBy(x => x.Name).FirstOrDefaultAsync(ct);
    }

    private static List<PositionNodeDto> BuildTree(
        List<Position> positions,
        Dictionary<Guid, List<PositionOccupantDto>> occupantsByPosition,
        Guid? parentId)
    {
        return positions
            .Where(p => p.ParentPositionId == parentId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.NameEn)
            .Select(p => new PositionNodeDto(
                p.Id,
                p.DepartmentId,
                p.ParentPositionId,
                p.Key,
                p.NameEn,
                p.NameAr,
                p.DescriptionEn,
                p.DescriptionAr,
                p.IsManagerial,
                p.IsActive,
                p.SortOrder,
                occupantsByPosition.GetValueOrDefault(p.Id) ?? [],
                BuildTree(positions, occupantsByPosition, p.Id)))
            .ToList();
    }

    private static PositionDto MapPosition(Position p, int occupantCount) =>
        new(
            p.Id,
            p.DepartmentId,
            p.ParentPositionId,
            p.Key,
            p.NameEn,
            p.NameAr,
            p.DescriptionEn,
            p.DescriptionAr,
            p.IsManagerial,
            p.IsActive,
            p.SortOrder,
            p.CreatedAtUtc,
            p.UpdatedAtUtc,
            Convert.ToBase64String(p.RowVersion),
            occupantCount);

    private static PositionOccupantDto MapOccupant(
        PositionAssignment assignment,
        IReadOnlyDictionary<Guid, UserDisplayInfo> users)
    {
        if (users.TryGetValue(assignment.UserId, out UserDisplayInfo? user))
        {
            return new PositionOccupantDto(
                assignment.Id,
                user.Id,
                user.DisplayName,
                user.Upn,
                assignment.IsPrimary,
                user.IsActive,
                user.AvatarUrl);
        }

        string shortId = assignment.UserId.ToString("N")[..8];
        return new PositionOccupantDto(
            assignment.Id,
            assignment.UserId,
            $"User {shortId}",
            shortId,
            assignment.IsPrimary,
            false,
            null);
    }

    private static PositionAssignmentDto MapAssignment(
        PositionAssignment assignment,
        IReadOnlyDictionary<Guid, UserDisplayInfo> users)
    {
        PositionOccupantDto occupant = MapOccupant(assignment, users);
        return new PositionAssignmentDto(
            assignment.Id,
            assignment.PositionId,
            assignment.UserId,
            occupant.DisplayName,
            occupant.Upn,
            assignment.IsPrimary,
            occupant.IsActiveUser,
            occupant.AvatarUrl,
            assignment.EffectiveFrom,
            assignment.EffectiveTo,
            assignment.CreatedAtUtc,
            assignment.UpdatedAtUtc);
    }
}

public sealed record PositionHierarchyResult(
    Guid? DepartmentId,
    string? DepartmentName,
    IReadOnlyList<PositionNodeDto> Roots);

internal static class PositionAudit
{
    public static BusinessAuditEntry Created(Position position) =>
        Field(position, "PositionCreated", null, position.NameEn, BusinessAuditAction.Created);

    public static BusinessAuditEntry Updated(Position position) =>
        Field(position, "PositionUpdated", null, position.NameEn, BusinessAuditAction.Updated);

    public static BusinessAuditEntry ParentChanged(Position position, string? oldParent, string? newParent) =>
        Field(position, "PositionParentChanged", oldParent, newParent, BusinessAuditAction.Updated);

    public static BusinessAuditEntry Deactivated(Position position) =>
        Field(position, "PositionDeactivated", "Active", "Inactive", BusinessAuditAction.StatusChanged);

    public static BusinessAuditEntry UserAssigned(Position position, Guid userId) =>
        Field(position, "PositionUserAssigned", null, userId.ToString(), BusinessAuditAction.Assigned);

    public static BusinessAuditEntry UserRemoved(Position position, Guid userId) =>
        Field(position, "PositionUserRemoved", userId.ToString(), null, BusinessAuditAction.Unassigned);

    public static BusinessAuditEntry PrimaryChanged(Position position, Guid userId) =>
        Field(position, "PrimaryPositionChanged", null, userId.ToString(), BusinessAuditAction.Updated);

    private static BusinessAuditEntry Field(
        Position position,
        string fieldName,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action) =>
        new()
        {
            AggregateType = AuditAggregateType.OrganizationPosition,
            AggregateId = position.Id,
            BusinessNumber = position.Key,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };
}
