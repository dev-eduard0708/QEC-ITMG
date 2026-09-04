using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Audit.Domain;
using Qec.Itmg.Audit.Persistence;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.Audit.Services;

public sealed record AuditEngagementDto(
    Guid Id, string AuditNumber, string Title, string AuditType, string? Objective, string? ScopeSummary,
    Guid? LeadAuditorUserId, Guid? OwnerUserId, DateOnly? StartDate, DateOnly? EndDate, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ClosedAtUtc, string RowVersion);

public sealed record AuditListResult(IReadOnlyList<AuditEngagementDto> Items, int TotalCount, int Page, int PageSize);

public sealed record AuditScopeLinkDto(Guid Id, Guid AuditEngagementId, string TargetType, Guid TargetId, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc);

public sealed record AuditQuestionDto(
    Guid Id, Guid AuditEngagementId, string? QuestionCode, string Category, string QuestionText,
    Guid? FrameworkRequirementId, Guid? InternalControlId, string ResponseType, bool Required, int SortOrder,
    string Status, string? Response, Guid? RespondedByUserId, DateTimeOffset? RespondedAtUtc, string? ReviewerNotes);

public sealed record FindingDto(
    Guid Id, string FindingNumber, Guid AuditEngagementId, Guid? InternalControlId, string Title, string Description,
    string Severity, string Status, Guid? OwnerUserId, DateTimeOffset? DueAtUtc, string? AcceptedRiskReason,
    string? ExceptionReference, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ClosedAtUtc, string RowVersion);

public sealed record ManagementResponseDto(
    Guid Id, Guid FindingId, string ResponseText, Guid RespondedByUserId, DateTimeOffset RespondedAtUtc,
    DateOnly? TargetDate, Guid? ManagementOwnerUserId);

public sealed record CorrectiveActionDto(
    Guid Id, string? ActionNumber, Guid FindingId, string Title, string Description, Guid OwnerUserId,
    DateTimeOffset? DueAtUtc, string Status, bool IsMandatory, bool IsOverdue, DateTimeOffset? CompletedAtUtc,
    Guid? VerifiedByUserId, DateTimeOffset? VerifiedAtUtc, string? VerificationNotes,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion);

public sealed record EvidenceRequestDto(
    Guid Id, Guid AuditEngagementId, Guid? AuditQuestionId, Guid? InternalControlId, string Title, string? Description,
    Guid? RequestedFromUserId, DateTimeOffset? DueAtUtc, string Status, Guid? EvidenceId, Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? FulfilledAtUtc, string? Notes, bool IsOverdue);

public sealed record AuditReadinessCounts(
    int OpenFindings,
    int OverdueCapa,
    int OpenEvidenceRequests,
    int OverdueEvidenceRequests,
    int CompletedCapaAwaitingVerification,
    int VerifiedCapa);

public sealed record CapaSummaryCounts(int Open, int Overdue, int CompletedAwaitingVerification, int Verified);

internal static class AuditAudit
{
    public static BusinessAuditEntry Created(AuditAggregateType type, Guid id, string? number) => new()
    {
        AggregateType = type,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Created,
        Source = AuditSource.Api,
    };

