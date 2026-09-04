using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.Compliance.Services;

public sealed record ControlMappingDto(
    Guid Id, Guid InternalControlId, Guid FrameworkRequirementId, string Relationship, string? Notes,
    Guid CreatedByUserId, DateTimeOffset CreatedAtUtc,
    string? RequirementCode, string? RequirementTitle, Guid? FrameworkVersionId, string? FrameworkCode);

public sealed class ControlMappingService(
    ComplianceDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public async Task<IReadOnlyList<ControlMappingDto>> ListAsync(
        Guid? internalControlId, Guid? frameworkRequirementId, Guid? frameworkVersionId, CancellationToken ct)
    {
        IQueryable<ControlMapping> q = db.ControlMappings.AsNoTracking();
        if (internalControlId is Guid cid) q = q.Where(x => x.InternalControlId == cid);
        if (frameworkRequirementId is Guid rid) q = q.Where(x => x.FrameworkRequirementId == rid);

        List<ControlMapping> items = await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return await EnrichAsync(items, frameworkVersionId, ct);
    }

    public async Task<ControlMappingDto> CreateAsync(
        Guid internalControlId, Guid frameworkRequirementId, MappingRelationship relationship,
        Guid createdByUserId, string? notes, CancellationToken ct)
    {
        FrameworkRequirement? req = await db.FrameworkRequirements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == frameworkRequirementId, ct)
            ?? throw new InvalidOperationException("Framework requirement was not found.");
        if (!req.IsActive) throw new InvalidOperationException("Cannot map to an inactive requirement.");

        bool exists = await db.ControlMappings.AnyAsync(
            x => x.InternalControlId == internalControlId && x.FrameworkRequirementId == frameworkRequirementId, ct);
        if (exists) throw new InvalidOperationException("This control is already mapped to the requirement.");

        ControlMapping entity = ControlMapping.Create(
            internalControlId, frameworkRequirementId, relationship, createdByUserId, clock.UtcNow, notes);
        db.ControlMappings.Add(entity);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Assessment,
            AggregateId = entity.Id,
            Action = BusinessAuditAction.Linked,
            FieldName = "ControlMapping",
            NewValue = $"{internalControlId}:{frameworkRequirementId}",
            Source = AuditSource.Api,
        }, ct);
        return (await EnrichAsync([entity], null, ct))[0];
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        ControlMapping? entity = await db.ControlMappings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return;
        string old = $"{entity.InternalControlId}:{entity.FrameworkRequirementId}";
        db.ControlMappings.Remove(entity);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Assessment,
            AggregateId = id,
            Action = BusinessAuditAction.Unlinked,
            FieldName = "ControlMapping",
            OldValue = old,
            Source = AuditSource.Api,
        }, ct);
    }

    private async Task<IReadOnlyList<ControlMappingDto>> EnrichAsync(
        IReadOnlyList<ControlMapping> items, Guid? frameworkVersionId, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        Guid[] reqIds = items.Select(x => x.FrameworkRequirementId).Distinct().ToArray();
        List<FrameworkRequirement> reqs = await db.FrameworkRequirements.AsNoTracking()
            .Where(x => reqIds.Contains(x.Id)).ToListAsync(ct);
        if (frameworkVersionId is Guid vid)
            reqs = reqs.Where(x => x.FrameworkVersionId == vid).ToList();
        Dictionary<Guid, FrameworkRequirement> byId = reqs.ToDictionary(x => x.Id);
        Guid[] versionIds = reqs.Select(x => x.FrameworkVersionId).Distinct().ToArray();
        Dictionary<Guid, FrameworkVersion> versions = await db.FrameworkVersions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        Guid[] fwIds = versions.Values.Select(x => x.FrameworkId).Distinct().ToArray();
        Dictionary<Guid, Framework> frameworks = await db.Frameworks.AsNoTracking()
            .Where(x => fwIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        List<ControlMappingDto> result = [];
        foreach (ControlMapping m in items)
        {
            if (!byId.TryGetValue(m.FrameworkRequirementId, out FrameworkRequirement? req)) continue;
            versions.TryGetValue(req.FrameworkVersionId, out FrameworkVersion? ver);
            string? fwCode = ver is not null && frameworks.TryGetValue(ver.FrameworkId, out Framework? fw) ? fw.Code : null;
            result.Add(new(
                m.Id, m.InternalControlId, m.FrameworkRequirementId, m.Relationship.ToString(), m.Notes,
                m.CreatedByUserId, m.CreatedAtUtc, req.Code, req.Title, req.FrameworkVersionId, fwCode));
        }

        return result;
    }
}
