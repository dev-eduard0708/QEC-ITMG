using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Governance.Domain;
using Qec.Itmg.Governance.Persistence;

namespace Qec.Itmg.Governance.Services;

public sealed record ControlListItemDto(
    Guid Id, string ControlNumber, string Title, string Domain, string DomainLabel,
    Guid? PrimaryOwnerUserId, string Frequency, string AutomationType, string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record ControlListResult(IReadOnlyList<ControlListItemDto> Items, int TotalCount, int Page, int PageSize);

public sealed record ControlDetailDto(
    Guid Id, string ControlNumber, string Title, string Objective, string Description,
    string Domain, string DomainLabel, string Frequency, string AutomationType, string Status,
    Guid? PrimaryOwnerUserId, Guid? PrimaryOwnerRoleId,
    IReadOnlyList<Guid> SecondaryOwnerUserIds,
    IReadOnlyList<Guid> LinkedConfigurationItemIds,
    IReadOnlyList<Guid> LinkedBusinessServiceIds,
    IReadOnlyList<Guid> LinkedManagedDocumentIds,
    IReadOnlyList<ControlTestProcedureDto> TestProcedures,
    IReadOnlyList<EvidenceRequirementDto> EvidenceRequirements,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? RetiredAtUtc, string RowVersion);

public sealed record ControlTestProcedureDto(
    Guid Id, Guid InternalControlId, string Title, string? Purpose, string ProcedureSteps,
    string ExpectedResult, string? SampleGuidance, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record EvidenceRequirementDto(
    Guid Id, Guid InternalControlId, string Description, string? Frequency, string? RetentionNotes,
    bool IsRequired, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record ControlDomainOption(string Code, string Label);

internal static class ControlAudit
{
    public static BusinessAuditEntry Created(Guid id, string number) => new()
    {
        AggregateType = AuditAggregateType.Control,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated, string? reason = null) => new()
    {
        AggregateType = AuditAggregateType.Control,
        AggregateId = id,
        BusinessNumber = number,
        Action = action,
        FieldName = field,
        OldValue = oldValue,
        NewValue = newValue,
        Reason = reason,
        Source = AuditSource.Api,
    };
}

public sealed class InternalControlService(
    GovernanceDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public IReadOnlyList<ControlDomainOption> ListDomains() =>
        ControlDomainCodes.Labels.Select(kv => new ControlDomainOption(kv.Key, kv.Value)).OrderBy(x => x.Label).ToList();

    public async Task<ControlListResult> ListAsync(
        int page, int pageSize, string? search, string? domain, ControlStatus? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<InternalControl> q = db.InternalControls.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(domain))
        {
            string d = ControlDomainCodes.Normalize(domain);
            q = q.Where(x => x.Domain == d);
        }

        if (status is ControlStatus s) q = q.Where(x => x.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.ControlNumber.Contains(term) || x.Objective.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<InternalControl> items = await q.OrderBy(x => x.ControlNumber)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(MapList).ToList(), total, page, pageSize);
    }

    public async Task<ControlDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        InternalControl? item = await db.InternalControls.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : await MapDetailAsync(item, ct);
    }

    public async Task<ControlDetailDto> CreateAsync(
        string title, string objective, string description, string domain,
        ControlFrequency frequency, ControlAutomationType automationType,
        Guid? primaryOwnerUserId, Guid? primaryOwnerRoleId, CancellationToken ct)
    {
        string domainCode = ControlDomainCodes.Normalize(domain);
        ControlDetailDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string controlNumber = await IssueControlNumberAsync(domainCode, innerCt);
            InternalControl entity = InternalControl.Create(
                controlNumber, title, objective, description, domainCode, frequency, automationType,
                clock.UtcNow, primaryOwnerUserId, primaryOwnerRoleId);
            db.InternalControls.Add(entity);
            await businessAudit.AppendAsync(ControlAudit.Created(entity.Id, entity.ControlNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = await MapDetailAsync(entity, innerCt);
        }, ct);
        return created!;
    }

    public async Task<ControlDetailDto> UpdateAsync(
        Guid id, string title, string objective, string description,
        ControlFrequency frequency, ControlAutomationType automationType,
        Guid? primaryOwnerUserId, Guid? primaryOwnerRoleId, CancellationToken ct)
    {
        InternalControl entity = await db.InternalControls.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Internal control was not found.");
        string oldTitle = entity.Title;
        entity.Update(title, objective, description, frequency, automationType, primaryOwnerUserId, primaryOwnerRoleId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(entity.Id, entity.ControlNumber, "Title", oldTitle, entity.Title), ct);
        return (await MapDetailAsync(entity, ct))!;
    }

    public async Task<ControlDetailDto> ActivateAsync(Guid id, CancellationToken ct)
    {
        InternalControl entity = await db.InternalControls.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Internal control was not found.");
        string old = entity.Status.ToString();
        entity.Activate(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(entity.Id, entity.ControlNumber, "Status", old, entity.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
        return (await MapDetailAsync(entity, ct))!;
    }

    public async Task<ControlDetailDto> RetireAsync(Guid id, CancellationToken ct)
    {
        InternalControl entity = await db.InternalControls.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Internal control was not found.");
        string old = entity.Status.ToString();
        entity.Retire(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(entity.Id, entity.ControlNumber, "Status", old, entity.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
        return (await MapDetailAsync(entity, ct))!;
    }

    public async Task AddSecondaryOwnerAsync(Guid controlId, Guid userId, CancellationToken ct)
    {
        await EnsureControlAsync(controlId, ct);
        bool exists = await db.ControlSecondaryOwners.AsNoTracking()
            .AnyAsync(x => x.InternalControlId == controlId && x.UserId == userId, ct);
        if (exists) return;
        db.ControlSecondaryOwners.Add(ControlSecondaryOwner.Create(controlId, userId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "SecondaryOwner", null, userId.ToString(), BusinessAuditAction.Assigned), ct);
    }

    public async Task RemoveSecondaryOwnerAsync(Guid controlId, Guid userId, CancellationToken ct)
    {
        ControlSecondaryOwner? row = await db.ControlSecondaryOwners
            .FirstOrDefaultAsync(x => x.InternalControlId == controlId && x.UserId == userId, ct);
        if (row is null) return;
        db.ControlSecondaryOwners.Remove(row);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "SecondaryOwner", userId.ToString(), null, BusinessAuditAction.Unassigned), ct);
    }

    public async Task LinkConfigurationItemAsync(Guid controlId, Guid configurationItemId, CancellationToken ct)
    {
        await EnsureControlAsync(controlId, ct);
        bool exists = await db.ControlConfigurationItemLinks.AsNoTracking()
            .AnyAsync(x => x.InternalControlId == controlId && x.ConfigurationItemId == configurationItemId, ct);
        if (exists) return;
        db.ControlConfigurationItemLinks.Add(ControlConfigurationItemLink.Create(controlId, configurationItemId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "ConfigurationItem", null, configurationItemId.ToString(), BusinessAuditAction.Linked), ct);
    }

    public async Task UnlinkConfigurationItemAsync(Guid controlId, Guid configurationItemId, CancellationToken ct)
    {
        ControlConfigurationItemLink? row = await db.ControlConfigurationItemLinks
            .FirstOrDefaultAsync(x => x.InternalControlId == controlId && x.ConfigurationItemId == configurationItemId, ct);
        if (row is null) return;
        db.ControlConfigurationItemLinks.Remove(row);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "ConfigurationItem", configurationItemId.ToString(), null, BusinessAuditAction.Unlinked), ct);
    }

    public async Task LinkBusinessServiceAsync(Guid controlId, Guid businessServiceId, CancellationToken ct)
    {
        await EnsureControlAsync(controlId, ct);
        bool exists = await db.ControlBusinessServiceLinks.AsNoTracking()
            .AnyAsync(x => x.InternalControlId == controlId && x.BusinessServiceId == businessServiceId, ct);
        if (exists) return;
        db.ControlBusinessServiceLinks.Add(ControlBusinessServiceLink.Create(controlId, businessServiceId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "BusinessService", null, businessServiceId.ToString(), BusinessAuditAction.Linked), ct);
    }

    public async Task UnlinkBusinessServiceAsync(Guid controlId, Guid businessServiceId, CancellationToken ct)
    {
        ControlBusinessServiceLink? row = await db.ControlBusinessServiceLinks
            .FirstOrDefaultAsync(x => x.InternalControlId == controlId && x.BusinessServiceId == businessServiceId, ct);
        if (row is null) return;
        db.ControlBusinessServiceLinks.Remove(row);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "BusinessService", businessServiceId.ToString(), null, BusinessAuditAction.Unlinked), ct);
    }

    public async Task LinkManagedDocumentAsync(Guid controlId, Guid managedDocumentId, CancellationToken ct)
    {
        await EnsureControlAsync(controlId, ct);
        bool exists = await db.ControlManagedDocumentLinks.AsNoTracking()
            .AnyAsync(x => x.InternalControlId == controlId && x.ManagedDocumentId == managedDocumentId, ct);
        if (exists) return;
        db.ControlManagedDocumentLinks.Add(ControlManagedDocumentLink.Create(controlId, managedDocumentId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "ManagedDocument", null, managedDocumentId.ToString(), BusinessAuditAction.Linked), ct);
    }

    public async Task UnlinkManagedDocumentAsync(Guid controlId, Guid managedDocumentId, CancellationToken ct)
    {
        ControlManagedDocumentLink? row = await db.ControlManagedDocumentLinks
            .FirstOrDefaultAsync(x => x.InternalControlId == controlId && x.ManagedDocumentId == managedDocumentId, ct);
        if (row is null) return;
        db.ControlManagedDocumentLinks.Remove(row);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "ManagedDocument", managedDocumentId.ToString(), null, BusinessAuditAction.Unlinked), ct);
    }

    public async Task<ControlTestProcedureDto> AddTestProcedureAsync(
        Guid controlId, string title, string procedureSteps, string expectedResult,
        string? purpose, string? sampleGuidance, CancellationToken ct)
    {
        await EnsureControlAsync(controlId, ct);
        ControlTestProcedure entity = ControlTestProcedure.Create(
            controlId, title, procedureSteps, expectedResult, clock.UtcNow, purpose, sampleGuidance);
        db.ControlTestProcedures.Add(entity);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "TestProcedure", null, entity.Title, BusinessAuditAction.Created), ct);
        return MapProcedure(entity);
    }

    public async Task<ControlTestProcedureDto> UpdateTestProcedureAsync(
        Guid controlId, Guid procedureId, string title, string? purpose, string procedureSteps,
        string expectedResult, string? sampleGuidance, bool isActive, CancellationToken ct)
    {
        ControlTestProcedure entity = await db.ControlTestProcedures
            .FirstOrDefaultAsync(x => x.Id == procedureId && x.InternalControlId == controlId, ct)
            ?? throw new InvalidOperationException("Test procedure was not found.");
        entity.Update(title, purpose, procedureSteps, expectedResult, sampleGuidance, isActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "TestProcedure", null, entity.Title), ct);
        return MapProcedure(entity);
    }

    public async Task<EvidenceRequirementDto> AddEvidenceRequirementAsync(
        Guid controlId, string description, ControlFrequency? frequency, string? retentionNotes, bool isRequired,
        CancellationToken ct)
    {
        await EnsureControlAsync(controlId, ct);
        EvidenceRequirement entity = EvidenceRequirement.Create(
            controlId, description, clock.UtcNow, frequency, retentionNotes, isRequired);
        db.EvidenceRequirements.Add(entity);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "EvidenceRequirement", null, entity.Description, BusinessAuditAction.Created), ct);
        return MapEvidence(entity);
    }

    public async Task<EvidenceRequirementDto> UpdateEvidenceRequirementAsync(
        Guid controlId, Guid requirementId, string description, ControlFrequency? frequency,
        string? retentionNotes, bool isRequired, CancellationToken ct)
    {
        EvidenceRequirement entity = await db.EvidenceRequirements
            .FirstOrDefaultAsync(x => x.Id == requirementId && x.InternalControlId == controlId, ct)
            ?? throw new InvalidOperationException("Evidence requirement was not found.");
        entity.Update(description, frequency, retentionNotes, isRequired, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "EvidenceRequirement", null, entity.Description), ct);
        return MapEvidence(entity);
    }

    public async Task DeleteEvidenceRequirementAsync(Guid controlId, Guid requirementId, CancellationToken ct)
    {
        EvidenceRequirement? entity = await db.EvidenceRequirements
            .FirstOrDefaultAsync(x => x.Id == requirementId && x.InternalControlId == controlId, ct);
        if (entity is null) return;
        string desc = entity.Description;
        db.EvidenceRequirements.Remove(entity);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            ControlAudit.Field(controlId, null, "EvidenceRequirement", desc, null, BusinessAuditAction.Unlinked), ct);
    }

    private async Task EnsureControlAsync(Guid controlId, CancellationToken ct)
    {
        bool exists = await db.InternalControls.AsNoTracking().AnyAsync(x => x.Id == controlId, ct);
        if (!exists) throw new InvalidOperationException("Internal control was not found.");
    }

    private async Task<string> IssueControlNumberAsync(string domainCode, CancellationToken ct)
    {
        string sequenceKey = $"controls-{domainCode.ToLowerInvariant()}";
        string issued = await numbers.NextAsync(sequenceKey, "CTRL", ct);
        // Platform format CTRL-YYYY-000001 → CTRL-{DOMAIN}-NNN
        string[] parts = issued.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !long.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
            throw new InvalidOperationException($"Unexpected control sequence format '{issued}'.");
        return $"CTRL-{domainCode}-{n.ToString("000", CultureInfo.InvariantCulture)}";
    }

    private async Task<ControlDetailDto> MapDetailAsync(InternalControl item, CancellationToken ct)
    {
        List<Guid> secondary = await db.ControlSecondaryOwners.AsNoTracking()
            .Where(x => x.InternalControlId == item.Id).Select(x => x.UserId).ToListAsync(ct);
        List<Guid> cis = await db.ControlConfigurationItemLinks.AsNoTracking()
            .Where(x => x.InternalControlId == item.Id).Select(x => x.ConfigurationItemId).ToListAsync(ct);
        List<Guid> services = await db.ControlBusinessServiceLinks.AsNoTracking()
            .Where(x => x.InternalControlId == item.Id).Select(x => x.BusinessServiceId).ToListAsync(ct);
        List<Guid> docs = await db.ControlManagedDocumentLinks.AsNoTracking()
            .Where(x => x.InternalControlId == item.Id).Select(x => x.ManagedDocumentId).ToListAsync(ct);
        List<ControlTestProcedure> procedures = await db.ControlTestProcedures.AsNoTracking()
            .Where(x => x.InternalControlId == item.Id).OrderBy(x => x.Title).ToListAsync(ct);
        List<EvidenceRequirement> evidence = await db.EvidenceRequirements.AsNoTracking()
            .Where(x => x.InternalControlId == item.Id).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);

        ControlDomainCodes.Labels.TryGetValue(item.Domain, out string? label);
        return new(
            item.Id, item.ControlNumber, item.Title, item.Objective, item.Description,
            item.Domain, label ?? item.Domain, item.Frequency.ToString(), item.AutomationType.ToString(),
            item.Status.ToString(), item.PrimaryOwnerUserId, item.PrimaryOwnerRoleId,
            secondary, cis, services, docs,
            procedures.Select(MapProcedure).ToList(),
            evidence.Select(MapEvidence).ToList(),
            item.CreatedAtUtc, item.UpdatedAtUtc, item.RetiredAtUtc, Convert.ToBase64String(item.RowVersion));
    }

    private static ControlListItemDto MapList(InternalControl x)
    {
        ControlDomainCodes.Labels.TryGetValue(x.Domain, out string? label);
        return new(
            x.Id, x.ControlNumber, x.Title, x.Domain, label ?? x.Domain,
            x.PrimaryOwnerUserId, x.Frequency.ToString(), x.AutomationType.ToString(), x.Status.ToString(),
            x.UpdatedAtUtc);
    }

    private static ControlTestProcedureDto MapProcedure(ControlTestProcedure x) => new(
        x.Id, x.InternalControlId, x.Title, x.Purpose, x.ProcedureSteps, x.ExpectedResult, x.SampleGuidance,
        x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static EvidenceRequirementDto MapEvidence(EvidenceRequirement x) => new(
        x.Id, x.InternalControlId, x.Description, x.Frequency?.ToString(), x.RetentionNotes, x.IsRequired,
        x.CreatedAtUtc, x.UpdatedAtUtc);
}
