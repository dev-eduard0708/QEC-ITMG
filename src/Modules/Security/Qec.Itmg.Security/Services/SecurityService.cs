using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Contracts.Security;
using Qec.Itmg.Security.Domain;
using Qec.Itmg.Security.Persistence;

namespace Qec.Itmg.Security.Services;

public sealed record VulnerabilityDto(
    Guid Id, string VulnerabilityNumber, string Title, string? Description, Guid ConfigurationItemId,
    string Source, string? ExternalReference, string Severity, DateTimeOffset DetectedAtUtc, DateTimeOffset? DueAtUtc,
    string Status, Guid? OwnerUserId, string? ResolutionSummary, string? AcceptedRiskReason, Guid? ExceptionId,
    DateTimeOffset? ResolvedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion,
    bool IsOverdue);

public sealed record VulnerabilityListResult(IReadOnlyList<VulnerabilityDto> Items, int TotalCount, int Page, int PageSize);

public sealed record RemediationLinkDto(
    Guid Id, Guid VulnerabilityId, string LinkType, Guid TargetId, string? Notes, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc);

public sealed record RiskDto(
    Guid Id, string RiskNumber, string Title, string Description, string Category, Guid OwnerUserId,
    Guid? ConfigurationItemId, Guid? BusinessServiceId, Guid? InternalControlId, string Status,
    int Likelihood, int Impact, int InherentScore, int? ResidualLikelihood, int? ResidualImpact, int? ResidualScore,
    string Treatment, string? TreatmentPlan, DateOnly? TargetDate,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ClosedAtUtc, string RowVersion);

public sealed record RiskListResult(IReadOnlyList<RiskDto> Items, int TotalCount, int Page, int PageSize);

public sealed record PolicyExceptionDto(
    Guid Id, string ExceptionNumber, string Title, string Reason, Guid? ManagedDocumentId, Guid? InternalControlId,
    Guid? RiskId, Guid? ConfigurationItemId, Guid RequestedByUserId, Guid? OwnerUserId, Guid? ApprovedByUserId,
    DateTimeOffset StartAtUtc, DateTimeOffset ExpiresAtUtc, string Status, string? CompensatingControls,
    string? RejectionReason, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion,
    bool IsExpired, int? DaysToExpiry);

