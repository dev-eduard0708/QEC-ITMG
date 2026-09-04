using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Governance.Domain;
using Qec.Itmg.Governance.Persistence;

namespace Qec.Itmg.Governance.Services;

public sealed record OrganizationProfileDto(
    Guid Id, string LegalName, string Timezone, string? ClassificationScheme,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record OrganizationalUnitDto(
    Guid Id, string Name, string? Code, Guid? ParentId, Guid? ManagerUserId, string? Description,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion,
    IReadOnlyList<Guid> MemberUserIds);

public sealed class OrganizationChartService(GovernanceDbContext db, IClock clock)
{
    public async Task<OrganizationProfileDto?> GetProfileAsync(CancellationToken ct)
    {
        OrganizationProfile? item = await db.OrganizationProfiles.AsNoTracking().OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return item is null ? null : MapProfile(item);
    }

    public async Task<OrganizationProfileDto> UpsertProfileAsync(
        string legalName, string timezone, string? classificationScheme, CancellationToken ct)
    {
        OrganizationProfile? existing = await db.OrganizationProfiles.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        DateTimeOffset now = clock.UtcNow;
        if (existing is null)
        {
            existing = OrganizationProfile.Create(legalName, timezone, now, classificationScheme);
            db.OrganizationProfiles.Add(existing);
        }
        else
        {
            existing.Update(legalName, timezone, classificationScheme, now);
        }

        await db.SaveChangesAsync(ct);
        return MapProfile(existing);
    }

    public async Task<IReadOnlyList<OrganizationalUnitDto>> ListUnitsAsync(CancellationToken ct)
    {
        List<OrganizationalUnit> units = await db.OrganizationalUnits.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        Dictionary<Guid, List<Guid>> members = await db.OrganizationalUnitMemberships.AsNoTracking()
            .GroupBy(x => x.OrganizationalUnitId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.UserId).ToList(), ct);
        return units.Select(u => MapUnit(u, members.GetValueOrDefault(u.Id) ?? [])).ToList();
    }

    public async Task<OrganizationalUnitDto?> GetUnitAsync(Guid id, CancellationToken ct)
    {
        OrganizationalUnit? unit = await db.OrganizationalUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (unit is null) return null;
        List<Guid> members = await db.OrganizationalUnitMemberships.AsNoTracking()
            .Where(x => x.OrganizationalUnitId == id)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        return MapUnit(unit, members);
    }

    public async Task<OrganizationalUnitDto> CreateUnitAsync(
        string name, string? code, Guid? parentId, Guid? managerUserId, string? description, CancellationToken ct)
    {
        if (parentId is Guid pid)
        {
            bool exists = await db.OrganizationalUnits.AsNoTracking().AnyAsync(x => x.Id == pid, ct);
            if (!exists) throw new InvalidOperationException("Parent organizational unit was not found.");
        }

        OrganizationalUnit unit = OrganizationalUnit.Create(name, clock.UtcNow, code, parentId, managerUserId, description);
        db.OrganizationalUnits.Add(unit);
        await db.SaveChangesAsync(ct);
        return MapUnit(unit, []);
    }

    public async Task<OrganizationalUnitDto> UpdateUnitAsync(
        Guid id, string name, string? code, Guid? parentId, Guid? managerUserId, string? description, bool isActive,
        CancellationToken ct)
    {
        OrganizationalUnit unit = await db.OrganizationalUnits.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Organizational unit was not found.");
        if (parentId is Guid pid)
        {
            if (pid == id) throw new InvalidOperationException("Organizational unit cannot be its own parent.");
            bool exists = await db.OrganizationalUnits.AsNoTracking().AnyAsync(x => x.Id == pid, ct);
            if (!exists) throw new InvalidOperationException("Parent organizational unit was not found.");
        }

        unit.Update(name, code, parentId, managerUserId, description, isActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        List<Guid> members = await db.OrganizationalUnitMemberships.AsNoTracking()
            .Where(x => x.OrganizationalUnitId == id)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        return MapUnit(unit, members);
    }

    public async Task AssignMemberAsync(Guid unitId, Guid userId, CancellationToken ct)
    {
        bool unitExists = await db.OrganizationalUnits.AsNoTracking().AnyAsync(x => x.Id == unitId, ct);
        if (!unitExists) throw new InvalidOperationException("Organizational unit was not found.");
        bool already = await db.OrganizationalUnitMemberships.AsNoTracking()
            .AnyAsync(x => x.OrganizationalUnitId == unitId && x.UserId == userId, ct);
        if (already) return;
        db.OrganizationalUnitMemberships.Add(OrganizationalUnitMembership.Create(unitId, userId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid unitId, Guid userId, CancellationToken ct)
    {
        OrganizationalUnitMembership? row = await db.OrganizationalUnitMemberships
            .FirstOrDefaultAsync(x => x.OrganizationalUnitId == unitId && x.UserId == userId, ct);
        if (row is null) return;
        db.OrganizationalUnitMemberships.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    private static OrganizationProfileDto MapProfile(OrganizationProfile x) => new(
        x.Id, x.LegalName, x.Timezone, x.ClassificationScheme, x.CreatedAtUtc, x.UpdatedAtUtc,
        Convert.ToBase64String(x.RowVersion));

    private static OrganizationalUnitDto MapUnit(OrganizationalUnit x, IReadOnlyList<Guid> members) => new(
        x.Id, x.Name, x.Code, x.ParentId, x.ManagerUserId, x.Description, x.IsActive,
        x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion), members);
}
