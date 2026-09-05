using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Persistence;

namespace Qec.Itmg.DocumentManagement.Services;

public sealed record EmployeePolicyItemDto(
    Guid AssignmentId,
    Guid ManagedDocumentId,
    Guid DocumentVersionId,
    string DocumentNumber,
    string Title,
    int VersionNumber,
    string? Summary,
    string? ContentText,
    Guid? AttachmentId,
    string Classification,
    DateTimeOffset? EffectiveDate,
    Guid OwnerUserId,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? DueAtUtc,
    bool IsRequired,
    string Status,
    DateTimeOffset? AcknowledgedAtUtc,
    bool IsOverdue);

public sealed record EmployeePolicySummaryDto(
    int Required,
    int Acknowledged,
    int Outstanding,
    int Overdue);

public sealed record PolicyAssignmentResultDto(
    Guid ManagedDocumentId,
    Guid DocumentVersionId,
    int AssignmentCount,
    string Scope);

public sealed record PolicyVersionAckStatsDto(
    Guid ManagedDocumentId,
    Guid DocumentVersionId,
    string DocumentNumber,
    string Title,
    int VersionNumber,
    int Assigned,
    int Acknowledged,
    int Outstanding,
    int Overdue);

public sealed record PolicyEmployeeAckRowDto(
    Guid UserId,
    string? DisplayName,
    string? Upn,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? DueAtUtc,
    string Status,
    DateTimeOffset? AcknowledgedAtUtc,
    int VersionNumber);

public sealed record PolicyAckReminderCandidate(
    Guid AssignmentId,
    Guid ManagedDocumentId,
    Guid DocumentVersionId,
    Guid UserId,
    string DocumentNumber,
    string Title,
    int VersionNumber,
    DateTimeOffset? DueAtUtc,
    string ReminderKind);

