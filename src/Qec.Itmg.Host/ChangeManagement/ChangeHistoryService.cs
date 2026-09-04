using Microsoft.EntityFrameworkCore;
using Qec.Itmg.ChangeManagement.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Host.ChangeManagement;

public sealed record ChangeTimelineEventDto(
    Guid Id,
    string Event,
    Guid? ActorUserId,
    DateTimeOffset OccurredAtUtc,
    string Summary,
    string? Details);

public sealed class ChangeHistoryService(
    PlatformDbContext platformDb,
    ChangeManagementDbContext changeDb)
{
    public async Task<IReadOnlyList<ChangeTimelineEventDto>> ListAsync(
        Guid changeId,
        CancellationToken cancellationToken = default)
    {
        bool exists = await changeDb.ChangeRequests.AsNoTracking()
            .AnyAsync(item => item.Id == changeId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Change was not found.");
        }

        var audit = await platformDb.BusinessAuditRecords.AsNoTracking()
            .Where(item => item.AggregateType == AuditAggregateType.Change && item.AggregateId == changeId)
            .OrderBy(item => item.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return audit.Select(Map).ToList();
    }

    private static ChangeTimelineEventDto Map(Platform.Domain.BusinessAuditRecord record)
    {
        string eventName = (record.Action, record.FieldName) switch
        {
            (BusinessAuditAction.Created, _) => "Created",
            (BusinessAuditAction.Updated, "Details") => "Edited",
            (BusinessAuditAction.Linked, _) => "CI linked",
            (BusinessAuditAction.Unlinked, _) => "CI unlinked",
            (BusinessAuditAction.Assigned, "ApprovalRequested") => "Approval requested",
            (BusinessAuditAction.Updated, "Approval") when record.NewValue?.StartsWith("Approved", StringComparison.OrdinalIgnoreCase) == true
                => "Approved",
            (BusinessAuditAction.Updated, "Approval") => "Approval decision",
            (BusinessAuditAction.StatusChanged, "Approval") => "Rejected",
            (BusinessAuditAction.StatusChanged, "Status") => StatusEvent(record.NewValue),
            (BusinessAuditAction.Updated, "Retrospective") => "Retrospective marked",
            (BusinessAuditAction.Updated, "CatalogItem") => "Catalog source",
            _ => record.FieldName ?? record.Action.ToString(),
        };

        string summary = eventName switch
        {
            "Created" => "Change request created",
            "Edited" => "Change details updated",
            "CI linked" => $"Linked CI {record.NewValue}",
            "CI unlinked" => $"Unlinked CI {record.OldValue}",
            "Approval requested" => $"Approval requested from {record.NewValue}",
            "Approved" => "Change approved",
            "Rejected" => "Change rejected",
            "Retrospective marked" => "Marked as retrospective",
            "Catalog source" => $"Created from catalog {record.NewValue}",
            _ when record.OldValue is not null && record.NewValue is not null
                => $"{record.OldValue} → {record.NewValue}",
            _ => record.NewValue ?? record.FieldName ?? record.Action.ToString(),
        };

        string? details = record.Reason;
        if (string.IsNullOrWhiteSpace(details) && record.FieldName is "Status")
        {
            details = $"{record.OldValue} → {record.NewValue}";
        }

        return new ChangeTimelineEventDto(
            record.Id,
            eventName,
            record.ActorUserId,
            record.OccurredAtUtc,
            summary,
            details);
    }

    private static string StatusEvent(string? status) => status switch
    {
        "Assessment" => "Submitted",
        "Approval" => "Submitted for approval",
        "Scheduled" => "Scheduled",
        "Implementation" => "Implementation started",
        "Validation" => "Validation",
        "PostImplementationReview" => "PIR",
        "Closed" => "Closed",
        "Cancelled" => "Cancelled",
        "Failed" => "Failed",
        "RolledBack" => "Rolled back",
        "RequiresFollowUp" => "Requires follow-up",
        "Rejected" => "Rejected",
        _ => status ?? "Status changed",
    };
}