    public static BusinessAuditEntry Field(
        AuditAggregateType type, Guid id, string? number, string field, string? oldValue, string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated, string? reason = null) => new()
    {
        AggregateType = type,
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

public sealed class AuditService(
    AuditDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string AuditSequenceKey = "audit";
    public const string AuditPrefix = "AUD";
    public const string FindingSequenceKey = "finding";
    public const string FindingPrefix = "FND";
    public const string CapaSequenceKey = "capa";
    public const string CapaPrefix = "CAPA";

    public async Task<AuditListResult> ListEngagementsAsync(
        int page, int pageSize, string? search, AuditEngagementStatus? status, AuditType? type, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<AuditEngagement> q = db.AuditEngagements.AsNoTracking();
        if (status is AuditEngagementStatus s) q = q.Where(x => x.Status == s);
        if (type is AuditType t) q = q.Where(x => x.AuditType == t);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.AuditNumber.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<AuditEngagement> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(MapEngagement).ToList(), total, page, pageSize);
    }

    public async Task<AuditEngagementDto?> GetEngagementAsync(Guid id, CancellationToken ct)
    {
        AuditEngagement? item = await db.AuditEngagements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapEngagement(item);
    }

    public async Task<AuditEngagementDto> CreateEngagementAsync(
        string title, AuditType auditType, string? objective, string? scopeSummary,
        Guid? leadAuditorUserId, Guid? ownerUserId, DateOnly? startDate, DateOnly? endDate,
        bool seedIsa315Questions, CancellationToken ct)
    {
        AuditEngagementDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(AuditSequenceKey, AuditPrefix, innerCt);
            AuditEngagement entity = AuditEngagement.Create(
                number, title, auditType, clock.UtcNow, objective, scopeSummary,
                leadAuditorUserId, ownerUserId, startDate, endDate);
            db.AuditEngagements.Add(entity);
            if (seedIsa315Questions && auditType == AuditType.ISA315Profile)
                SeedIsa315Questions(entity.Id);
            await businessAudit.AppendAsync(AuditAudit.Created(AuditAggregateType.AuditEngagement, entity.Id, entity.AuditNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapEngagement(entity);
        }, ct);
        return created!;
    }

    public async Task<AuditEngagementDto> UpdateEngagementAsync(
        Guid id, string title, string? objective, string? scopeSummary,
        Guid? leadAuditorUserId, Guid? ownerUserId, DateOnly? startDate, DateOnly? endDate, CancellationToken ct)
    {
        AuditEngagement entity = await LoadEngagement(id, ct);
        entity.Update(title, objective, scopeSummary, leadAuditorUserId, ownerUserId, startDate, endDate, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, entity.Id, entity.AuditNumber, "Title", null, title), ct);
        return MapEngagement(entity);
    }

    public async Task<AuditEngagementDto> TransitionEngagementAsync(Guid id, AuditEngagementStatus next, CancellationToken ct)
    {
        AuditEngagement entity = await LoadEngagement(id, ct);
        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, entity.Id, entity.AuditNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapEngagement(entity);
    }

    public async Task<IReadOnlyList<AuditScopeLinkDto>> ListScopeAsync(Guid engagementId, CancellationToken ct) =>
        await db.AuditScopeLinks.AsNoTracking()
            .Where(x => x.AuditEngagementId == engagementId)
            .OrderBy(x => x.TargetType).ThenBy(x => x.CreatedAtUtc)
            .Select(x => new AuditScopeLinkDto(x.Id, x.AuditEngagementId, x.TargetType.ToString(), x.TargetId, x.CreatedByUserId, x.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<AuditScopeLinkDto> AddScopeAsync(
        Guid engagementId, AuditScopeTargetType targetType, Guid targetId, Guid actorUserId, CancellationToken ct)
    {
        _ = await LoadEngagement(engagementId, ct);
        bool exists = await db.AuditScopeLinks.AnyAsync(
            x => x.AuditEngagementId == engagementId && x.TargetType == targetType && x.TargetId == targetId, ct);
        if (exists) throw new InvalidOperationException("Scope link already exists.");
        AuditScopeLink link = AuditScopeLink.Create(engagementId, targetType, targetId, actorUserId, clock.UtcNow);
        db.AuditScopeLinks.Add(link);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, engagementId, null, "Scope", null,
                $"{targetType}:{targetId}", BusinessAuditAction.Linked), ct);
        return new(link.Id, link.AuditEngagementId, link.TargetType.ToString(), link.TargetId, link.CreatedByUserId, link.CreatedAtUtc);
    }