public sealed record PenetrationTestDto(
    Guid Id, string PentestNumber, string Title, string? Provider, string ScopeSummary,
    DateOnly? StartDate, DateOnly? EndDate, string Status, Guid? ReportEvidenceId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PentestFindingDto(
    Guid Id, Guid PenetrationTestId, string Title, string Description, string Severity,
    Guid? ConfigurationItemId, string Status, Guid? VulnerabilityId, Guid? AuditFindingId, Guid? EvidenceId,
    DateTimeOffset CreatedAtUtc);

public sealed record AwarenessCampaignDto(
    Guid Id, string Title, string? Description, DateTimeOffset StartsAtUtc, DateTimeOffset? DueAtUtc,
    string Status, Guid OwnerUserId, DateTimeOffset CreatedAtUtc,
    int AssignedCount, int CompletedCount, int OutstandingCount, int OverdueCount);

public sealed record AwarenessCompletionDto(
    Guid Id, Guid CampaignId, Guid UserId, string Status, DateTimeOffset? CompletedAtUtc, Guid? EvidenceId, string? Notes);

public sealed record SecurityDashboardCounts(
    int OpenVulnerabilities,
    int CriticalHighVulnerabilities,
    int OverdueRemediation,
    int OpenSecurityIncidents,
    int OpenExceptions,
    int ExpiringExceptions,
    int OpenRisks,
    int HighResidualRisks,
    int PentestOpenFindings,
    int AwarenessOutstanding,
    string Note);

internal static class SecAudit
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

public sealed class SecurityService(
    SecurityDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction,
    IVulnerabilityScannerIngestClient scanner)
{
    public const string VulnSequence = "vuln";
    public const string VulnPrefix = "VUL";
    public const string RiskSequence = "risk";
    public const string RiskPrefix = "RSK";
    public const string ExceptionSequence = "exception";
    public const string ExceptionPrefix = "EXC";
    public const string PentestSequence = "pentest";
    public const string PentestPrefix = "PEN";

    // ——— Vulnerabilities ———

    public async Task<VulnerabilityListResult> ListVulnerabilitiesAsync(
        int page, int pageSize, string? search, VulnerabilityStatus? status, VulnerabilitySeverity? severity,
        bool overdueOnly, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        DateTimeOffset now = clock.UtcNow;
        IQueryable<Vulnerability> q = db.Vulnerabilities.AsNoTracking();
        if (status is VulnerabilityStatus s) q = q.Where(x => x.Status == s);
        if (severity is VulnerabilitySeverity sev) q = q.Where(x => x.Severity == sev);
        if (overdueOnly)
            q = q.Where(x => x.DueAtUtc != null && x.DueAtUtc < now
                && (x.Status == VulnerabilityStatus.Open || x.Status == VulnerabilityStatus.InRemediation));
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.VulnerabilityNumber.Contains(term) || x.Source.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<Vulnerability> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(x => MapVuln(x, now)).ToList(), total, page, pageSize);
    }

    public async Task<VulnerabilityDto?> GetVulnerabilityAsync(Guid id, CancellationToken ct)
    {
        Vulnerability? item = await db.Vulnerabilities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapVuln(item, clock.UtcNow);
    }

    public async Task<VulnerabilityDto> CreateVulnerabilityAsync(
        string title, Guid configurationItemId, string source, VulnerabilitySeverity severity,
        DateTimeOffset? detectedAtUtc, string? description, string? externalReference,
        DateTimeOffset? dueAtUtc, Guid? ownerUserId, CancellationToken ct)
    {
        VulnerabilityDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(VulnSequence, VulnPrefix, innerCt);
            Vulnerability entity = Vulnerability.Create(
                number, title, configurationItemId, source, severity, detectedAtUtc ?? clock.UtcNow, clock.UtcNow,
                description, externalReference, dueAtUtc, ownerUserId);
            db.Vulnerabilities.Add(entity);
            await businessAudit.AppendAsync(SecAudit.Created(AuditAggregateType.Vulnerability, entity.Id, entity.VulnerabilityNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapVuln(entity, clock.UtcNow);
        }, ct);
        return created!;
    }

    public async Task<VulnerabilityDto> TransitionVulnerabilityAsync(
        Guid id, VulnerabilityStatus next, string? resolutionSummary, string? acceptedRiskReason,
        Guid? exceptionId, CancellationToken ct)
    {
        Vulnerability entity = await LoadVuln(id, ct);
        if (next == VulnerabilityStatus.AcceptedRisk)
        {
            PolicyException? ex = await db.PolicyExceptions.FirstOrDefaultAsync(x => x.Id == exceptionId, ct)
                ?? throw new InvalidOperationException("Approved exception required.");
            if (ex.Status != PolicyExceptionStatus.Approved || ex.IsExpired(clock.UtcNow))
                throw new InvalidOperationException("Exception must be Approved and not expired.");
        }

        bool hasLink = await db.VulnerabilityRemediationLinks.AnyAsync(x => x.VulnerabilityId == id, ct);
        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow, resolutionSummary, acceptedRiskReason, exceptionId, hasLink);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.Vulnerability, entity.Id, entity.VulnerabilityNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged, acceptedRiskReason ?? resolutionSummary), ct);
        return MapVuln(entity, clock.UtcNow);
    }

    public async Task<RemediationLinkDto> AddRemediationLinkAsync(
        Guid vulnerabilityId, VulnerabilityRemediationLinkType linkType, Guid targetId, Guid actorUserId,
        string? notes, CancellationToken ct)
    {
        Vulnerability entity = await LoadVuln(vulnerabilityId, ct);
        bool exists = await db.VulnerabilityRemediationLinks.AnyAsync(
            x => x.VulnerabilityId == vulnerabilityId && x.LinkType == linkType && x.TargetId == targetId, ct);
        if (exists) throw new InvalidOperationException("Remediation link already exists.");
        VulnerabilityRemediationLink link = VulnerabilityRemediationLink.Create(
            vulnerabilityId, linkType, targetId, actorUserId, clock.UtcNow, notes);
        db.VulnerabilityRemediationLinks.Add(link);
        if (entity.Status == VulnerabilityStatus.Open)
            entity.Transition(VulnerabilityStatus.InRemediation, clock.UtcNow, hasRemediationLink: true);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.Vulnerability, vulnerabilityId, entity.VulnerabilityNumber, "RemediationLink",
                null, $"{linkType}:{targetId}", BusinessAuditAction.Linked), ct);
        return new(link.Id, link.VulnerabilityId, link.LinkType.ToString(), link.TargetId, link.Notes, link.CreatedByUserId, link.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<RemediationLinkDto>> ListRemediationLinksAsync(Guid vulnerabilityId, CancellationToken ct) =>
        await db.VulnerabilityRemediationLinks.AsNoTracking()
            .Where(x => x.VulnerabilityId == vulnerabilityId)
            .Select(x => new RemediationLinkDto(x.Id, x.VulnerabilityId, x.LinkType.ToString(), x.TargetId, x.Notes, x.CreatedByUserId, x.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<int> IngestFromScannerStubAsync(CancellationToken ct)
    {
        IReadOnlyList<ScannerVulnerabilityIngestItem> items = await scanner.FetchAsync(ct);
        int created = 0;
        foreach (ScannerVulnerabilityIngestItem item in items)
        {
            if (await IngestScannerItemAsync(item, ct))
                created++;
        }

        return created;
    }

    /// <summary>Idempotent single-finding ingest keyed by ExternalReference.</summary>
    public async Task<bool> IngestScannerItemAsync(ScannerVulnerabilityIngestItem item, CancellationToken ct)
    {
        bool exists = await db.Vulnerabilities.AnyAsync(x => x.ExternalReference == item.ExternalReference, ct);
        if (exists) return false;
        if (!Enum.TryParse(item.Severity, true, out VulnerabilitySeverity severity))
            severity = VulnerabilitySeverity.Medium;
        await CreateVulnerabilityAsync(
            item.Title, item.ConfigurationItemId, item.Source, severity, item.DetectedAtUtc,
            item.Description, item.ExternalReference, item.DueAtUtc, null, ct);
        return true;
    }

    // ——— Risks ———

    public async Task<RiskListResult> ListRisksAsync(int page, int pageSize, string? search, RiskStatus? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Risk> q = db.Risks.AsNoTracking();
        if (status is RiskStatus s) q = q.Where(x => x.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.RiskNumber.Contains(term) || x.Category.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<Risk> items = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(MapRisk).ToList(), total, page, pageSize);
    }

    public async Task<RiskDto?> GetRiskAsync(Guid id, CancellationToken ct)
    {
        Risk? item = await db.Risks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapRisk(item);
    }

    public async Task<RiskDto> CreateRiskAsync(
        string title, string description, string category, Guid ownerUserId, int likelihood, int impact,
        RiskTreatment treatment, Guid? configurationItemId, Guid? businessServiceId, Guid? internalControlId,
        string? treatmentPlan, DateOnly? targetDate, CancellationToken ct)
    {
        RiskDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(RiskSequence, RiskPrefix, innerCt);
            Risk entity = Risk.Create(
                number, title, description, category, ownerUserId, likelihood, impact, treatment, clock.UtcNow,
                configurationItemId, businessServiceId, internalControlId, treatmentPlan, targetDate);
            db.Risks.Add(entity);
            await businessAudit.AppendAsync(SecAudit.Created(AuditAggregateType.Risk, entity.Id, entity.RiskNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapRisk(entity);
        }, ct);
        return created!;
    }

    public async Task<RiskDto> UpdateRiskAsync(
        Guid id, int likelihood, int impact, int? residualLikelihood, int? residualImpact,
        RiskTreatment treatment, string? treatmentPlan, DateOnly? targetDate, CancellationToken ct)
    {
        Risk entity = await LoadRisk(id, ct);
        entity.UpdateAnalysis(likelihood, impact, residualLikelihood, residualImpact, treatment, treatmentPlan, targetDate, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return MapRisk(entity);
    }

    public async Task<RiskDto> TransitionRiskAsync(Guid id, RiskStatus next, CancellationToken ct)
    {
        Risk entity = await LoadRisk(id, ct);
        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.Risk, entity.Id, entity.RiskNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapRisk(entity);
    }

    public async Task AddRiskLinkAsync(Guid riskId, string targetType, Guid targetId, Guid actorUserId, CancellationToken ct)
    {
        _ = await LoadRisk(riskId, ct);
        bool exists = await db.RiskLinks.AnyAsync(
            x => x.RiskId == riskId && x.TargetType == targetType && x.TargetId == targetId, ct);
        if (exists) throw new InvalidOperationException("Risk link already exists.");
        db.RiskLinks.Add(RiskLink.Create(riskId, targetType, targetId, actorUserId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    // ——— Exceptions ———

    public async Task<IReadOnlyList<PolicyExceptionDto>> ListExceptionsAsync(PolicyExceptionStatus? status, CancellationToken ct)
    {
        IQueryable<PolicyException> q = db.PolicyExceptions.AsNoTracking();
        if (status is PolicyExceptionStatus s) q = q.Where(x => x.Status == s);
        DateTimeOffset now = clock.UtcNow;
        return (await q.OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct))
            .Select(x => MapException(x, now)).ToList();
    }

    public async Task<PolicyExceptionDto?> GetExceptionAsync(Guid id, CancellationToken ct)
    {
        PolicyException? item = await db.PolicyExceptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapException(item, clock.UtcNow);
    }

    public async Task<PolicyExceptionDto> CreateExceptionAsync(
        string title, string reason, Guid requestedByUserId, DateTimeOffset startAtUtc, DateTimeOffset expiresAtUtc,
        Guid? managedDocumentId, Guid? internalControlId, Guid? riskId, Guid? configurationItemId,
        Guid? ownerUserId, string? compensatingControls, CancellationToken ct)
    {
        PolicyExceptionDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(ExceptionSequence, ExceptionPrefix, innerCt);
            PolicyException entity = PolicyException.Create(
                number, title, reason, requestedByUserId, startAtUtc, expiresAtUtc, clock.UtcNow,
                managedDocumentId, internalControlId, riskId, configurationItemId, ownerUserId, compensatingControls);
            db.PolicyExceptions.Add(entity);
            await businessAudit.AppendAsync(SecAudit.Created(AuditAggregateType.PolicyException, entity.Id, entity.ExceptionNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapException(entity, clock.UtcNow);
        }, ct);
        return created!;
    }

    public async Task<PolicyExceptionDto> SubmitExceptionAsync(Guid id, CancellationToken ct)
    {
        PolicyException entity = await LoadException(id, ct);
        string old = entity.Status.ToString();
        entity.SubmitForApproval(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.PolicyException, entity.Id, entity.ExceptionNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapException(entity, clock.UtcNow);
    }

    public async Task<PolicyExceptionDto> ApproveExceptionAsync(Guid id, Guid approverUserId, CancellationToken ct)
    {
        PolicyException entity = await LoadException(id, ct);
        string old = entity.Status.ToString();
        entity.Approve(approverUserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.PolicyException, entity.Id, entity.ExceptionNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapException(entity, clock.UtcNow);
    }

    public async Task<PolicyExceptionDto> RejectExceptionAsync(Guid id, Guid approverUserId, string reason, CancellationToken ct)
    {
        PolicyException entity = await LoadException(id, ct);
        string old = entity.Status.ToString();
        entity.Reject(approverUserId, reason, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.PolicyException, entity.Id, entity.ExceptionNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged, reason), ct);
        return MapException(entity, clock.UtcNow);
    }

    public async Task<PolicyExceptionDto> CloseExceptionAsync(Guid id, CancellationToken ct)
    {
        PolicyException entity = await LoadException(id, ct);
        string old = entity.Status.ToString();
        entity.Close(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.PolicyException, entity.Id, entity.ExceptionNumber, "Status", old, entity.Status.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapException(entity, clock.UtcNow);
    }

    public async Task<int> MarkExpiredExceptionsJobAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<PolicyException> items = await db.PolicyExceptions
            .Where(x => x.Status == PolicyExceptionStatus.Approved && x.ExpiresAtUtc < now)
            .ToListAsync(ct);
        foreach (PolicyException item in items)
        {
            string old = item.Status.ToString();
            item.MarkExpired(now);
            await businessAudit.AppendAsync(
                SecAudit.Field(AuditAggregateType.PolicyException, item.Id, item.ExceptionNumber, "Status", old, item.Status.ToString(),
                    BusinessAuditAction.StatusChanged), ct);
        }

        if (items.Count > 0) await db.SaveChangesAsync(ct);
        return items.Count;
    }

    public async Task<IReadOnlyList<PolicyException>> GetExpiringExceptionCandidatesAsync(CancellationToken ct) =>
        await db.PolicyExceptions
            .Where(x => x.Status == PolicyExceptionStatus.Approved)
            .ToListAsync(ct);

    public async Task<bool> HasExceptionNotificationAsync(Guid exceptionId, string eventKey, CancellationToken ct) =>
        await db.ExceptionExpiryNotificationLogs.AnyAsync(x => x.ExceptionId == exceptionId && x.EventKey == eventKey, ct);

    public async Task RecordExceptionNotificationAsync(Guid exceptionId, string eventKey, CancellationToken ct)
    {
        db.ExceptionExpiryNotificationLogs.Add(ExceptionExpiryNotificationLog.Create(exceptionId, eventKey, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    // ——— Pentest ———

    public async Task<IReadOnlyList<PenetrationTestDto>> ListPentestsAsync(CancellationToken ct) =>
        (await db.PenetrationTests.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc).Take(100).ToListAsync(ct))
        .Select(MapPentest).ToList();

    public async Task<PenetrationTestDto?> GetPentestAsync(Guid id, CancellationToken ct)
    {
        PenetrationTest? item = await db.PenetrationTests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : MapPentest(item);
    }

    public async Task<PenetrationTestDto> CreatePentestAsync(
        string title, string scopeSummary, string? provider, DateOnly? startDate, DateOnly? endDate, CancellationToken ct)
    {
        PenetrationTestDto? created = null;
        await sharedDbTransaction.ExecuteAsync(async innerCt =>
        {
            string number = await numbers.NextAsync(PentestSequence, PentestPrefix, innerCt);
            PenetrationTest entity = PenetrationTest.Create(number, title, scopeSummary, clock.UtcNow, provider, startDate, endDate);
            db.PenetrationTests.Add(entity);
            await businessAudit.AppendAsync(SecAudit.Created(AuditAggregateType.PenetrationTest, entity.Id, entity.PentestNumber), innerCt);
            await db.SaveChangesAsync(innerCt);
            created = MapPentest(entity);
        }, ct);
        return created!;
    }

    public async Task<PenetrationTestDto> TransitionPentestAsync(Guid id, PenetrationTestStatus next, CancellationToken ct)
    {
        PenetrationTest entity = await db.PenetrationTests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Penetration test not found.");
        string old = entity.Status.ToString();
        entity.Transition(next, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(
            SecAudit.Field(AuditAggregateType.PenetrationTest, entity.Id, entity.PentestNumber, "Status", old, next.ToString(),
                BusinessAuditAction.StatusChanged), ct);
        return MapPentest(entity);
    }

    public async Task<IReadOnlyList<PentestFindingDto>> ListPentestFindingsAsync(Guid pentestId, CancellationToken ct) =>
        (await db.PentestFindings.AsNoTracking().Where(x => x.PenetrationTestId == pentestId).ToListAsync(ct))
        .Select(MapPentestFinding).ToList();

    public async Task<PentestFindingDto> AddPentestFindingAsync(
        Guid pentestId, string title, string description, VulnerabilitySeverity severity, Guid? configurationItemId, CancellationToken ct)
    {
        _ = await GetPentestAsync(pentestId, ct) ?? throw new InvalidOperationException("Penetration test not found.");
        PentestFinding finding = PentestFinding.Create(pentestId, title, description, severity, clock.UtcNow, configurationItemId);
        db.PentestFindings.Add(finding);
        await db.SaveChangesAsync(ct);
        return MapPentestFinding(finding);
    }

    public async Task<PentestFindingDto> LinkPentestFindingAsync(
        Guid findingId, Guid? vulnerabilityId, Guid? auditFindingId, Guid? evidenceId, CancellationToken ct)
    {
        PentestFinding finding = await db.PentestFindings.FirstOrDefaultAsync(x => x.Id == findingId, ct)
            ?? throw new InvalidOperationException("Pentest finding not found.");
        finding.Link(vulnerabilityId, auditFindingId, evidenceId);
        await db.SaveChangesAsync(ct);
        return MapPentestFinding(finding);
    }

    // ——— Awareness ———

    public async Task<IReadOnlyList<AwarenessCampaignDto>> ListCampaignsAsync(CancellationToken ct)
    {
        List<AwarenessCampaign> campaigns = await db.AwarenessCampaigns.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
        List<AwarenessCompletion> completions = await db.AwarenessCompletions.AsNoTracking().ToListAsync(ct);
        DateTimeOffset now = clock.UtcNow;
        return campaigns.Select(c => MapCampaign(c, completions.Where(x => x.CampaignId == c.Id).ToList(), now)).ToList();
    }

    public async Task<AwarenessCampaignDto> CreateCampaignAsync(
        string title, Guid ownerUserId, DateTimeOffset startsAtUtc, string? description, DateTimeOffset? dueAtUtc, CancellationToken ct)
    {
        AwarenessCampaign entity = AwarenessCampaign.Create(title, ownerUserId, startsAtUtc, clock.UtcNow, description, dueAtUtc);
        db.AwarenessCampaigns.Add(entity);
        await db.SaveChangesAsync(ct);
        return MapCampaign(entity, [], clock.UtcNow);
    }

    public async Task<AwarenessCampaignDto> OpenCampaignAsync(Guid id, CancellationToken ct)
    {
        AwarenessCampaign entity = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        entity.Open();
        await db.SaveChangesAsync(ct);
        List<AwarenessCompletion> completions = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        return MapCampaign(entity, completions, clock.UtcNow);
    }

    public async Task<AwarenessCompletionDto> AssignCompletionAsync(Guid campaignId, Guid userId, CancellationToken ct)
    {
        _ = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        bool exists = await db.AwarenessCompletions.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId, ct);
        if (exists) throw new InvalidOperationException("User already assigned.");
        AwarenessCompletion completion = AwarenessCompletion.Assign(campaignId, userId);
        db.AwarenessCompletions.Add(completion);
        await db.SaveChangesAsync(ct);
        return MapCompletion(completion);
    }

    public async Task<AwarenessCompletionDto> CompleteAwarenessAsync(
        Guid campaignId, Guid userId, Guid? evidenceId, string? notes, CancellationToken ct)
    {
        AwarenessCompletion completion = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Assignment not found.");
        completion.Complete(clock.UtcNow, evidenceId, notes);
        await db.SaveChangesAsync(ct);
        return MapCompletion(completion);
    }

    public async Task<IReadOnlyList<AwarenessCompletionDto>> ListCompletionsAsync(Guid campaignId, CancellationToken ct) =>
        (await db.AwarenessCompletions.AsNoTracking().Where(x => x.CampaignId == campaignId).ToListAsync(ct))
        .Select(MapCompletion).ToList();

    // ——— Dashboard ———

    public async Task<SecurityDashboardCounts> GetDashboardCountsAsync(int openSecurityIncidents, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        int openVulns = await db.Vulnerabilities.AsNoTracking().CountAsync(
            x => x.Status == VulnerabilityStatus.Open || x.Status == VulnerabilityStatus.InRemediation, ct);
        int criticalHigh = await db.Vulnerabilities.AsNoTracking().CountAsync(
            x => (x.Status == VulnerabilityStatus.Open || x.Status == VulnerabilityStatus.InRemediation)
                && (x.Severity == VulnerabilitySeverity.Critical || x.Severity == VulnerabilitySeverity.High), ct);
        int overdue = await db.Vulnerabilities.AsNoTracking().CountAsync(
            x => x.DueAtUtc != null && x.DueAtUtc < now
                && (x.Status == VulnerabilityStatus.Open || x.Status == VulnerabilityStatus.InRemediation), ct);
        int openExceptions = await db.PolicyExceptions.AsNoTracking().CountAsync(
            x => x.Status == PolicyExceptionStatus.Approved || x.Status == PolicyExceptionStatus.PendingApproval, ct);
        DateTimeOffset soon = now.AddDays(30);
        int expiring = await db.PolicyExceptions.AsNoTracking().CountAsync(
            x => x.Status == PolicyExceptionStatus.Approved && x.ExpiresAtUtc >= now && x.ExpiresAtUtc <= soon, ct);
        int openRisks = await db.Risks.AsNoTracking().CountAsync(x => x.Status != RiskStatus.Closed, ct);
        int highResidual = await db.Risks.AsNoTracking().CountAsync(
            x => x.Status != RiskStatus.Closed && x.ResidualScore != null && x.ResidualScore >= 15, ct);
        int pentestOpen = await db.PentestFindings.AsNoTracking().CountAsync(x => x.Status == PentestFindingStatus.Open, ct);

        List<AwarenessCampaign> openCampaigns = await db.AwarenessCampaigns.AsNoTracking()
            .Where(x => x.Status == AwarenessCampaignStatus.Open).ToListAsync(ct);
        List<Guid> campaignIds = openCampaigns.Select(x => x.Id).ToList();
        List<AwarenessCompletion> completions = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => campaignIds.Contains(x.CampaignId)).ToListAsync(ct);
        int outstanding = completions.Count(x => x.Status == AwarenessCompletionStatus.Assigned);

        return new(
            openVulns, criticalHigh, overdue, openSecurityIncidents, openExceptions, expiring,
            openRisks, highResidual, pentestOpen, outstanding,
            "Counts only. Not a cybersecurity compliance score.");
    }

    private async Task<Vulnerability> LoadVuln(Guid id, CancellationToken ct) =>
        await db.Vulnerabilities.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Vulnerability not found.");

    private async Task<Risk> LoadRisk(Guid id, CancellationToken ct) =>
        await db.Risks.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Risk not found.");

    private async Task<PolicyException> LoadException(Guid id, CancellationToken ct) =>
        await db.PolicyExceptions.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Exception not found.");

    private static VulnerabilityDto MapVuln(Vulnerability x, DateTimeOffset now) => new(
        x.Id, x.VulnerabilityNumber, x.Title, x.Description, x.ConfigurationItemId, x.Source, x.ExternalReference,
        x.Severity.ToString(), x.DetectedAtUtc, x.DueAtUtc, x.Status.ToString(), x.OwnerUserId, x.ResolutionSummary,
        x.AcceptedRiskReason, x.ExceptionId, x.ResolvedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc,
        Convert.ToBase64String(x.RowVersion), x.IsOverdue(now));

    private static RiskDto MapRisk(Risk x) => new(
        x.Id, x.RiskNumber, x.Title, x.Description, x.Category, x.OwnerUserId, x.ConfigurationItemId,
        x.BusinessServiceId, x.InternalControlId, x.Status.ToString(), x.Likelihood, x.Impact, x.InherentScore,
        x.ResidualLikelihood, x.ResidualImpact, x.ResidualScore, x.Treatment.ToString(), x.TreatmentPlan,
        x.TargetDate, x.CreatedAtUtc, x.UpdatedAtUtc, x.ClosedAtUtc, Convert.ToBase64String(x.RowVersion));

    private static PolicyExceptionDto MapException(PolicyException x, DateTimeOffset now) => new(
        x.Id, x.ExceptionNumber, x.Title, x.Reason, x.ManagedDocumentId, x.InternalControlId, x.RiskId,
        x.ConfigurationItemId, x.RequestedByUserId, x.OwnerUserId, x.ApprovedByUserId, x.StartAtUtc, x.ExpiresAtUtc,
        x.Status.ToString(), x.CompensatingControls, x.RejectionReason, x.CreatedAtUtc, x.UpdatedAtUtc,
        Convert.ToBase64String(x.RowVersion), x.IsExpired(now), x.DaysToExpiry(now));

    private static PenetrationTestDto MapPentest(PenetrationTest x) => new(
        x.Id, x.PentestNumber, x.Title, x.Provider, x.ScopeSummary, x.StartDate, x.EndDate, x.Status.ToString(),
        x.ReportEvidenceId, x.CreatedAtUtc, x.UpdatedAtUtc);

    private static PentestFindingDto MapPentestFinding(PentestFinding x) => new(
        x.Id, x.PenetrationTestId, x.Title, x.Description, x.Severity.ToString(), x.ConfigurationItemId,
        x.Status.ToString(), x.VulnerabilityId, x.AuditFindingId, x.EvidenceId, x.CreatedAtUtc);

    private static AwarenessCampaignDto MapCampaign(
        AwarenessCampaign c, IReadOnlyList<AwarenessCompletion> completions, DateTimeOffset now)
    {
        int assigned = completions.Count;
        int completed = completions.Count(x => x.Status is AwarenessCompletionStatus.Completed or AwarenessCompletionStatus.Exempt);
        int outstanding = completions.Count(x => x.Status == AwarenessCompletionStatus.Assigned);
        int overdue = c.DueAtUtc is DateTimeOffset due && due < now ? outstanding : 0;
        return new(c.Id, c.Title, c.Description, c.StartsAtUtc, c.DueAtUtc, c.Status.ToString(), c.OwnerUserId,
            c.CreatedAtUtc, assigned, completed, outstanding, overdue);
    }

    private static AwarenessCompletionDto MapCompletion(AwarenessCompletion x) => new(
        x.Id, x.CampaignId, x.UserId, x.Status.ToString(), x.CompletedAtUtc, x.EvidenceId, x.Notes);
}
