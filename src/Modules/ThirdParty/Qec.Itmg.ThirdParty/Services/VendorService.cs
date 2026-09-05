using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.ThirdParty.Domain;
using Qec.Itmg.ThirdParty.Persistence;

namespace Qec.Itmg.ThirdParty.Services;

public sealed record VendorDto(
    Guid Id, string VendorNumber, string Name, string? LegalName, string Status, string Criticality,
    string? ServiceDescription, string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
    Guid? OwnerUserId, Guid? RiskId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record VendorContactDto(
    Guid Id, Guid VendorId, string Name, string? Email, string? Phone, string? Role, bool IsPrimary, DateTimeOffset CreatedAtUtc);

public sealed record ContractDto(
    Guid Id, string ContractNumber, Guid VendorId, string Title, string? ContractType, Guid OwnerUserId,
    DateOnly StartDate, DateOnly? EndDate, DateOnly? RenewalDate, bool AutoRenew, string Status,
    string? SlaReference, Guid? ManagedDocumentId, string? Notes,
    int? DaysToExpiry, bool ExpiringSoon, bool Expired,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record VendorAssessmentDto(
    Guid Id, string AssessmentNumber, Guid VendorId, string AssessmentType, Guid OwnerUserId, Guid? ReviewerUserId,
    DateTimeOffset? ScheduledAtUtc, DateTimeOffset? DueAtUtc, DateTimeOffset? CompletedAtUtc,
    string Status, string? Result, string? Summary, Guid? RiskId, bool AssessmentOverdue,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record VendorLinkDto(
    Guid Id, Guid VendorId, string TargetType, Guid TargetId, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc);

public sealed record VendorDashboardCounts(
    int ActiveVendors,
    int CriticalVendors,
    int ContractsExpiring,
    int ExpiredContracts,
    int AssessmentsDue,
    int AssessmentsOverdue,
    int VendorsWithPrivilegedAccess,
    int OpenVendorLinkedRisks,
    string Note);

internal static class TpmAudit
{
    public static BusinessAuditEntry Created(AuditAggregateType type, Guid id, string? number) => new()
    {
        AggregateType = type, AggregateId = id, BusinessNumber = number,
        Action = BusinessAuditAction.Created, Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        AuditAggregateType type, Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated) => new()
    {
        AggregateType = type, AggregateId = id, BusinessNumber = number, Action = action,
        FieldName = field, OldValue = oldValue, NewValue = newValue, Source = AuditSource.Api,
    };
}

public sealed class VendorService(
    ThirdPartyDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string VenSeq = "ven";
    public const string VenPrefix = "VEN";
    public const string CtrSeq = "ctr";
    public const string CtrPrefix = "CTR";
    public const string VasSeq = "vas";
    public const string VasPrefix = "VAS";

    public async Task<VendorDashboardCounts> GetDashboardAsync(
        int vendorsWithPrivilegedAccess,
        CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        DateTimeOffset now = clock.UtcNow;
        List<Vendor> vendors = await db.Vendors.AsNoTracking().ToListAsync(ct);
        List<Contract> contracts = await db.Contracts.AsNoTracking().ToListAsync(ct);
        List<VendorAssessment> assessments = await db.VendorAssessments.AsNoTracking().ToListAsync(ct);

        int active = vendors.Count(x => x.Status == VendorStatus.Active);
        int critical = vendors.Count(x => x.Status == VendorStatus.Active && x.Criticality is VendorCriticality.Critical or VendorCriticality.High);
        int expiring = contracts.Count(x => x.IsExpiringSoon(today));
        int expired = contracts.Count(x => x.Status == ContractStatus.Expired || x.IsExpired(today));
        int due = assessments.Count(x =>
            x.Status != VendorAssessmentStatus.Complete &&
            x.DueAtUtc is DateTimeOffset d && d >= now && d <= now.AddDays(30));
        int overdue = assessments.Count(x => x.IsOverdue(now));
        int openRisks = await db.Vendors.AsNoTracking().CountAsync(x => x.RiskId != null, ct)
            + await db.VendorScopeLinks.AsNoTracking().CountAsync(x => x.TargetType == VendorLinkTargetType.Risk, ct)
            + await db.VendorAssessments.AsNoTracking().CountAsync(x => x.RiskId != null && x.Status != VendorAssessmentStatus.Complete, ct);

        return new(
            active, critical, expiring, expired, due, overdue, vendorsWithPrivilegedAccess, openRisks,
            "Counts only. Not a vendor compliance score.");
    }

    public async Task<IReadOnlyList<VendorDto>> ListVendorsAsync(string? search, VendorStatus? status, CancellationToken ct)
    {
        IQueryable<Vendor> q = db.Vendors.AsNoTracking();
        if (status is VendorStatus s) q = q.Where(x => x.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Name.Contains(term) || x.VendorNumber.Contains(term));
        }
        return (await q.OrderBy(x => x.Name).Take(200).ToListAsync(ct)).Select(MapVendor).ToList();
    }

    public async Task<VendorDto?> GetVendorAsync(Guid id, CancellationToken ct)
    {
        Vendor? item = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapVendor(item);
    }

    public async Task<VendorDto> CreateVendorAsync(
        string name, VendorCriticality criticality, string? legalName, string? serviceDescription,
        string? primaryContactName, string? primaryContactEmail, string? primaryContactPhone,
        Guid? ownerUserId, Guid? riskId, CancellationToken ct)
    {
        VendorDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(VenSeq, VenPrefix, innerCt);
            Vendor entity = Vendor.Create(
                number, name, criticality, clock.UtcNow, legalName, serviceDescription,
                primaryContactName, primaryContactEmail, primaryContactPhone, ownerUserId, riskId);
            db.Vendors.Add(entity);
            await businessAudit.AppendAsync(TpmAudit.Created(AuditAggregateType.Vendor, entity.Id, entity.VendorNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapVendor(entity);
        }, ct);
        return created!;
    }

    public async Task<VendorDto> UpdateVendorAsync(
        Guid id, string name, VendorCriticality criticality, VendorStatus status,
        string? legalName, string? serviceDescription,
        string? primaryContactName, string? primaryContactEmail, string? primaryContactPhone,
        Guid? ownerUserId, Guid? riskId, string rowVersion, CancellationToken ct)
    {
        Vendor entity = await db.Vendors.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Vendor not found.");
        db.Entry(entity).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
        entity.Update(name, criticality, status, legalName, serviceDescription,
            primaryContactName, primaryContactEmail, primaryContactPhone, ownerUserId, riskId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return MapVendor(entity);
    }

    public async Task<IReadOnlyList<VendorContactDto>> ListContactsAsync(Guid vendorId, CancellationToken ct) =>
        (await db.VendorContacts.AsNoTracking().Where(x => x.VendorId == vendorId).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name).ToListAsync(ct))
        .Select(MapContact).ToList();

    public async Task<VendorContactDto> AddContactAsync(
        Guid vendorId, string name, string? email, string? phone, string? role, bool isPrimary, CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(x => x.Id == vendorId, ct))
            throw new InvalidOperationException("Vendor not found.");
        VendorContact contact = VendorContact.Create(vendorId, name, clock.UtcNow, email, phone, role, isPrimary);
        db.VendorContacts.Add(contact);
        await db.SaveChangesAsync(ct);
        return MapContact(contact);
    }

    public async Task<IReadOnlyList<ContractDto>> ListContractsAsync(Guid? vendorId, ContractStatus? status, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        IQueryable<Contract> q = db.Contracts.AsNoTracking();
        if (vendorId is Guid vid) q = q.Where(x => x.VendorId == vid);
        if (status is ContractStatus s) q = q.Where(x => x.Status == s);
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapContract(x, today)).ToList();
    }

    public async Task<ContractDto?> GetContractAsync(Guid id, CancellationToken ct)
    {
        Contract? item = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapContract(item, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }

    public async Task<ContractDto> CreateContractAsync(
        Guid vendorId, string title, Guid ownerUserId, DateOnly startDate,
        string? contractType, DateOnly? endDate, DateOnly? renewalDate, bool autoRenew,
        string? slaReference, Guid? managedDocumentId, string? notes, CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(x => x.Id == vendorId, ct))
            throw new InvalidOperationException("Vendor not found.");
        ContractDto? created = null;
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(CtrSeq, CtrPrefix, innerCt);
            Contract entity = Contract.Create(
                number, vendorId, title, ownerUserId, startDate, clock.UtcNow,
                contractType, endDate, renewalDate, autoRenew, slaReference, managedDocumentId, notes);
            db.Contracts.Add(entity);
            await businessAudit.AppendAsync(TpmAudit.Created(AuditAggregateType.Contract, entity.Id, entity.ContractNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapContract(entity, today);
        }, ct);
        return created!;
    }

    public async Task<ContractDto> TransitionContractAsync(Guid id, ContractStatus status, CancellationToken ct)
    {
        Contract entity = await db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Contract not found.");
        string old = entity.Status.ToString();
        entity.Transition(status, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            TpmAudit.Field(AuditAggregateType.Contract, entity.Id, entity.ContractNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapContract(entity, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }

    public async Task<IReadOnlyList<VendorAssessmentDto>> ListAssessmentsAsync(Guid? vendorId, VendorAssessmentStatus? status, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        IQueryable<VendorAssessment> q = db.VendorAssessments.AsNoTracking();
        if (vendorId is Guid vid) q = q.Where(x => x.VendorId == vid);
        if (status is VendorAssessmentStatus s) q = q.Where(x => x.Status == s);
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapAssessment(x, now)).ToList();
    }

    public async Task<VendorAssessmentDto?> GetAssessmentAsync(Guid id, CancellationToken ct)
    {
        VendorAssessment? item = await db.VendorAssessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapAssessment(item, clock.UtcNow);
    }

    public async Task<VendorAssessmentDto> CreateAssessmentAsync(
        Guid vendorId, VendorAssessmentType type, Guid ownerUserId,
        Guid? reviewerUserId, DateTimeOffset? scheduledAtUtc, DateTimeOffset? dueAtUtc, Guid? riskId,
        CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(x => x.Id == vendorId, ct))
            throw new InvalidOperationException("Vendor not found.");
        VendorAssessmentDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(VasSeq, VasPrefix, innerCt);
            VendorAssessment entity = VendorAssessment.Create(
                number, vendorId, type, ownerUserId, clock.UtcNow, reviewerUserId, scheduledAtUtc, dueAtUtc, riskId);
            db.VendorAssessments.Add(entity);
            await businessAudit.AppendAsync(
                TpmAudit.Created(AuditAggregateType.VendorAssessment, entity.Id, entity.AssessmentNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapAssessment(entity, clock.UtcNow);
        }, ct);
        return created!;
    }

    public async Task<VendorAssessmentDto> TransitionAssessmentAsync(
        Guid id, VendorAssessmentStatus status, VendorAssessmentResult? result, string? summary, CancellationToken ct)
    {
        VendorAssessment entity = await db.VendorAssessments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Assessment not found.");
        string old = entity.Status.ToString();
        entity.Transition(status, clock.UtcNow, result, summary);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            TpmAudit.Field(AuditAggregateType.VendorAssessment, entity.Id, entity.AssessmentNumber, "Status", old,
                entity.Status.ToString(), BusinessAuditAction.StatusChanged), ct);
        return MapAssessment(entity, clock.UtcNow);
    }

    public async Task<IReadOnlyList<VendorLinkDto>> ListLinksAsync(Guid vendorId, CancellationToken ct) =>
        (await db.VendorScopeLinks.AsNoTracking().Where(x => x.VendorId == vendorId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct))
        .Select(MapLink).ToList();

    public async Task<VendorLinkDto> AddLinkAsync(
        Guid vendorId, VendorLinkTargetType targetType, Guid targetId, Guid createdByUserId, CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(x => x.Id == vendorId, ct))
            throw new InvalidOperationException("Vendor not found.");
        if (await db.VendorScopeLinks.AnyAsync(
                x => x.VendorId == vendorId && x.TargetType == targetType && x.TargetId == targetId, ct))
            throw new InvalidOperationException("Link already exists.");
        VendorScopeLink link = VendorScopeLink.Create(vendorId, targetType, targetId, createdByUserId, clock.UtcNow);
        db.VendorScopeLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return MapLink(link);
    }

    public async Task<IReadOnlyList<Contract>> GetContractsForNotificationsAsync(CancellationToken ct) =>
        await db.Contracts.AsNoTracking()
            .Where(x => x.Status == ContractStatus.Active || x.Status == ContractStatus.Expired)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VendorAssessment>> GetAssessmentsForNotificationsAsync(CancellationToken ct) =>
        await db.VendorAssessments.AsNoTracking()
            .Where(x => x.Status != VendorAssessmentStatus.Complete)
            .ToListAsync(ct);

    public async Task<bool> HasNotificationAsync(Guid resourceId, string eventKey, CancellationToken ct) =>
        await db.VendorNotificationLogs.AnyAsync(x => x.ResourceId == resourceId && x.EventKey == eventKey, ct);

    public async Task RecordNotificationAsync(Guid resourceId, string eventKey, CancellationToken ct)
    {
        db.VendorNotificationLogs.Add(VendorNotificationLog.Create(resourceId, eventKey, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private static VendorDto MapVendor(Vendor x) => new(
        x.Id, x.VendorNumber, x.Name, x.LegalName, x.Status.ToString(), x.Criticality.ToString(),
        x.ServiceDescription, x.PrimaryContactName, x.PrimaryContactEmail, x.PrimaryContactPhone,
        x.OwnerUserId, x.RiskId, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static VendorContactDto MapContact(VendorContact x) => new(
        x.Id, x.VendorId, x.Name, x.Email, x.Phone, x.Role, x.IsPrimary, x.CreatedAtUtc);

    private static ContractDto MapContract(Contract x, DateOnly today) => new(
        x.Id, x.ContractNumber, x.VendorId, x.Title, x.ContractType, x.OwnerUserId,
        x.StartDate, x.EndDate, x.RenewalDate, x.AutoRenew, x.Status.ToString(),
        x.SlaReference, x.ManagedDocumentId, x.Notes,
        x.DaysToExpiry(today), x.IsExpiringSoon(today), x.IsExpired(today) || x.Status == ContractStatus.Expired,
        x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static VendorAssessmentDto MapAssessment(VendorAssessment x, DateTimeOffset now) => new(
        x.Id, x.AssessmentNumber, x.VendorId, x.AssessmentType.ToString(), x.OwnerUserId, x.ReviewerUserId,
        x.ScheduledAtUtc, x.DueAtUtc, x.CompletedAtUtc, x.Status.ToString(), x.Result?.ToString(), x.Summary,
        x.RiskId, x.IsOverdue(now), x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static VendorLinkDto MapLink(VendorScopeLink x) => new(
        x.Id, x.VendorId, x.TargetType.ToString(), x.TargetId, x.CreatedByUserId, x.CreatedAtUtc);
}