    public async Task RemoveScopeAsync(Guid engagementId, Guid linkId, CancellationToken ct)
    {
        AuditScopeLink? link = await db.AuditScopeLinks.FirstOrDefaultAsync(
            x => x.Id == linkId && x.AuditEngagementId == engagementId, ct)
            ?? throw new InvalidOperationException("Scope link not found.");
        db.AuditScopeLinks.Remove(link);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, engagementId, null, "Scope",
                $"{link.TargetType}:{link.TargetId}", null, BusinessAuditAction.Unlinked), ct);
    }

    public async Task<IReadOnlyList<AuditQuestionDto>> ListQuestionsAsync(Guid engagementId, CancellationToken ct) =>
        (await db.AuditQuestions.AsNoTracking()
            .Where(x => x.AuditEngagementId == engagementId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Category)
            .ToListAsync(ct))
        .Select(MapQuestion).ToList();

    public async Task<AuditQuestionDto> AddQuestionAsync(
        Guid engagementId, string category, string questionText, AuditQuestionResponseType responseType,
        bool required, int? sortOrder, string? questionCode, Guid? frameworkRequirementId, Guid? internalControlId,
        CancellationToken ct)
    {
        _ = await LoadEngagement(engagementId, ct);
        int order = sortOrder ?? ((await db.AuditQuestions.Where(x => x.AuditEngagementId == engagementId)
            .MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 10);
        AuditQuestion q = AuditQuestion.Create(
            engagementId, category, questionText, responseType, required, order, questionCode,
            frameworkRequirementId, internalControlId);
        db.AuditQuestions.Add(q);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, engagementId, null, "Question", null, q.Id.ToString()), ct);
        return MapQuestion(q);
    }

    public async Task<AuditQuestionDto> AnswerQuestionAsync(Guid engagementId, Guid questionId, string? response, Guid userId, CancellationToken ct)
    {
        AuditQuestion q = await LoadQuestion(engagementId, questionId, ct);
        q.Answer(response, userId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, engagementId, null, "QuestionResponse", null, questionId.ToString()), ct);
        return MapQuestion(q);
    }

    public async Task<AuditQuestionDto> ReviewQuestionAsync(Guid engagementId, Guid questionId, string? notes, CancellationToken ct)
    {
        AuditQuestion q = await LoadQuestion(engagementId, questionId, ct);
        q.MarkReviewed(notes);
        await db.SaveChangesAsync(ct);
        return MapQuestion(q);
    }

    public async Task<AuditQuestionDto> MarkQuestionNaAsync(Guid engagementId, Guid questionId, string? notes, CancellationToken ct)
    {
        AuditQuestion q = await LoadQuestion(engagementId, questionId, ct);
        q.MarkNotApplicable(notes);
        await db.SaveChangesAsync(ct);
        return MapQuestion(q);
    }

    public async Task<IReadOnlyList<FindingDto>> ListFindingsAsync(Guid? engagementId, FindingStatus? status, CancellationToken ct)
    {
        IQueryable<Finding> q = db.Findings.AsNoTracking();
        if (engagementId is Guid eid) q = q.Where(x => x.AuditEngagementId == eid);
        if (status is FindingStatus s) q = q.Where(x => x.Status == s);
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct)).Select(MapFinding).ToList();
    }

    public async Task<FindingDto?> GetFindingAsync(Guid id, CancellationToken ct)
    {
        Finding? item = await db.Findings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapFinding(item);
    }

    public async Task<FindingDto> CreateFindingAsync(
        Guid engagementId, string title, string description, FindingSeverity severity,
        Guid? internalControlId, Guid? ownerUserId, DateTimeOffset? dueAtUtc, CancellationToken ct)
    {
        _ = await LoadEngagement(engagementId, ct);
        FindingDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(FindingSequenceKey, FindingPrefix, innerCt);
            Finding entity = Finding.Create(
                number, engagementId, title, description, severity, clock.UtcNow, internalControlId, ownerUserId, dueAtUtc);
            db.Findings.Add(entity);
            await businessAudit.AppendAsync(AuditAudit.Created(AuditAggregateType.Finding, entity.Id, entity.FindingNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapFinding(entity);
        }, ct);
        return created!;
    }

    public async Task<FindingDto> TransitionFindingAsync(
        Guid id, FindingStatus next, string? acceptedRiskReason, string? exceptionReference, bool overrideCapaGate, CancellationToken ct)
    {
        Finding entity = await LoadFinding(id, ct);
        if (next == FindingStatus.Closed && !overrideCapaGate)
        {
            List<CorrectiveAction> capas = await db.CorrectiveActions.Where(x => x.FindingId == id).ToListAsync(ct);
            if (capas.Any(x => x.IsMandatory && x.Status != CorrectiveActionStatus.Verified))
                throw new InvalidOperationException(
                    "Mandatory corrective actions must be Verified before closing, or use AcceptedRisk/override.");
        }

        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow, acceptedRiskReason, exceptionReference);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.Finding, entity.Id, entity.FindingNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged,
                next == FindingStatus.AcceptedRisk ? acceptedRiskReason : overrideCapaGate ? "capa-override" : null), ct);
        return MapFinding(entity);
    }

    public async Task<ManagementResponseDto> AddManagementResponseAsync(
        Guid findingId, string responseText, Guid respondedByUserId, DateOnly? targetDate, Guid? managementOwnerUserId, CancellationToken ct)
    {
        _ = await LoadFinding(findingId, ct);
        ManagementResponse response = ManagementResponse.Create(
            findingId, responseText, respondedByUserId, clock.UtcNow, targetDate, managementOwnerUserId);
        db.ManagementResponses.Add(response);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.Finding, findingId, null, "ManagementResponse", null, response.Id.ToString()), ct);
        return MapResponse(response);
    }

    public async Task<IReadOnlyList<ManagementResponseDto>> ListManagementResponsesAsync(Guid findingId, CancellationToken ct) =>
        (await db.ManagementResponses.AsNoTracking().Where(x => x.FindingId == findingId)
            .OrderByDescending(x => x.RespondedAtUtc).ToListAsync(ct))
        .Select(MapResponse).ToList();

    public async Task<IReadOnlyList<CorrectiveActionDto>> ListCapaAsync(Guid? findingId, Guid? engagementId, CancellationToken ct)
    {
        IQueryable<CorrectiveAction> q = db.CorrectiveActions.AsNoTracking();
        if (findingId is Guid fid) q = q.Where(x => x.FindingId == fid);
        if (engagementId is Guid eid)
        {
            List<Guid> findingIds = await db.Findings.AsNoTracking()
                .Where(x => x.AuditEngagementId == eid).Select(x => x.Id).ToListAsync(ct);
            q = q.Where(x => findingIds.Contains(x.FindingId));
        }

        DateTimeOffset now = clock.UtcNow;
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapCapa(x, now)).ToList();
    }

    public async Task<CorrectiveActionDto> CreateCapaAsync(
        Guid findingId, string title, string description, Guid ownerUserId, DateTimeOffset? dueAtUtc, bool isMandatory, CancellationToken ct)
    {
        _ = await LoadFinding(findingId, ct);
        CorrectiveActionDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(CapaSequenceKey, CapaPrefix, innerCt);
            CorrectiveAction entity = CorrectiveAction.Create(
                number, findingId, title, description, ownerUserId, clock.UtcNow, dueAtUtc, isMandatory);
            db.CorrectiveActions.Add(entity);
            Finding finding = await LoadFinding(findingId, innerCt);
            if (finding.Status == FindingStatus.Open)
                finding.Transition(FindingStatus.InRemediation, clock.UtcNow);
            await businessAudit.AppendAsync(AuditAudit.Created(AuditAggregateType.Finding, findingId, entity.ActionNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapCapa(entity, clock.UtcNow);
        }, ct);
        return created!;
    }

    public async Task<CorrectiveActionDto> TransitionCapaAsync(
        Guid id, CorrectiveActionStatus next, Guid? verifiedBy, string? notes, CancellationToken ct)
    {
        CorrectiveAction entity = await db.CorrectiveActions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Corrective action not found.");
        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow, verifiedBy, notes);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.Finding, entity.FindingId, entity.ActionNumber, "CapaStatus", old, next.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapCapa(entity, clock.UtcNow);
    }

    public async Task<CapaSummaryCounts> GetCapaSummaryAsync(Guid? engagementId, CancellationToken ct)
    {
        IQueryable<CorrectiveAction> q = db.CorrectiveActions.AsNoTracking();
        if (engagementId is Guid eid)
        {
            List<Guid> findingIds = await db.Findings.AsNoTracking()
                .Where(x => x.AuditEngagementId == eid).Select(x => x.Id).ToListAsync(ct);
            q = q.Where(x => findingIds.Contains(x.FindingId));
        }

        List<CorrectiveAction> items = await q.ToListAsync(ct);
        DateTimeOffset now = clock.UtcNow;
        return new(
            items.Count(x => x.Status is CorrectiveActionStatus.Open or CorrectiveActionStatus.InProgress),
            items.Count(x => x.IsOverdue(now)),
            items.Count(x => x.Status == CorrectiveActionStatus.Completed),
            items.Count(x => x.Status == CorrectiveActionStatus.Verified));
    }

    public async Task<IReadOnlyList<EvidenceRequestDto>> ListEvidenceRequestsAsync(Guid? engagementId, EvidenceRequestStatus? status, CancellationToken ct)
    {
        IQueryable<EvidenceRequest> q = db.EvidenceRequests.AsNoTracking();
        if (engagementId is Guid eid) q = q.Where(x => x.AuditEngagementId == eid);
        if (status is EvidenceRequestStatus s) q = q.Where(x => x.Status == s);
        DateTimeOffset now = clock.UtcNow;
        return (await q.OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapEvidenceRequest(x, now)).ToList();
    }

    public async Task<EvidenceRequestDto> CreateEvidenceRequestAsync(
        Guid engagementId, string title, string? description, Guid createdByUserId,
        Guid? auditQuestionId, Guid? internalControlId, Guid? requestedFromUserId, DateTimeOffset? dueAtUtc, CancellationToken ct)
    {
        _ = await LoadEngagement(engagementId, ct);
        EvidenceRequest entity = EvidenceRequest.Create(
            engagementId, title, createdByUserId, clock.UtcNow, description, auditQuestionId, internalControlId,
            requestedFromUserId, dueAtUtc);
        db.EvidenceRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, engagementId, null, "EvidenceRequest", null, entity.Id.ToString()), ct);
        return MapEvidenceRequest(entity, clock.UtcNow);
    }

    public async Task<EvidenceRequestDto> FulfillEvidenceRequestAsync(Guid id, Guid evidenceId, string? notes, CancellationToken ct)
    {
        EvidenceRequest entity = await db.EvidenceRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Evidence request not found.");
        entity.Fulfill(evidenceId, clock.UtcNow, notes);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            AuditAudit.Field(AuditAggregateType.AuditEngagement, entity.AuditEngagementId, null, "EvidenceRequestFulfilled",
                null, $"{id}:{evidenceId}"), ct);
        return MapEvidenceRequest(entity, clock.UtcNow);
    }

    public async Task<EvidenceRequestDto> UpdateEvidenceRequestStatusAsync(Guid id, EvidenceRequestStatus status, string? notes, CancellationToken ct)
    {
        EvidenceRequest entity = await db.EvidenceRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Evidence request not found.");
        if (status == EvidenceRequestStatus.InProgress) entity.MarkInProgress();
        else if (status == EvidenceRequestStatus.Cancelled) entity.Cancel(notes);
        else throw new InvalidOperationException("Use fulfill endpoint for Fulfilled status.");
        await db.SaveChangesAsync(ct);
        return MapEvidenceRequest(entity, clock.UtcNow);
    }

    public async Task<AuditReadinessCounts> GetInternalReadinessAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        int openFindings = await db.Findings.AsNoTracking().CountAsync(
            x => x.Status != FindingStatus.Closed && x.Status != FindingStatus.AcceptedRisk, ct);
        List<CorrectiveAction> capas = await db.CorrectiveActions.AsNoTracking().ToListAsync(ct);
        int overdueCapa = capas.Count(x => x.IsOverdue(now));
        int awaiting = capas.Count(x => x.Status == CorrectiveActionStatus.Completed);
        int verified = capas.Count(x => x.Status == CorrectiveActionStatus.Verified);
        int openRequests = await db.EvidenceRequests.AsNoTracking().CountAsync(
            x => x.Status == EvidenceRequestStatus.Requested || x.Status == EvidenceRequestStatus.InProgress, ct);
        int overdueRequests = await db.EvidenceRequests.AsNoTracking().CountAsync(
            x => (x.Status == EvidenceRequestStatus.Requested || x.Status == EvidenceRequestStatus.InProgress)
                && x.DueAtUtc != null && x.DueAtUtc < now, ct);
        return new(openFindings, overdueCapa, openRequests, overdueRequests, awaiting, verified);
    }

    public async Task<IReadOnlyList<EvidenceRequest>> GetDueEvidenceRequestCandidatesAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        return await db.EvidenceRequests
            .Where(x => (x.Status == EvidenceRequestStatus.Requested || x.Status == EvidenceRequestStatus.InProgress)
                && x.DueAtUtc != null && x.RequestedFromUserId != null)
            .ToListAsync(ct);
    }

    public async Task<bool> HasNotificationAsync(Guid requestId, string eventKey, CancellationToken ct) =>
        await db.EvidenceRequestNotificationLogs.AnyAsync(x => x.EvidenceRequestId == requestId && x.EventKey == eventKey, ct);

    public async Task RecordNotificationAsync(Guid requestId, string eventKey, CancellationToken ct)
    {
        db.EvidenceRequestNotificationLogs.Add(EvidenceRequestNotificationLog.Create(requestId, eventKey, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task<(AuditEngagement Engagement, IReadOnlyList<AuditScopeLink> Scope, IReadOnlyList<AuditQuestion> Questions,
        IReadOnlyList<Finding> Findings, IReadOnlyList<ManagementResponse> Responses, IReadOnlyList<CorrectiveAction> Capas,
        IReadOnlyList<EvidenceRequest> Requests)> LoadPackDataAsync(Guid engagementId, CancellationToken ct)
    {
        AuditEngagement engagement = await LoadEngagement(engagementId, ct);
        List<AuditScopeLink> scope = await db.AuditScopeLinks.AsNoTracking().Where(x => x.AuditEngagementId == engagementId).ToListAsync(ct);
        List<AuditQuestion> questions = await db.AuditQuestions.AsNoTracking().Where(x => x.AuditEngagementId == engagementId).ToListAsync(ct);
        List<Finding> findings = await db.Findings.AsNoTracking().Where(x => x.AuditEngagementId == engagementId).ToListAsync(ct);
        List<Guid> findingIds = findings.Select(x => x.Id).ToList();
        List<ManagementResponse> responses = await db.ManagementResponses.AsNoTracking()
            .Where(x => findingIds.Contains(x.FindingId)).ToListAsync(ct);
        List<CorrectiveAction> capas = await db.CorrectiveActions.AsNoTracking()
            .Where(x => findingIds.Contains(x.FindingId)).ToListAsync(ct);
        List<EvidenceRequest> requests = await db.EvidenceRequests.AsNoTracking()
            .Where(x => x.AuditEngagementId == engagementId).ToListAsync(ct);
        return (engagement, scope, questions, findings, responses, capas, requests);
    }

    private void SeedIsa315Questions(Guid engagementId)
    {
        (string Code, string Category, string Text)[] seeds =
        [
            ("IT-ENV-01", "IT environment", "Describe the IT environment supporting financial reporting processes."),
            ("IT-APP-01", "Applications in scope", "List business applications in scope for this engagement and their owners."),
            ("IT-INF-01", "Infrastructure", "Summarize hosting/infrastructure platforms relevant to in-scope applications."),
            ("IT-IF-01", "Interfaces", "Identify key interfaces transferring financial data between systems."),
            ("ITGC-ACC-01", "ITGC access", "How are user access rights granted, reviewed, and revoked for in-scope systems?"),
            ("ITGC-CHG-01", "ITGC change", "How are changes to in-scope applications authorized, tested, and migrated?"),
            ("IT-OPS-01", "IT operations", "What monitoring/backup/job controls support reliable processing for in-scope systems?"),
            ("ITDC-01", "IT-dependent controls", "Which automated/IT-dependent controls support financial reporting assertions?"),
            ("DES-01", "Design/implementation understanding", "Document understanding of design and implementation of key ITGCs."),
            ("RISK-01", "Risk observations", "Record IT-related risk observations affecting financial reporting (no scoring)."),
            ("EVD-01", "Evidence requests", "Identify evidence needed to support ITGC/application control understanding."),
        ];
        int order = 10;
        foreach ((string code, string category, string text) in seeds)
        {
            db.AuditQuestions.Add(AuditQuestion.Create(
                engagementId, category, text, AuditQuestionResponseType.Text, required: true, sortOrder: order,
                questionCode: code));
            order += 10;
        }
    }

    private async Task<AuditEngagement> LoadEngagement(Guid id, CancellationToken ct) =>
        await db.AuditEngagements.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Audit engagement not found.");

    private async Task<AuditQuestion> LoadQuestion(Guid engagementId, Guid questionId, CancellationToken ct) =>
        await db.AuditQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.AuditEngagementId == engagementId, ct)
        ?? throw new InvalidOperationException("Audit question not found.");

    private async Task<Finding> LoadFinding(Guid id, CancellationToken ct) =>
        await db.Findings.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Finding not found.");

    private static AuditEngagementDto MapEngagement(AuditEngagement x) => new(
        x.Id, x.AuditNumber, x.Title, x.AuditType.ToString(), x.Objective, x.ScopeSummary,
        x.LeadAuditorUserId, x.OwnerUserId, x.StartDate, x.EndDate, x.Status.ToString(),
        x.CreatedAtUtc, x.UpdatedAtUtc, x.ClosedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static AuditQuestionDto MapQuestion(AuditQuestion x) => new(
        x.Id, x.AuditEngagementId, x.QuestionCode, x.Category, x.QuestionText, x.FrameworkRequirementId,
        x.InternalControlId, x.ResponseType.ToString(), x.Required, x.SortOrder, x.Status.ToString(),
        x.Response, x.RespondedByUserId, x.RespondedAtUtc, x.ReviewerNotes);

    private static FindingDto MapFinding(Finding x) => new(
        x.Id, x.FindingNumber, x.AuditEngagementId, x.InternalControlId, x.Title, x.Description,
        x.Severity.ToString(), x.Status.ToString(), x.OwnerUserId, x.DueAtUtc, x.AcceptedRiskReason,
        x.ExceptionReference, x.CreatedAtUtc, x.UpdatedAtUtc, x.ClosedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static ManagementResponseDto MapResponse(ManagementResponse x) => new(
        x.Id, x.FindingId, x.ResponseText, x.RespondedByUserId, x.RespondedAtUtc, x.TargetDate, x.ManagementOwnerUserId);

    private static CorrectiveActionDto MapCapa(CorrectiveAction x, DateTimeOffset now) => new(
        x.Id, x.ActionNumber, x.FindingId, x.Title, x.Description, x.OwnerUserId, x.DueAtUtc, x.Status.ToString(),
        x.IsMandatory, x.IsOverdue(now), x.CompletedAtUtc, x.VerifiedByUserId, x.VerifiedAtUtc, x.VerificationNotes,
        x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static EvidenceRequestDto MapEvidenceRequest(EvidenceRequest x, DateTimeOffset now) => new(
        x.Id, x.AuditEngagementId, x.AuditQuestionId, x.InternalControlId, x.Title, x.Description,
        x.RequestedFromUserId, x.DueAtUtc, x.Status.ToString(), x.EvidenceId, x.CreatedByUserId, x.CreatedAtUtc,
        x.FulfilledAtUtc, x.Notes,
        x.DueAtUtc is DateTimeOffset due && due < now
            && x.Status is EvidenceRequestStatus.Requested or EvidenceRequestStatus.InProgress);
}