public sealed class PolicyAcknowledgementService(
    DocumentManagementDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    IActiveEmployeeLookup employees)
{
    public const string ReminderDue7 = "due_7";
    public const string ReminderDue1 = "due_1";
    public const string ReminderOverdue = "overdue";

    public async Task<PolicyAssignmentResultDto> AssignPublishedVersionAsync(
        Guid documentId,
        PolicyAssignmentScope scope,
        Guid assignedByUserId,
        IReadOnlyList<Guid>? specificUserIds,
        DateTimeOffset? dueAtUtc,
        bool isRequired,
        CancellationToken ct)
    {
        ManagedDocument doc = await db.ManagedDocuments.FirstOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");
        if (doc.DocumentType != DocumentType.Policy)
            throw new InvalidOperationException("Only policies can be assigned for acknowledgement.");
        if (doc.Status != DocumentStatus.Published || doc.CurrentVersionId is null)
            throw new InvalidOperationException("Only published policies with a current version can be assigned.");
        if (!doc.RequiresAcknowledgement)
            throw new InvalidOperationException("Policy does not require acknowledgement.");

        DocumentVersion version = await db.DocumentVersions.FirstAsync(x => x.Id == doc.CurrentVersionId, ct);
        if (version.PublishedAtUtc is null)
            throw new InvalidOperationException("Current version is not published.");

        List<PolicyAssignment> created = [];
        if (scope == PolicyAssignmentScope.AllEmployees)
        {
            bool exists = await db.PolicyAssignments.AnyAsync(
                x => x.DocumentVersionId == version.Id
                    && x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                    && x.UserId == null, ct);
            if (!exists)
            {
                PolicyAssignment assignment = PolicyAssignment.Create(
                    doc.Id, version.Id, PolicyAssignmentScope.AllEmployees, assignedByUserId, clock.UtcNow,
                    dueAtUtc: dueAtUtc, isRequired: isRequired);
                db.PolicyAssignments.Add(assignment);
                created.Add(assignment);
            }
        }
        else
        {
            IReadOnlyList<Guid> userIds = specificUserIds ?? [];
            if (userIds.Count == 0)
                throw new ArgumentException("At least one user is required.", nameof(specificUserIds));
            foreach (Guid userId in userIds.Distinct())
            {
                bool exists = await db.PolicyAssignments.AnyAsync(
                    x => x.DocumentVersionId == version.Id
                        && x.AssignmentScope == PolicyAssignmentScope.SpecificUser
                        && x.UserId == userId, ct);
                if (exists) continue;
                PolicyAssignment assignment = PolicyAssignment.Create(
                    doc.Id, version.Id, PolicyAssignmentScope.SpecificUser, assignedByUserId, clock.UtcNow,
                    userId: userId, dueAtUtc: dueAtUtc, isRequired: isRequired);
                db.PolicyAssignments.Add(assignment);
                created.Add(assignment);
            }
        }

        if (created.Count > 0)
        {
            await businessAudit.AppendAsync(new BusinessAuditEntry
            {
                AggregateType = AuditAggregateType.Document,
                AggregateId = doc.Id,
                BusinessNumber = doc.DocumentNumber,
                Action = BusinessAuditAction.Assigned,
                FieldName = "PolicyAssigned",
                NewValue = $"{scope}:{version.VersionNumber}:{created.Count}",
                Source = AuditSource.Api,
            }, ct);
            await db.SaveChangesAsync(ct);
        }

        int total = await CountAssignedUsersAsync(version.Id, ct);
        return new PolicyAssignmentResultDto(doc.Id, version.Id, total, scope.ToString());
    }

    public async Task<IReadOnlyList<Guid>> ResolveAssigneeUserIdsAsync(Guid documentVersionId, CancellationToken ct)
    {
        List<PolicyAssignment> assignments = await db.PolicyAssignments.AsNoTracking()
            .Where(x => x.DocumentVersionId == documentVersionId && x.IsRequired)
            .ToListAsync(ct);
        HashSet<Guid> userIds = [];
        bool allEmployees = assignments.Any(x => x.AssignmentScope == PolicyAssignmentScope.AllEmployees);
        if (allEmployees)
        {
            foreach (ActiveEmployeeInfo emp in await employees.ListActiveAsync(ct))
                userIds.Add(emp.Id);
        }

        foreach (PolicyAssignment a in assignments.Where(x => x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId is not null))
            userIds.Add(a.UserId!.Value);

        return userIds.ToList();
    }

    public async Task<EmployeePolicySummaryDto> GetEmployeeSummaryAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<EmployeePolicyItemDto> items = await ListEmployeePoliciesAsync(userId, "all", ct);
        IReadOnlyList<EmployeePolicyItemDto> required = items.Where(x => x.IsRequired).ToList();
        int acknowledged = required.Count(x => x.Status == "Acknowledged");
        int outstanding = required.Count(x => x.Status is "NeedsAcknowledgement" or "Overdue");
        int overdue = required.Count(x => x.Status == "Overdue");
        return new EmployeePolicySummaryDto(required.Count, acknowledged, outstanding, overdue);
    }

    public async Task<IReadOnlyList<EmployeePolicyItemDto>> ListEmployeePoliciesAsync(
        Guid userId, string filter, CancellationToken ct)
    {
        List<PolicyAssignment> assignments = await LoadAssignmentsForUserAsync(userId, ct);
        if (assignments.Count == 0) return [];

        HashSet<Guid> versionIds = assignments.Select(x => x.DocumentVersionId).ToHashSet();
        Dictionary<Guid, DocumentVersion> versions = await db.DocumentVersions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        HashSet<Guid> docIds = assignments.Select(x => x.ManagedDocumentId).ToHashSet();
        Dictionary<Guid, ManagedDocument> docs = await db.ManagedDocuments.AsNoTracking()
            .Where(x => docIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        Dictionary<Guid, PolicyAcknowledgement> acks = await db.PolicyAcknowledgements.AsNoTracking()
            .Where(x => x.UserId == userId && versionIds.Contains(x.DocumentVersionId))
            .ToDictionaryAsync(x => x.DocumentVersionId, ct);

        DateTimeOffset now = clock.UtcNow;
        List<EmployeePolicyItemDto> items = [];
        foreach (PolicyAssignment assignment in assignments
                     .GroupBy(x => x.DocumentVersionId)
                     .Select(g => g.OrderByDescending(x => x.AssignedAtUtc).First()))
        {
            if (!docs.TryGetValue(assignment.ManagedDocumentId, out ManagedDocument? doc)) continue;
            if (!versions.TryGetValue(assignment.DocumentVersionId, out DocumentVersion? version)) continue;
            if (doc.Status is DocumentStatus.Draft or DocumentStatus.InReview or DocumentStatus.Approved or DocumentStatus.Retired)
                continue;
            // Only current published version counts as outstanding/required; history kept via acknowledged older versions
            bool isCurrent = doc.CurrentVersionId == version.Id && doc.Status == DocumentStatus.Published;
            acks.TryGetValue(version.Id, out PolicyAcknowledgement? ack);
            bool acknowledged = ack is not null;
            bool overdue = !acknowledged && assignment.IsRequired && assignment.DueAtUtc is DateTimeOffset due && due < now;
            string status = acknowledged
                ? "Acknowledged"
                : overdue
                    ? "Overdue"
                    : assignment.IsRequired && isCurrent
                        ? "NeedsAcknowledgement"
                        : acknowledged
                            ? "Acknowledged"
                            : "Assigned";

            if (!acknowledged && !isCurrent)
                continue; // unacked historical assignment for superseded version — skip from employee list

            items.Add(new EmployeePolicyItemDto(
                assignment.Id, doc.Id, version.Id, doc.DocumentNumber, doc.Title, version.VersionNumber,
                version.ChangeSummary, version.ContentText, version.AttachmentId, doc.Classification.ToString(),
                doc.EffectiveDate, doc.OwnerUserId, assignment.AssignedAtUtc, assignment.DueAtUtc,
                assignment.IsRequired && isCurrent, status, ack?.AcknowledgedAtUtc, overdue));
        }

        // Also include historical acknowledgements for superseded versions not in current assignments list
        List<PolicyAcknowledgement> historyAcks = await db.PolicyAcknowledgements.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        foreach (PolicyAcknowledgement hist in historyAcks)
        {
            if (items.Any(x => x.DocumentVersionId == hist.DocumentVersionId)) continue;
            if (!docs.ContainsKey(hist.ManagedDocumentId))
            {
                ManagedDocument? d = await db.ManagedDocuments.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == hist.ManagedDocumentId, ct);
                if (d is null) continue;
                docs[d.Id] = d;
            }

            ManagedDocument doc = docs[hist.ManagedDocumentId];
            DocumentVersion? version = await db.DocumentVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == hist.DocumentVersionId, ct);
            if (version is null) continue;
            items.Add(new EmployeePolicyItemDto(
                Guid.Empty, doc.Id, version.Id, hist.PolicyNumberSnapshot ?? doc.DocumentNumber,
                hist.PolicyTitleSnapshot ?? doc.Title, hist.VersionNumber,
                version.ChangeSummary, version.ContentText, version.AttachmentId, doc.Classification.ToString(),
                doc.EffectiveDate, doc.OwnerUserId, hist.AssignedAtUtc ?? hist.AcknowledgedAtUtc, hist.DueAtUtc,
                false, "Acknowledged", hist.AcknowledgedAtUtc, false));
        }

        filter = (filter ?? "outstanding").Trim().ToLowerInvariant();
        return filter switch
        {
            "acknowledged" => items.Where(x => x.Status == "Acknowledged").OrderByDescending(x => x.AcknowledgedAtUtc).ToList(),
            "all" => items.OrderBy(x => x.Status == "Acknowledged").ThenBy(x => x.DueAtUtc).ToList(),
            _ => items.Where(x => x.Status is "NeedsAcknowledgement" or "Overdue")
                .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue).ToList(),
        };
    }

    public async Task<EmployeePolicyItemDto?> GetEmployeePolicyAsync(Guid userId, Guid documentId, CancellationToken ct)
    {
        IReadOnlyList<EmployeePolicyItemDto> all = await ListEmployeePoliciesAsync(userId, "all", ct);
        EmployeePolicyItemDto? current = all
            .Where(x => x.ManagedDocumentId == documentId)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault(x => x.Status is "NeedsAcknowledgement" or "Overdue")
            ?? all.Where(x => x.ManagedDocumentId == documentId)
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefault();
        return current;
    }

    public async Task<PolicyAcknowledgementDto> AcknowledgeAsync(
        Guid documentId,
        Guid userId,
        bool acceptedStatement,
        string? clientIp,
        string? userAgent,
        CancellationToken ct)
    {
        if (!acceptedStatement)
            throw new InvalidOperationException("You must confirm that you have read and understood this policy.");

        ManagedDocument doc = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");
        if (doc.DocumentType != DocumentType.Policy || !doc.RequiresAcknowledgement)
            throw new InvalidOperationException("Document does not require acknowledgement.");
        if (doc.Status != DocumentStatus.Published || doc.CurrentVersionId is null)
            throw new InvalidOperationException("Only the published current version can be acknowledged.");

        DocumentVersion version = await db.DocumentVersions.AsNoTracking()
            .FirstAsync(x => x.Id == doc.CurrentVersionId, ct);

        PolicyAcknowledgement? existing = await db.PolicyAcknowledgements
            .FirstOrDefaultAsync(x => x.DocumentVersionId == version.Id && x.UserId == userId, ct);
        if (existing is not null)
        {
            return new PolicyAcknowledgementDto(
                existing.Id, existing.ManagedDocumentId, existing.DocumentVersionId, existing.UserId,
                existing.AcknowledgedAtUtc, existing.PolicyNumberSnapshot, existing.PolicyTitleSnapshot,
                existing.VersionNumber, existing.AcknowledgementStatementVersion, existing.Source);
        }

        PolicyAssignment? assignment = await FindAssignmentForUserAsync(userId, version.Id, ct)
            ?? throw new InvalidOperationException("This policy is not assigned to you.");

        PolicyAcknowledgement ack = PolicyAcknowledgement.Create(
            documentId, version.Id, userId, clock.UtcNow, doc.DocumentNumber, doc.Title, version.VersionNumber,
            assignment.Id, assignment.AssignedAtUtc, assignment.DueAtUtc, clientIp, userAgent);
        db.PolicyAcknowledgements.Add(ack);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Document,
            AggregateId = documentId,
            BusinessNumber = doc.DocumentNumber,
            Action = BusinessAuditAction.Updated,
            FieldName = "PolicyAcknowledged",
            NewValue = $"{userId}:{version.VersionNumber}",
            Source = AuditSource.Api,
        }, ct);
        await db.SaveChangesAsync(ct);

        return new PolicyAcknowledgementDto(
            ack.Id, ack.ManagedDocumentId, ack.DocumentVersionId, ack.UserId, ack.AcknowledgedAtUtc,
            ack.PolicyNumberSnapshot, ack.PolicyTitleSnapshot, ack.VersionNumber,
            ack.AcknowledgementStatementVersion, ack.Source);
    }

    public async Task<PolicyVersionAckStatsDto> GetVersionStatsAsync(Guid documentId, CancellationToken ct)
    {
        ManagedDocument doc = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");
        if (doc.CurrentVersionId is null)
            return new PolicyVersionAckStatsDto(doc.Id, Guid.Empty, doc.DocumentNumber, doc.Title, 0, 0, 0, 0, 0);

        DocumentVersion version = await db.DocumentVersions.AsNoTracking()
            .FirstAsync(x => x.Id == doc.CurrentVersionId, ct);
        IReadOnlyList<Guid> assignees = await ResolveAssigneeUserIdsAsync(version.Id, ct);
        HashSet<Guid> acknowledged = (await db.PolicyAcknowledgements.AsNoTracking()
            .Where(x => x.DocumentVersionId == version.Id)
            .Select(x => x.UserId)
            .ToListAsync(ct)).ToHashSet();

        DateTimeOffset now = clock.UtcNow;
        List<PolicyAssignment> assignments = await db.PolicyAssignments.AsNoTracking()
            .Where(x => x.DocumentVersionId == version.Id && x.IsRequired)
            .ToListAsync(ct);
        int overdue = 0;
        foreach (Guid userId in assignees)
        {
            if (acknowledged.Contains(userId)) continue;
            DateTimeOffset? due = ResolveDue(assignments, userId);
            if (due is DateTimeOffset d && d < now) overdue++;
        }

        int assigned = assignees.Count;
        int ackCount = assignees.Count(acknowledged.Contains);
        return new PolicyVersionAckStatsDto(
            doc.Id, version.Id, doc.DocumentNumber, doc.Title, version.VersionNumber,
            assigned, ackCount, assigned - ackCount, overdue);
    }

    public async Task<IReadOnlyList<PolicyEmployeeAckRowDto>> ListEmployeeStatusAsync(Guid documentId, CancellationToken ct)
    {
        ManagedDocument doc = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");
        if (doc.CurrentVersionId is null) return [];
        DocumentVersion version = await db.DocumentVersions.AsNoTracking()
            .FirstAsync(x => x.Id == doc.CurrentVersionId, ct);
        IReadOnlyList<Guid> assignees = await ResolveAssigneeUserIdsAsync(version.Id, ct);
        List<PolicyAssignment> assignments = await db.PolicyAssignments.AsNoTracking()
            .Where(x => x.DocumentVersionId == version.Id)
            .ToListAsync(ct);
        Dictionary<Guid, PolicyAcknowledgement> acks = await db.PolicyAcknowledgements.AsNoTracking()
            .Where(x => x.DocumentVersionId == version.Id)
            .ToDictionaryAsync(x => x.UserId, ct);
        DateTimeOffset now = clock.UtcNow;
        List<PolicyEmployeeAckRowDto> rows = [];
        foreach (Guid userId in assignees)
        {
            ActiveEmployeeInfo? emp = await employees.GetAsync(userId, ct);
            DateTimeOffset assignedAt = ResolveAssignedAt(assignments, userId);
            DateTimeOffset? due = ResolveDue(assignments, userId);
            acks.TryGetValue(userId, out PolicyAcknowledgement? ack);
            string status = ack is not null
                ? "Acknowledged"
                : due is DateTimeOffset d && d < now
                    ? "Overdue"
                    : "Outstanding";
            rows.Add(new PolicyEmployeeAckRowDto(
                userId, emp?.DisplayName, emp?.Upn, assignedAt, due, status, ack?.AcknowledgedAtUtc, version.VersionNumber));
        }

        return rows.OrderBy(x => x.Status).ThenBy(x => x.DisplayName ?? x.Upn).ToList();
    }

    public async Task<string> ExportCsvAsync(Guid documentId, CancellationToken ct)
    {
        IReadOnlyList<PolicyEmployeeAckRowDto> rows = await ListEmployeeStatusAsync(documentId, ct);
        ManagedDocument doc = await db.ManagedDocuments.AsNoTracking().FirstAsync(x => x.Id == documentId, ct);
        StringBuilder sb = new();
        sb.AppendLine("Employee,UPN,PolicyNumber,PolicyTitle,Version,AssignedAt,DueAt,Status,AcknowledgedAt");
        foreach (PolicyEmployeeAckRowDto row in rows)
        {
            sb.Append(Csv(row.DisplayName))
                .Append(',').Append(Csv(row.Upn))
                .Append(',').Append(Csv(doc.DocumentNumber))
                .Append(',').Append(Csv(doc.Title))
                .Append(',').Append(row.VersionNumber)
                .Append(',').Append(Csv(row.AssignedAtUtc.ToString("u", CultureInfo.InvariantCulture)))
                .Append(',').Append(Csv(row.DueAtUtc?.ToString("u", CultureInfo.InvariantCulture)))
                .Append(',').Append(Csv(row.Status))
                .Append(',').Append(Csv(row.AcknowledgedAtUtc?.ToString("u", CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.ReportExport,
            AggregateId = documentId,
            BusinessNumber = doc.DocumentNumber,
            Action = BusinessAuditAction.Created,
            FieldName = "PolicyAcknowledgementExport",
            NewValue = rows.Count.ToString(CultureInfo.InvariantCulture),
            Source = AuditSource.Api,
        }, ct);

        return sb.ToString();
    }

    public async Task<IReadOnlyList<PolicyAckReminderCandidate>> FindReminderCandidatesAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<PolicyAssignment> assignments = await db.PolicyAssignments.AsNoTracking()
            .Where(x => x.IsRequired)
            .ToListAsync(ct);
        if (assignments.Count == 0) return [];

        HashSet<Guid> versionIds = assignments.Select(x => x.DocumentVersionId).ToHashSet();
        Dictionary<Guid, ManagedDocument> docs = await db.ManagedDocuments.AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Published && x.CurrentVersionId != null && versionIds.Contains(x.CurrentVersionId.Value))
            .ToDictionaryAsync(x => x.CurrentVersionId!.Value, ct);
        Dictionary<(Guid AssignmentId, Guid UserId, string Kind), bool> sent = (await db.PolicyAcknowledgementReminderLogs.AsNoTracking()
            .Select(x => new { x.PolicyAssignmentId, x.UserId, x.ReminderKind })
            .ToListAsync(ct))
            .ToDictionary(x => (x.PolicyAssignmentId, x.UserId, x.ReminderKind), _ => true);

        List<PolicyAckReminderCandidate> due = [];
        foreach (IGrouping<Guid, PolicyAssignment> byVersion in assignments.GroupBy(x => x.DocumentVersionId))
        {
            if (!docs.TryGetValue(byVersion.Key, out ManagedDocument? doc)) continue;
            DocumentVersion version = await db.DocumentVersions.AsNoTracking().FirstAsync(x => x.Id == byVersion.Key, ct);
            IReadOnlyList<Guid> users = await ResolveAssigneeUserIdsAsync(byVersion.Key, ct);
            HashSet<Guid> acknowledged = (await db.PolicyAcknowledgements.AsNoTracking()
                .Where(x => x.DocumentVersionId == byVersion.Key)
                .Select(x => x.UserId)
                .ToListAsync(ct)).ToHashSet();

            foreach (Guid userId in users)
            {
                if (acknowledged.Contains(userId)) continue;
                PolicyAssignment assignment = ResolvePreferredAssignment(byVersion.ToList(), userId);
                DateTimeOffset? dueAt = assignment.DueAtUtc;
                string? kind = null;
                if (dueAt is DateTimeOffset d)
                {
                    double days = (d - now).TotalDays;
                    if (days < 0) kind = ReminderOverdue;
                    else if (days <= 1) kind = ReminderDue1;
                    else if (days <= 7) kind = ReminderDue7;
                }

                if (kind is null) continue;
                if (sent.ContainsKey((assignment.Id, userId, kind))) continue;
                due.Add(new PolicyAckReminderCandidate(
                    assignment.Id, doc.Id, version.Id, userId, doc.DocumentNumber, doc.Title,
                    version.VersionNumber, dueAt, kind));
            }
        }

        return due;
    }

    public async Task MarkReminderSentAsync(
        Guid assignmentId, Guid userId, Guid documentVersionId, string reminderKind, CancellationToken ct)
    {
        bool exists = await db.PolicyAcknowledgementReminderLogs.AnyAsync(
            x => x.PolicyAssignmentId == assignmentId && x.UserId == userId && x.ReminderKind == reminderKind, ct);
        if (exists) return;
        db.PolicyAcknowledgementReminderLogs.Add(
            PolicyAcknowledgementReminderLog.Create(assignmentId, userId, documentVersionId, reminderKind, clock.UtcNow));
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Document,
            AggregateId = documentVersionId,
            Action = BusinessAuditAction.Updated,
            FieldName = "PolicyAcknowledgementReminderSent",
            NewValue = $"{userId}:{reminderKind}",
            Source = AuditSource.Job,
        }, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<PolicyAssignment>> LoadAssignmentsForUserAsync(Guid userId, CancellationToken ct)
    {
        return await db.PolicyAssignments.AsNoTracking()
            .Where(x =>
                x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId))
            .ToListAsync(ct);
    }

    private async Task<PolicyAssignment?> FindAssignmentForUserAsync(Guid userId, Guid versionId, CancellationToken ct)
    {
        List<PolicyAssignment> list = await db.PolicyAssignments
            .Where(x => x.DocumentVersionId == versionId && (
                x.AssignmentScope == PolicyAssignmentScope.AllEmployees
                || (x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)))
            .OrderByDescending(x => x.AssignedAtUtc)
            .ToListAsync(ct);
        return list.FirstOrDefault(x => x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)
            ?? list.FirstOrDefault(x => x.AssignmentScope == PolicyAssignmentScope.AllEmployees);
    }

    private async Task<int> CountAssignedUsersAsync(Guid versionId, CancellationToken ct) =>
        (await ResolveAssigneeUserIdsAsync(versionId, ct)).Count;

    private static DateTimeOffset? ResolveDue(List<PolicyAssignment> assignments, Guid userId)
    {
        PolicyAssignment preferred = ResolvePreferredAssignment(assignments, userId);
        return preferred.DueAtUtc;
    }

    private static DateTimeOffset ResolveAssignedAt(List<PolicyAssignment> assignments, Guid userId) =>
        ResolvePreferredAssignment(assignments, userId).AssignedAtUtc;

    private static PolicyAssignment ResolvePreferredAssignment(List<PolicyAssignment> assignments, Guid userId) =>
        assignments.FirstOrDefault(x => x.AssignmentScope == PolicyAssignmentScope.SpecificUser && x.UserId == userId)
        ?? assignments.FirstOrDefault(x => x.AssignmentScope == PolicyAssignmentScope.AllEmployees)
        ?? assignments[0];

    private static string Csv(string? value)
    {
        string v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
