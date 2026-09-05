using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.BusinessContinuity.Domain;
using Qec.Itmg.BusinessContinuity.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Continuity;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.BusinessContinuity.Services;

public sealed record BiaDto(
    Guid Id, string BiaNumber, Guid BusinessServiceId, Guid OwnerUserId, string? BusinessProcessName,
    string BusinessImpactSummary, string? FinancialImpact, string? OperationalImpact, string? RegulatoryImpact,
    string? ReputationalImpact, int? MaximumTolerableDowntimeMinutes, string Criticality, string Status,
    DateTimeOffset? ReviewedAtUtc, Guid? ApprovedByUserId, DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record ContinuityPlanDto(
    Guid Id, string PlanNumber, string Title, string PlanType, Guid OwnerUserId, Guid? ManagedDocumentId,
    string Status, DateTimeOffset? EffectiveAtUtc, DateTimeOffset? ReviewAtUtc, bool IsReviewOverdue,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record RecoveryProcedureDto(
    Guid Id, string ProcedureNumber, Guid ContinuityPlanId, string Title, Guid OwnerUserId,
    Guid? ManagedDocumentId, int? Sequence, string? RecoveryStage, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record DrTestDto(
    Guid Id, string DrTestNumber, string Title, Guid? ContinuityPlanId, Guid BusinessServiceId, string TestType,
    DateTimeOffset PlannedAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, Guid OwnerUserId,
    string Status, string? Result, int? ObservedRtoMinutes, int? ObservedRpoMinutes, string? Summary, string? Gaps,
    bool? RtoMet, bool? RpoMet, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record ContinuityLinkDto(
    Guid Id, Guid OwnerId, string OwnerType, string TargetType, Guid TargetId, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc);

public sealed record ContinuityDashboardCounts(
    int CriticalServices,
    int ServicesWithoutApprovedBia,
    int ServicesWithoutActivePlan,
    int ServicesMissingRecentDrTest,
    int UpcomingDrTests,
    int OverdueDrTests,
    int DrPassed,
    int DrPassedWithGaps,
    int DrFailed,
    int RtoMisses,
    int RpoMisses,
    int ConfirmedSpofs,
    int PlansOverdueReview,
    int OpenBcmLinkedRisks,
    string Note);

public sealed record ServiceReadinessRow(
    Guid BusinessServiceId,
    string? ServiceName,
    int? RtoMinutes,
    int? RpoMinutes,
    string? BiaStatus,
    string? PlanStatus,
    string? LatestDrTestNumber,
    string? LatestDrTestResult,
    int SpofCount);

internal static class BcmAudit
{
    public static BusinessAuditEntry Created(AuditAggregateType type, Guid id, string? number) => new()
    {
        AggregateType = type, AggregateId = id, BusinessNumber = number,
        Action = BusinessAuditAction.Created, Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        AuditAggregateType type, Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated, string? reason = null) => new()
    {
        AggregateType = type, AggregateId = id, BusinessNumber = number, Action = action,
        FieldName = field, OldValue = oldValue, NewValue = newValue, Reason = reason, Source = AuditSource.Api,
    };
}

public sealed class ContinuityService(
    ContinuityDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction) : IDrTestCoverageQuery
{
    public const string BiaSeq = "bia";
    public const string BiaPrefix = "BIA";
    public const string BcpSeq = "bcp";
    public const string BcpPrefix = "BCP";
    public const string DrpSeq = "drp";
    public const string DrpPrefix = "DRP";
    public const string RcpSeq = "rcp";
    public const string RcpPrefix = "RCP";
    public const string DrtSeq = "drt";
    public const string DrtPrefix = "DRT";

    // ——— BIA ———

    public async Task<IReadOnlyList<BiaDto>> ListBiaAsync(Guid? serviceId, BiaStatus? status, CancellationToken ct)
    {
        IQueryable<BiaRecord> q = db.BiaRecords.AsNoTracking();
        if (serviceId is Guid sid) q = q.Where(x => x.BusinessServiceId == sid);
        if (status is BiaStatus s) q = q.Where(x => x.Status == s);
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct)).Select(MapBia).ToList();
    }

    public async Task<BiaDto?> GetBiaAsync(Guid id, CancellationToken ct)
    {
        BiaRecord? item = await db.BiaRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapBia(item);
    }

    public async Task<BiaDto> CreateBiaAsync(
        Guid businessServiceId, Guid ownerUserId, string impactSummary, string criticality,
        string? processName, string? financial, string? operational, string? regulatory, string? reputational,
        int? mtdMinutes, CancellationToken ct)
    {
        BiaDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(BiaSeq, BiaPrefix, innerCt);
            BiaRecord entity = BiaRecord.Create(
                number, businessServiceId, ownerUserId, impactSummary, criticality, clock.UtcNow,
                processName, financial, operational, regulatory, reputational, mtdMinutes);
            db.BiaRecords.Add(entity);
            await businessAudit.AppendAsync(BcmAudit.Created(AuditAggregateType.BiaRecord, entity.Id, entity.BiaNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapBia(entity);
        }, ct);
        return created!;
    }

    public async Task<BiaDto> TransitionBiaAsync(Guid id, BiaStatus next, Guid? actorUserId, CancellationToken ct)
    {
        BiaRecord entity = await db.BiaRecords.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("BIA not found.");
        string old = entity.Status.ToString();
        entity.Transition(next, actorUserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            BcmAudit.Field(AuditAggregateType.BiaRecord, entity.Id, entity.BiaNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapBia(entity);
    }

    // ——— Plans ———

    public async Task<IReadOnlyList<ContinuityPlanDto>> ListPlansAsync(ContinuityPlanType? type, ContinuityPlanStatus? status, CancellationToken ct)
    {
        IQueryable<ContinuityPlan> q = db.ContinuityPlans.AsNoTracking();
        if (type is ContinuityPlanType t) q = q.Where(x => x.PlanType == t);
        if (status is ContinuityPlanStatus s) q = q.Where(x => x.Status == s);
        DateTimeOffset now = clock.UtcNow;
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapPlan(x, now)).ToList();
    }

    public async Task<ContinuityPlanDto?> GetPlanAsync(Guid id, CancellationToken ct)
    {
        ContinuityPlan? item = await db.ContinuityPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapPlan(item, clock.UtcNow);
    }

    public async Task<ContinuityPlanDto> CreatePlanAsync(
        string title, ContinuityPlanType planType, Guid ownerUserId, Guid? managedDocumentId,
        DateTimeOffset? effectiveAtUtc, DateTimeOffset? reviewAtUtc, CancellationToken ct)
    {
        ContinuityPlanDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string seq = planType == ContinuityPlanType.ITDisasterRecovery ? DrpSeq : BcpSeq;
            string prefix = planType == ContinuityPlanType.ITDisasterRecovery ? DrpPrefix : BcpPrefix;
            string number = await numbers.NextAsync(seq, prefix, innerCt);
            ContinuityPlan entity = ContinuityPlan.Create(
                number, title, planType, ownerUserId, clock.UtcNow, managedDocumentId, effectiveAtUtc, reviewAtUtc);
            db.ContinuityPlans.Add(entity);
            await businessAudit.AppendAsync(BcmAudit.Created(AuditAggregateType.ContinuityPlan, entity.Id, entity.PlanNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapPlan(entity, clock.UtcNow);
        }, ct);
        return created!;
    }

    public async Task<ContinuityPlanDto> TransitionPlanAsync(Guid id, ContinuityPlanStatus next, CancellationToken ct)
    {
        ContinuityPlan entity = await db.ContinuityPlans.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Plan not found.");
        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            BcmAudit.Field(AuditAggregateType.ContinuityPlan, entity.Id, entity.PlanNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapPlan(entity, clock.UtcNow);
    }

    // ——— Procedures ———

    public async Task<IReadOnlyList<RecoveryProcedureDto>> ListProceduresAsync(Guid? planId, CancellationToken ct)
    {
        IQueryable<RecoveryProcedure> q = db.RecoveryProcedures.AsNoTracking();
        if (planId is Guid pid) q = q.Where(x => x.ContinuityPlanId == pid);
        return (await q.OrderBy(x => x.Sequence).ThenBy(x => x.Title).Take(200).ToListAsync(ct))
            .Select(MapProcedure).ToList();
    }

    public async Task<RecoveryProcedureDto> CreateProcedureAsync(
        Guid planId, string title, Guid ownerUserId, Guid? managedDocumentId, int? sequence, string? stage, CancellationToken ct)
    {
        _ = await GetPlanAsync(planId, ct) ?? throw new InvalidOperationException("Plan not found.");
        RecoveryProcedureDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(RcpSeq, RcpPrefix, innerCt);
            RecoveryProcedure entity = RecoveryProcedure.Create(
                number, planId, title, ownerUserId, clock.UtcNow, managedDocumentId, sequence, stage);
            db.RecoveryProcedures.Add(entity);
            await businessAudit.AppendAsync(BcmAudit.Created(AuditAggregateType.RecoveryProcedure, entity.Id, entity.ProcedureNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapProcedure(entity);
        }, ct);
        return created!;
    }

    // ——— Links ———

    public async Task<IReadOnlyList<ContinuityLinkDto>> ListLinksAsync(Guid ownerId, string ownerType, CancellationToken ct) =>
        await db.ContinuityScopeLinks.AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.OwnerType == ownerType)
            .Select(x => new ContinuityLinkDto(x.Id, x.OwnerId, x.OwnerType, x.TargetType.ToString(), x.TargetId, x.CreatedByUserId, x.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<ContinuityLinkDto> AddLinkAsync(
        Guid ownerId, string ownerType, ContinuityLinkTargetType targetType, Guid targetId, Guid actorUserId, CancellationToken ct)
    {
        bool exists = await db.ContinuityScopeLinks.AnyAsync(
            x => x.OwnerId == ownerId && x.OwnerType == ownerType && x.TargetType == targetType && x.TargetId == targetId, ct);
        if (exists) throw new InvalidOperationException("Link already exists.");
        ContinuityScopeLink link = ContinuityScopeLink.Create(ownerId, ownerType, targetType, targetId, actorUserId, clock.UtcNow);
        db.ContinuityScopeLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return new(link.Id, link.OwnerId, link.OwnerType, link.TargetType.ToString(), link.TargetId, link.CreatedByUserId, link.CreatedAtUtc);
    }

    // ——— DR Tests ———

    public async Task<IReadOnlyList<DrTestDto>> ListDrTestsAsync(Guid? serviceId, DrTestStatus? status, CancellationToken ct)
    {
        IQueryable<DrTest> q = db.DrTests.AsNoTracking();
        if (serviceId is Guid sid) q = q.Where(x => x.BusinessServiceId == sid);
        if (status is DrTestStatus s) q = q.Where(x => x.Status == s);
        return (await q.OrderByDescending(x => x.PlannedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapDrTest(x, null, null)).ToList();
    }

    public async Task<DrTestDto?> GetDrTestAsync(Guid id, int? serviceRto, int? serviceRpo, CancellationToken ct)
    {
        DrTest? item = await db.DrTests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapDrTest(item, serviceRto, serviceRpo);
    }

    public async Task<DrTestDto> CreateDrTestAsync(
        string title, Guid businessServiceId, DrTestType testType, DateTimeOffset plannedAtUtc,
        Guid ownerUserId, Guid? continuityPlanId, CancellationToken ct)
    {
        DrTestDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(DrtSeq, DrtPrefix, innerCt);
            DrTest entity = DrTest.Create(
                number, title, businessServiceId, testType, plannedAtUtc, ownerUserId, clock.UtcNow, continuityPlanId);
            db.DrTests.Add(entity);
            await businessAudit.AppendAsync(BcmAudit.Created(AuditAggregateType.DrTest, entity.Id, entity.DrTestNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapDrTest(entity, null, null);
        }, ct);
        return created!;
    }

    public async Task<DrTestDto> StartDrTestAsync(Guid id, CancellationToken ct)
    {
        DrTest entity = await LoadDrTest(id, ct);
        string old = entity.Status.ToString();
        entity.Start(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            BcmAudit.Field(AuditAggregateType.DrTest, entity.Id, entity.DrTestNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapDrTest(entity, null, null);
    }

    public async Task<DrTestDto> CompleteDrTestAsync(
        Guid id, DrTestResult result, int? observedRto, int? observedRpo, string? summary, string? gaps,
        int? serviceRto, int? serviceRpo, CancellationToken ct)
    {
        DrTest entity = await LoadDrTest(id, ct);
        string old = entity.Status.ToString();
        entity.Complete(result, clock.UtcNow, observedRto, observedRpo, summary, gaps);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            BcmAudit.Field(AuditAggregateType.DrTest, entity.Id, entity.DrTestNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged, result.ToString()), ct);
        return MapDrTest(entity, serviceRto, serviceRpo);
    }

    public async Task<DrTestDto> CancelDrTestAsync(Guid id, CancellationToken ct)
    {
        DrTest entity = await LoadDrTest(id, ct);
        entity.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return MapDrTest(entity, null, null);
    }

    // ——— Dashboard / readiness ———

    public async Task<ContinuityDashboardCounts> GetDashboardCountsAsync(
        IReadOnlyList<(Guid Id, string Criticality, int? Rto, int? Rpo)> services,
        int confirmedSpofs,
        CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        HashSet<Guid> criticalIds = services
            .Where(s => s.Criticality is "High" or "Critical")
            .Select(s => s.Id)
            .ToHashSet();
        int criticalServices = criticalIds.Count;

        List<BiaRecord> bias = await db.BiaRecords.AsNoTracking().ToListAsync(ct);
        HashSet<Guid> approvedBiaServices = bias
            .Where(x => x.Status == BiaStatus.Approved)
            .Select(x => x.BusinessServiceId)
            .ToHashSet();
        int withoutBia = criticalIds.Count(id => !approvedBiaServices.Contains(id));

        List<ContinuityScopeLink> planServiceLinks = await db.ContinuityScopeLinks.AsNoTracking()
            .Where(x => x.OwnerType == "ContinuityPlan" && x.TargetType == ContinuityLinkTargetType.BusinessService)
            .ToListAsync(ct);
        List<ContinuityPlan> activePlans = await db.ContinuityPlans.AsNoTracking()
            .Where(x => x.Status == ContinuityPlanStatus.Active).ToListAsync(ct);
        HashSet<Guid> activePlanIds = activePlans.Select(x => x.Id).ToHashSet();
        HashSet<Guid> servicesWithActivePlan = planServiceLinks
            .Where(x => activePlanIds.Contains(x.OwnerId))
            .Select(x => x.TargetId)
            .ToHashSet();
        int withoutPlan = criticalIds.Count(id => !servicesWithActivePlan.Contains(id));

        DrTestCoverageSnapshot miss = await GetMissingForCriticalServicesAsync(
            criticalIds.Select(id => (id, "Critical")).ToList(), now, 365, ct);

        List<DrTest> tests = await db.DrTests.AsNoTracking().ToListAsync(ct);
        int upcoming = tests.Count(x => x.Status == DrTestStatus.Planned && x.PlannedAtUtc >= now && x.PlannedAtUtc <= now.AddDays(30));
        int overdue = tests.Count(x => x.Status == DrTestStatus.Planned && x.PlannedAtUtc < now);
        int passed = tests.Count(x => x.Result == DrTestResult.Passed);
        int gaps = tests.Count(x => x.Result == DrTestResult.PassedWithGaps);
        int failed = tests.Count(x => x.Result == DrTestResult.Failed);

        Dictionary<Guid, (int? Rto, int? Rpo)> serviceTargets = services.ToDictionary(s => s.Id, s => (s.Rto, s.Rpo));
        int rtoMiss = 0, rpoMiss = 0;
        foreach (DrTest t in tests.Where(x => x.Status == DrTestStatus.Completed))
        {
            if (!serviceTargets.TryGetValue(t.BusinessServiceId, out var target)) continue;
            if (t.ObservedRtoMinutes is int oRto && target.Rto is int sRto && oRto > sRto) rtoMiss++;
            if (t.ObservedRpoMinutes is int oRpo && target.Rpo is int sRpo && oRpo > sRpo) rpoMiss++;
        }

        int plansOverdue = activePlans.Count(x => x.IsReviewOverdue(now));
        int openRiskLinks = await db.ContinuityScopeLinks.AsNoTracking()
            .CountAsync(x => x.TargetType == ContinuityLinkTargetType.Risk, ct);

        return new(
            criticalServices, withoutBia, withoutPlan, miss.CriticalServicesMissingRecentDrTest,
            upcoming, overdue, passed, gaps, failed, rtoMiss, rpoMiss, confirmedSpofs, plansOverdue, openRiskLinks,
            "Counts only. Not a BCM compliance score.");
    }

    public async Task<IReadOnlyList<ServiceReadinessRow>> GetServiceReadinessAsync(
        IReadOnlyList<(Guid Id, string Name, string Criticality, int? Rto, int? Rpo)> services,
        IReadOnlyDictionary<Guid, int> spofByService,
        CancellationToken ct)
    {
        List<BiaRecord> bias = await db.BiaRecords.AsNoTracking().ToListAsync(ct);
        List<ContinuityPlan> plans = await db.ContinuityPlans.AsNoTracking().ToListAsync(ct);
        List<ContinuityScopeLink> links = await db.ContinuityScopeLinks.AsNoTracking()
            .Where(x => x.OwnerType == "ContinuityPlan" && x.TargetType == ContinuityLinkTargetType.BusinessService)
            .ToListAsync(ct);
        List<DrTest> tests = await db.DrTests.AsNoTracking().ToListAsync(ct);

        List<ServiceReadinessRow> rows = [];
        foreach (var svc in services)
        {
            BiaRecord? bia = bias.Where(x => x.BusinessServiceId == svc.Id)
                .OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
            List<Guid> planIds = links.Where(x => x.TargetId == svc.Id).Select(x => x.OwnerId).ToList();
            ContinuityPlan? plan = plans.Where(x => planIds.Contains(x.Id))
                .OrderByDescending(x => x.Status == ContinuityPlanStatus.Active)
                .ThenByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
            DrTest? latest = tests.Where(x => x.BusinessServiceId == svc.Id)
                .OrderByDescending(x => x.CompletedAtUtc ?? x.PlannedAtUtc).FirstOrDefault();
            spofByService.TryGetValue(svc.Id, out int spofCount);
            rows.Add(new(
                svc.Id, svc.Name, svc.Rto, svc.Rpo, bia?.Status.ToString(), plan?.Status.ToString(),
                latest?.DrTestNumber, latest?.Result?.ToString(), spofCount));
        }

        return rows;
    }

    public async Task<DrTestCoverageSnapshot> GetMissingForCriticalServicesAsync(
        IReadOnlyCollection<(Guid ServiceId, string Criticality)> services,
        DateTimeOffset asOfUtc,
        int recentDays = 365,
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> critical = services
            .Where(s => s.Criticality is "High" or "Critical" or "critical" or "high")
            .Select(s => s.ServiceId)
            .ToHashSet();
        if (critical.Count == 0) return new(0);

        DateTimeOffset since = asOfUtc.AddDays(-recentDays);
        HashSet<Guid> tested = (await db.DrTests.AsNoTracking()
            .Where(x => x.Status == DrTestStatus.Completed
                && x.CompletedAtUtc != null
                && x.CompletedAtUtc >= since
                && (x.Result == DrTestResult.Passed || x.Result == DrTestResult.PassedWithGaps))
            .Select(x => x.BusinessServiceId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return new(critical.Count(id => !tested.Contains(id)));
    }

    public async Task<IReadOnlyList<ContinuityPlan>> GetPlansNeedingReviewAsync(CancellationToken ct)
    {
        return await db.ContinuityPlans
            .Where(x => x.Status == ContinuityPlanStatus.Active && x.ReviewAtUtc != null)
            .ToListAsync(ct);
    }

    /// <summary>BIA stuck in review or approved past annual re-review window.</summary>
    public async Task<IReadOnlyList<BiaRecord>> GetBiasNeedingReviewAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset inReviewCutoff = now.AddDays(-14);
        DateTimeOffset annualCutoff = now.AddDays(-365);
        return await db.BiaRecords.AsNoTracking()
            .Where(x =>
                (x.Status == BiaStatus.InReview && x.ReviewedAtUtc != null && x.ReviewedAtUtc < inReviewCutoff) ||
                (x.Status == BiaStatus.Approved && x.ApprovedAtUtc != null && x.ApprovedAtUtc < annualCutoff))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DrTest>> GetDrTestNotificationCandidatesAsync(CancellationToken ct) =>
        await db.DrTests
            .Where(x => x.Status == DrTestStatus.Planned || x.Status == DrTestStatus.Completed)
            .ToListAsync(ct);

    public async Task<bool> HasNotificationAsync(Guid resourceId, string eventKey, CancellationToken ct) =>
        await db.ContinuityNotificationLogs.AnyAsync(x => x.ResourceId == resourceId && x.EventKey == eventKey, ct);

    public async Task RecordNotificationAsync(Guid resourceId, string eventKey, CancellationToken ct)
    {
        db.ContinuityNotificationLogs.Add(ContinuityNotificationLog.Create(resourceId, eventKey, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private async Task<DrTest> LoadDrTest(Guid id, CancellationToken ct) =>
        await db.DrTests.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("DR test not found.");

    private static BiaDto MapBia(BiaRecord x) => new(
        x.Id, x.BiaNumber, x.BusinessServiceId, x.OwnerUserId, x.BusinessProcessName, x.BusinessImpactSummary,
        x.FinancialImpact, x.OperationalImpact, x.RegulatoryImpact, x.ReputationalImpact,
        x.MaximumTolerableDowntimeMinutes, x.Criticality, x.Status.ToString(), x.ReviewedAtUtc, x.ApprovedByUserId,
        x.ApprovedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static ContinuityPlanDto MapPlan(ContinuityPlan x, DateTimeOffset now) => new(
        x.Id, x.PlanNumber, x.Title, x.PlanType.ToString(), x.OwnerUserId, x.ManagedDocumentId, x.Status.ToString(),
        x.EffectiveAtUtc, x.ReviewAtUtc, x.IsReviewOverdue(now), x.CreatedAtUtc, x.UpdatedAtUtc,
        Convert.ToBase64String(x.RowVersion));

    private static RecoveryProcedureDto MapProcedure(RecoveryProcedure x) => new(
        x.Id, x.ProcedureNumber, x.ContinuityPlanId, x.Title, x.OwnerUserId, x.ManagedDocumentId, x.Sequence,
        x.RecoveryStage, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static DrTestDto MapDrTest(DrTest x, int? serviceRto, int? serviceRpo)
    {
        bool? rtoMet = serviceRto is int targetRto && x.ObservedRtoMinutes is int observedRto
            ? observedRto <= targetRto : null;
        bool? rpoMet = serviceRpo is int targetRpo && x.ObservedRpoMinutes is int observedRpo
            ? observedRpo <= targetRpo : null;
        return new(
            x.Id, x.DrTestNumber, x.Title, x.ContinuityPlanId, x.BusinessServiceId, x.TestType.ToString(),
            x.PlannedAtUtc, x.StartedAtUtc, x.CompletedAtUtc, x.OwnerUserId, x.Status.ToString(),
            x.Result?.ToString(), x.ObservedRtoMinutes, x.ObservedRpoMinutes, x.Summary, x.Gaps,
            rtoMet, rpoMet, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));
    }
}
