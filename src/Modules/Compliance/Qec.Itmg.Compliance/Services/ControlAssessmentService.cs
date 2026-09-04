using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.Compliance.Services;

public sealed record ControlAssessmentDto(
    Guid Id, Guid InternalControlId, Guid? FrameworkVersionId, DateOnly? PeriodStart, DateOnly? PeriodEnd,
    string Status, string Result, Guid? AssessorUserId, DateTimeOffset? AssessmentDateUtc, string? Notes,
    Guid? TestProcedureId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record AssessmentListResult(IReadOnlyList<ControlAssessmentDto> Items, int TotalCount, int Page, int PageSize);

public sealed class ControlAssessmentService(
    ComplianceDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public async Task<AssessmentListResult> ListAsync(
        int page, int pageSize, Guid? internalControlId, AssessmentStatus? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<ControlAssessment> q = db.ControlAssessments.AsNoTracking();
        if (internalControlId is Guid cid) q = q.Where(x => x.InternalControlId == cid);
        if (status is AssessmentStatus s) q = q.Where(x => x.Status == s);
        int total = await q.CountAsync(ct);
        List<ControlAssessment> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<ControlAssessmentDto?> GetAsync(Guid id, CancellationToken ct)
    {
        ControlAssessment? item = await db.ControlAssessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<ControlAssessmentDto> CreateAsync(
        Guid internalControlId, Guid? frameworkVersionId, DateOnly? periodStart, DateOnly? periodEnd,
        Guid? assessorUserId, Guid? testProcedureId, string? notes, CancellationToken ct)
    {
        ControlAssessment entity = ControlAssessment.Create(
            internalControlId, clock.UtcNow, frameworkVersionId, periodStart, periodEnd, assessorUserId, testProcedureId, notes);
        db.ControlAssessments.Add(entity);
        await db.SaveChangesAsync(ct);
        await Audit(entity.Id, BusinessAuditAction.Created, "Status", null, entity.Status.ToString(), ct);
        return Map(entity);
    }

    public async Task<ControlAssessmentDto> StartAsync(Guid id, Guid? assessorUserId, CancellationToken ct)
    {
        ControlAssessment entity = await Load(id, ct);
        string old = entity.Status.ToString();
        entity.Start(assessorUserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await Audit(entity.Id, BusinessAuditAction.StatusChanged, "Status", old, entity.Status.ToString(), ct);
        return Map(entity);
    }

    public async Task<ControlAssessmentDto> RecordResultAsync(Guid id, AssessmentResult result, string? notes, CancellationToken ct)
    {
        ControlAssessment entity = await Load(id, ct);
        string old = entity.Result.ToString();
        entity.RecordResult(result, notes, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await Audit(entity.Id, BusinessAuditAction.Updated, "Result", old, entity.Result.ToString(), ct);
        return Map(entity);
    }

    public async Task<ControlAssessmentDto> CompleteAsync(
        Guid id, AssessmentResult result, Guid? assessorUserId, string? notes, CancellationToken ct)
    {
        ControlAssessment entity = await Load(id, ct);
        string oldStatus = entity.Status.ToString();
        entity.Complete(result, assessorUserId, notes, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await Audit(entity.Id, BusinessAuditAction.StatusChanged, "Status", oldStatus, entity.Status.ToString(), ct);
        return Map(entity);
    }

    private async Task<ControlAssessment> Load(Guid id, CancellationToken ct) =>
        await db.ControlAssessments.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Assessment was not found.");

    private async Task Audit(
        Guid id, BusinessAuditAction action, string field, string? oldValue, string? newValue, CancellationToken ct) =>
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Assessment,
            AggregateId = id,
            Action = action,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        }, ct);

    private static ControlAssessmentDto Map(ControlAssessment x) => new(
        x.Id, x.InternalControlId, x.FrameworkVersionId, x.PeriodStart, x.PeriodEnd,
        x.Status.ToString(), x.Result.ToString(), x.AssessorUserId, x.AssessmentDateUtc, x.Notes,
        x.TestProcedureId, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));
}
