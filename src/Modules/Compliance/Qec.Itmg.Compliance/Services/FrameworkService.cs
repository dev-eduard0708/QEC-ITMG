using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;

namespace Qec.Itmg.Compliance.Services;

public sealed record FrameworkDto(
    Guid Id, string Code, string Name, string Publisher, string? Description, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record FrameworkVersionDto(
    Guid Id, Guid FrameworkId, string VersionCode, string? Title,
    DateOnly? PublishedDate, DateOnly? EffectiveDate, bool IsCurrent, DateTimeOffset CreatedAtUtc);

public sealed record FrameworkRequirementDto(
    Guid Id, Guid FrameworkVersionId, Guid? ParentRequirementId, string Code, string Title,
    string? Text, string RequirementType, int? SortOrder, bool IsActive);

public sealed record FrameworkDetailDto(
    FrameworkDto Framework, IReadOnlyList<FrameworkVersionDto> Versions);

public sealed class FrameworkService(ComplianceDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<FrameworkDto>> ListAsync(CancellationToken ct)
    {
        List<Framework> items = await db.Frameworks.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<FrameworkDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        Framework? fw = await db.Frameworks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (fw is null) return null;
        List<FrameworkVersion> versions = await db.FrameworkVersions.AsNoTracking()
            .Where(x => x.FrameworkId == id).OrderByDescending(x => x.IsCurrent).ThenBy(x => x.VersionCode).ToListAsync(ct);
        return new(Map(fw), versions.Select(MapVersion).ToList());
    }

    public async Task<FrameworkDto> CreateAsync(string code, string name, string publisher, string? description, CancellationToken ct)
    {
        if (await db.Frameworks.AnyAsync(x => x.Code == code.Trim().ToUpperInvariant(), ct))
            throw new InvalidOperationException($"Framework code '{code}' already exists.");
        Framework entity = Framework.Create(code, name, publisher, clock.UtcNow, description);
        db.Frameworks.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<FrameworkDto> UpdateAsync(Guid id, string name, string publisher, string? description, bool isActive, CancellationToken ct)
    {
        Framework entity = await db.Frameworks.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Framework was not found.");
        entity.Update(name, publisher, description, isActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<FrameworkVersionDto> AddVersionAsync(
        Guid frameworkId, string versionCode, string? title, DateOnly? publishedDate, DateOnly? effectiveDate,
        bool isCurrent, CancellationToken ct)
    {
        bool exists = await db.Frameworks.AnyAsync(x => x.Id == frameworkId, ct);
        if (!exists) throw new InvalidOperationException("Framework was not found.");
        if (await db.FrameworkVersions.AnyAsync(x => x.FrameworkId == frameworkId && x.VersionCode == versionCode.Trim(), ct))
            throw new InvalidOperationException("Version code already exists for this framework.");

        if (isCurrent)
        {
            List<FrameworkVersion> currents = await db.FrameworkVersions
                .Where(x => x.FrameworkId == frameworkId && x.IsCurrent).ToListAsync(ct);
            foreach (FrameworkVersion c in currents) c.SetCurrent(false);
        }

        FrameworkVersion version = FrameworkVersion.Create(
            frameworkId, versionCode, clock.UtcNow, title, publishedDate, effectiveDate, isCurrent);
        db.FrameworkVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task SetCurrentVersionAsync(Guid frameworkId, Guid versionId, CancellationToken ct)
    {
        List<FrameworkVersion> versions = await db.FrameworkVersions
            .Where(x => x.FrameworkId == frameworkId).ToListAsync(ct);
        FrameworkVersion target = versions.FirstOrDefault(x => x.Id == versionId)
            ?? throw new InvalidOperationException("Framework version was not found.");
        foreach (FrameworkVersion v in versions) v.SetCurrent(v.Id == versionId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FrameworkRequirementDto>> ListRequirementsAsync(Guid versionId, CancellationToken ct)
    {
        List<FrameworkRequirement> items = await db.FrameworkRequirements.AsNoTracking()
            .Where(x => x.FrameworkVersionId == versionId)
            .OrderBy(x => x.SortOrder ?? int.MaxValue).ThenBy(x => x.Code)
            .ToListAsync(ct);
        return items.Select(MapReq).ToList();
    }

    public async Task<FrameworkRequirementDto?> GetRequirementAsync(Guid id, CancellationToken ct)
    {
        FrameworkRequirement? item = await db.FrameworkRequirements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapReq(item);
    }

    public async Task<FrameworkRequirementDto> AddRequirementAsync(
        Guid versionId, string code, string title, FrameworkRequirementType type,
        Guid? parentId, string? text, int? sortOrder, CancellationToken ct)
    {
        bool versionExists = await db.FrameworkVersions.AnyAsync(x => x.Id == versionId, ct);
        if (!versionExists) throw new InvalidOperationException("Framework version was not found.");
        if (await db.FrameworkRequirements.AnyAsync(x => x.FrameworkVersionId == versionId && x.Code == code.Trim(), ct))
            throw new InvalidOperationException("Requirement code already exists in this version.");
        if (parentId is Guid pid)
        {
            bool parentOk = await db.FrameworkRequirements.AnyAsync(
                x => x.Id == pid && x.FrameworkVersionId == versionId, ct);
            if (!parentOk) throw new InvalidOperationException("Parent requirement was not found in this version.");
        }

        FrameworkRequirement entity = FrameworkRequirement.Create(versionId, code, title, type, parentId, text, sortOrder);
        db.FrameworkRequirements.Add(entity);
        await db.SaveChangesAsync(ct);
        return MapReq(entity);
    }

    private static FrameworkDto Map(Framework x) => new(
        x.Id, x.Code, x.Name, x.Publisher, x.Description, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc,
        Convert.ToBase64String(x.RowVersion));

    private static FrameworkVersionDto MapVersion(FrameworkVersion x) => new(
        x.Id, x.FrameworkId, x.VersionCode, x.Title, x.PublishedDate, x.EffectiveDate, x.IsCurrent, x.CreatedAtUtc);

    private static FrameworkRequirementDto MapReq(FrameworkRequirement x) => new(
        x.Id, x.FrameworkVersionId, x.ParentRequirementId, x.Code, x.Title, x.Text,
        x.RequirementType.ToString(), x.SortOrder, x.IsActive);
}
