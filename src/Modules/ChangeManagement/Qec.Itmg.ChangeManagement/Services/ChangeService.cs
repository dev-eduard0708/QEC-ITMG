using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.ChangeManagement.Domain;
using Qec.Itmg.ChangeManagement.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;

namespace Qec.Itmg.ChangeManagement.Services;

public sealed record ChangeDto(
    Guid Id,
    string ChangeNumber,
    string Title,
    string Description,
    string Type,
    string Status,
    string RiskRating,
    Guid RequesterUserId,
    Guid? OwnerUserId,
    string? BusinessImpact,
    string? TechnicalImpact,
    string? SecurityImpact,
    string? ImplementationPlan,
    string? TestPlan,
    string? RollbackPlan,
    DateTimeOffset? ScheduledStartUtc,
    DateTimeOffset? ScheduledEndUtc,
    DateTimeOffset? ImplementationStartedAtUtc,
    DateTimeOffset? ImplementationCompletedAtUtc,
    string Result,
    string? ValidationNotes,
    string? PirNotes,
    bool IsRetrospective,
    bool IsPreAuthorizedStandard,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string RowVersion,
    int AffectedCiCount);

public sealed record ChangeListResult(IReadOnlyList<ChangeDto> Items, int TotalCount, int Page, int PageSize);

public sealed record ChangeCiDto(
    Guid ChangeRequestId,
    Guid ConfigurationItemId,
    DateTimeOffset LinkedAtUtc,
    Guid LinkedByUserId);

public sealed record ChangeApprovalDto(
    Guid Id,
    Guid ChangeRequestId,
    Guid ApproverUserId,
    string Decision,
    string? Comment,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record ChangeHistoryDto(
    Guid Id,
    string FromStatus,
    string ToStatus,
    Guid ChangedByUserId,
    string? Comment,
    DateTimeOffset ChangedAtUtc);

internal static class ChangeAuditComposer
{
    public static BusinessAuditEntry Field(
        Guid changeId,
        string? number,
        string fieldName,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated) =>
        new()
        {
            AggregateType = AuditAggregateType.Change,
            AggregateId = changeId,
            BusinessNumber = number,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry Created(Guid id, string number) =>
        new()
        {
            AggregateType = AuditAggregateType.Change,
            AggregateId = id,
            BusinessNumber = number,
            Action = BusinessAuditAction.Created,
            Source = AuditSource.Api,
        };
}

public sealed class ChangeService(
    ChangeManagementDbContext db,
    INumberSequenceService numbers,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    ISharedDbTransaction sharedDbTransaction)
{
    public const string SequenceKey = "changes";
    public const string Prefix = "CHG";

    public async Task<ChangeListResult> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        ChangeType? type = null,
        ChangeStatus? status = null,
        ChangeRiskRating? risk = null,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<ChangeRequest> query = db.ChangeRequests.AsNoTracking();
        if (type is ChangeType t) query = query.Where(item => item.Type == t);
        if (status is ChangeStatus s) query = query.Where(item => item.Status == s);
        if (risk is ChangeRiskRating r) query = query.Where(item => item.RiskRating == r);
        if (ownerUserId is Guid owner) query = query.Where(item => item.OwnerUserId == owner);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Title.Contains(term) || item.ChangeNumber.Contains(term) || item.Description.Contains(term));
        }

        int total = await query.CountAsync(cancellationToken);
        List<ChangeRequest> items = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> ciCounts = await CountCisAsync(items.Select(item => item.Id).ToList(), cancellationToken);
        return new ChangeListResult(items.Select(item => Map(item, ciCounts.GetValueOrDefault(item.Id))).ToList(), total, page, pageSize);
    }

    public async Task<ChangeDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ChangeRequest? change = await db.ChangeRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (change is null) return null;
        int count = await db.ChangeConfigurationItems.AsNoTracking()
            .CountAsync(item => item.ChangeRequestId == id, cancellationToken);
        return Map(change, count);
    }

    public async Task<ChangeRequest> CreateAsync(
        string title,
        string description,
        ChangeType type,
        Guid requesterUserId,
        ChangeRiskRating riskRating = ChangeRiskRating.Medium,
        Guid? ownerUserId = null,
        bool isRetrospective = false,
        bool isPreAuthorizedStandard = false,
        CancellationToken cancellationToken = default)
    {
        string number = await numbers.NextAsync(SequenceKey, Prefix, cancellationToken);
        ChangeRequest change = ChangeRequest.Create(
            number, title, description, type, requesterUserId, clock.UtcNow, riskRating, ownerUserId, isRetrospective, isPreAuthorizedStandard);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.ChangeRequests.Add(change);
                await businessAudit.AppendAsync(ChangeAuditComposer.Created(change.Id, change.ChangeNumber), ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return change;
    }

    public async Task<ChangeRequest> UpdateAsync(
        Guid id,
        string title,
        string description,
        ChangeType type,
        ChangeRiskRating riskRating,
        Guid? ownerUserId,
        string? businessImpact,
        string? technicalImpact,
        string? securityImpact,
        string? implementationPlan,
        string? testPlan,
        string? rollbackPlan,
        DateTimeOffset? scheduledStartUtc,
        DateTimeOffset? scheduledEndUtc,
        bool isPreAuthorizedStandard,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        ChangeRequest change = await db.ChangeRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Change was not found.");

        change.UpdateDetails(
            title, description, type, riskRating, ownerUserId, businessImpact, technicalImpact, securityImpact,
            implementationPlan, testPlan, rollbackPlan, scheduledStartUtc, scheduledEndUtc, isPreAuthorizedStandard,
            rowVersion, clock.UtcNow);

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    ChangeAuditComposer.Field(change.Id, change.ChangeNumber, "Details", null, "updated"),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return change;
    }

    public async Task<IReadOnlyList<ChangeCiDto>> ListCisAsync(Guid changeId, CancellationToken cancellationToken = default)
    {
        await EnsureExistsAsync(changeId, cancellationToken);
        return await db.ChangeConfigurationItems.AsNoTracking()
            .Where(item => item.ChangeRequestId == changeId)
            .OrderByDescending(item => item.LinkedAtUtc)
            .Select(item => new ChangeCiDto(item.ChangeRequestId, item.ConfigurationItemId, item.LinkedAtUtc, item.LinkedByUserId))
            .ToListAsync(cancellationToken);
    }

    public async Task LinkCiAsync(Guid changeId, Guid configurationItemId, Guid linkedByUserId, CancellationToken cancellationToken = default)
    {
        ChangeRequest change = await db.ChangeRequests.FirstOrDefaultAsync(item => item.Id == changeId, cancellationToken)
            ?? throw new InvalidOperationException("Change was not found.");
        bool exists = await db.ChangeConfigurationItems.AnyAsync(
            item => item.ChangeRequestId == changeId && item.ConfigurationItemId == configurationItemId,
            cancellationToken);
        if (exists) return;

        ChangeConfigurationItem link = ChangeConfigurationItem.Create(changeId, configurationItemId, linkedByUserId, clock.UtcNow);
        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.ChangeConfigurationItems.Add(link);
                await businessAudit.AppendAsync(
                    ChangeAuditComposer.Field(
                        change.Id, change.ChangeNumber, "ConfigurationItem", null, configurationItemId.ToString("D"), BusinessAuditAction.Linked),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    public async Task UnlinkCiAsync(Guid changeId, Guid configurationItemId, CancellationToken cancellationToken = default)
    {
        ChangeRequest change = await db.ChangeRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == changeId, cancellationToken)
            ?? throw new InvalidOperationException("Change was not found.");
        ChangeConfigurationItem? link = await db.ChangeConfigurationItems.FirstOrDefaultAsync(
            item => item.ChangeRequestId == changeId && item.ConfigurationItemId == configurationItemId,
            cancellationToken);
        if (link is null) return;

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                db.ChangeConfigurationItems.Remove(link);
                await businessAudit.AppendAsync(
                    ChangeAuditComposer.Field(
                        change.Id, change.ChangeNumber, "ConfigurationItem", configurationItemId.ToString("D"), null, BusinessAuditAction.Unlinked),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ChangeApprovalDto>> ListApprovalsAsync(Guid changeId, CancellationToken cancellationToken = default)
    {
        await EnsureExistsAsync(changeId, cancellationToken);
        return await db.ChangeApprovals.AsNoTracking()
            .Where(item => item.ChangeRequestId == changeId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new ChangeApprovalDto(
                item.Id, item.ChangeRequestId, item.ApproverUserId, item.Decision.ToString(), item.Comment, item.DecidedAtUtc, item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ChangeApproval> DecideApprovalAsync(
        Guid changeId,
        Guid approverUserId,
        ApprovalDecision decision,
        string? comment,
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (decision is not (ApprovalDecision.Approved or ApprovalDecision.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        ChangeRequest change = await db.ChangeRequests.FirstOrDefaultAsync(item => item.Id == changeId, cancellationToken)
            ?? throw new InvalidOperationException("Change was not found.");

        if (change.Status != ChangeStatus.Approval)
        {
            throw new InvalidOperationException("Change is not awaiting approval.");
        }

        if (approverUserId == requesterUserId)
        {
            throw new InvalidOperationException("Requester cannot approve or reject their own change.");
        }

        bool alreadyDecided = await db.ChangeApprovals.AnyAsync(
            item => item.ChangeRequestId == changeId
                && item.ApproverUserId == approverUserId
                && item.Decision != ApprovalDecision.Pending,
            cancellationToken);
        if (alreadyDecided)
        {
            throw new InvalidOperationException("Approver already recorded a decision for this change.");
        }

        ChangeApproval? pending = await db.ChangeApprovals.FirstOrDefaultAsync(
            item => item.ChangeRequestId == changeId
                && item.ApproverUserId == approverUserId
                && item.Decision == ApprovalDecision.Pending,
            cancellationToken);

        ChangeApproval approval = pending ?? ChangeApproval.CreatePending(changeId, approverUserId, clock.UtcNow);
        if (pending is null)
        {
            db.ChangeApprovals.Add(approval);
        }

        approval.Decide(decision, comment, clock.UtcNow);

        if (decision == ApprovalDecision.Rejected)
        {
            ChangeStatus from = change.Status;
            change.TransitionTo(ChangeStatus.Rejected, clock.UtcNow, Convert.ToBase64String(change.RowVersion));
            db.ChangeStatusHistories.Add(
                ChangeStatusHistory.Create(change.Id, from, ChangeStatus.Rejected, approverUserId, clock.UtcNow, comment));
        }

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    ChangeAuditComposer.Field(
                        change.Id,
                        change.ChangeNumber,
                        "Approval",
                        null,
                        $"{decision}|{approverUserId:D}",
                        decision == ApprovalDecision.Approved ? BusinessAuditAction.Updated : BusinessAuditAction.StatusChanged),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return approval;
    }

    public async Task<ChangeRequest> TransitionAsync(
        Guid id,
        ChangeStatus target,
        Guid actorUserId,
        string rowVersion,
        string? comment = null,
        string? validationNotes = null,
        string? pirNotes = null,
        ChangeResult? result = null,
        CancellationToken cancellationToken = default)
    {
        ChangeRequest change = await db.ChangeRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Change was not found.");

        await ValidateTransitionRulesAsync(change, target, cancellationToken);

        ChangeStatus from = change.Status;
        change.TransitionTo(target, clock.UtcNow, rowVersion, validationNotes, pirNotes, result);
        if (!string.IsNullOrWhiteSpace(pirNotes))
        {
            change.SetPirNotes(pirNotes, clock.UtcNow);
        }

        db.ChangeStatusHistories.Add(
            ChangeStatusHistory.Create(change.Id, from, change.Status, actorUserId, clock.UtcNow, comment));

        await sharedDbTransaction.ExecuteAsync(
            async ct =>
            {
                await businessAudit.AppendAsync(
                    ChangeAuditComposer.Field(
                        change.Id, change.ChangeNumber, "Status", from.ToString(), change.Status.ToString(), BusinessAuditAction.StatusChanged),
                    ct);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);

        return change;
    }

    public async Task<IReadOnlyList<ChangeHistoryDto>> ListHistoryAsync(Guid changeId, CancellationToken cancellationToken = default)
    {
        await EnsureExistsAsync(changeId, cancellationToken);
        return await db.ChangeStatusHistories.AsNoTracking()
            .Where(item => item.ChangeRequestId == changeId)
            .OrderBy(item => item.ChangedAtUtc)
            .Select(item => new ChangeHistoryDto(
                item.Id, item.FromStatus.ToString(), item.ToStatus.ToString(), item.ChangedByUserId, item.Comment, item.ChangedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task ValidateTransitionRulesAsync(ChangeRequest change, ChangeStatus target, CancellationToken cancellationToken)
    {
        if (target == ChangeStatus.Assessment && string.IsNullOrWhiteSpace(change.Title))
        {
            throw new InvalidOperationException("Basic change content is required.");
        }

        if (target == ChangeStatus.Approval && !change.HasAssessmentContent())
        {
            throw new InvalidOperationException("Risk, impact, and plans are required before approval.");
        }

        if (target == ChangeStatus.Scheduled)
        {
            bool preAuth = change.Type == ChangeType.Standard && change.IsPreAuthorizedStandard;
            if (!preAuth)
            {
                bool approved = await db.ChangeApprovals.AsNoTracking().AnyAsync(
                    item => item.ChangeRequestId == change.Id && item.Decision == ApprovalDecision.Approved,
                    cancellationToken);
                if (!approved)
                {
                    throw new InvalidOperationException("At least one approval is required before scheduling.");
                }
            }
        }

        if (target == ChangeStatus.Implementation)
        {
            int ciCount = await db.ChangeConfigurationItems.CountAsync(
                item => item.ChangeRequestId == change.Id, cancellationToken);
            if (ciCount < 1)
            {
                throw new InvalidOperationException("At least one affected CI is required before implementation.");
            }

            if (!change.HasScheduleAndPlans())
            {
                throw new InvalidOperationException("Schedule and implementation/test/rollback plans are required.");
            }
        }

        if (target == ChangeStatus.Closed)
        {
            if (change.Result == ChangeResult.Pending && change.Status is not ChangeStatus.Validation and not ChangeStatus.PostImplementationReview and not ChangeStatus.RequiresFollowUp and not ChangeStatus.Failed and not ChangeStatus.RolledBack)
            {
                throw new InvalidOperationException("Validation outcome is required before closing.");
            }

            if (change.RequiresPirBeforeClose() && change.Status != ChangeStatus.PostImplementationReview)
            {
                throw new InvalidOperationException("Post-implementation review is required before closing this change.");
            }

            if (change.RequiresPirBeforeClose() && string.IsNullOrWhiteSpace(change.PirNotes) && change.Status == ChangeStatus.PostImplementationReview)
            {
                // Allow close from PIR even if notes empty but prefer notes — require notes for emergency
                if (change.Type == ChangeType.Emergency)
                {
                    throw new InvalidOperationException("PIR notes are required for emergency changes before closing.");
                }
            }
        }

        if (target == ChangeStatus.Closed && change.Status == ChangeStatus.Validation)
        {
            if (change.RequiresPirBeforeClose())
            {
                throw new InvalidOperationException("PIR is mandatory before closing emergency or high-risk normal changes.");
            }
        }
    }

    private async Task EnsureExistsAsync(Guid changeId, CancellationToken cancellationToken)
    {
        bool exists = await db.ChangeRequests.AsNoTracking().AnyAsync(item => item.Id == changeId, cancellationToken);
        if (!exists) throw new InvalidOperationException("Change was not found.");
    }

    private async Task<Dictionary<Guid, int>> CountCisAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        return await db.ChangeConfigurationItems.AsNoTracking()
            .Where(item => ids.Contains(item.ChangeRequestId))
            .GroupBy(item => item.ChangeRequestId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
    }

    private static ChangeDto Map(ChangeRequest change, int ciCount) =>
        new(
            change.Id,
            change.ChangeNumber,
            change.Title,
            change.Description,
            change.Type.ToString(),
            change.Status.ToString(),
            change.RiskRating.ToString(),
            change.RequesterUserId,
            change.OwnerUserId,
            change.BusinessImpact,
            change.TechnicalImpact,
            change.SecurityImpact,
            change.ImplementationPlan,
            change.TestPlan,
            change.RollbackPlan,
            change.ScheduledStartUtc,
            change.ScheduledEndUtc,
            change.ImplementationStartedAtUtc,
            change.ImplementationCompletedAtUtc,
            change.Result.ToString(),
            change.ValidationNotes,
            change.PirNotes,
            change.IsRetrospective,
            change.IsPreAuthorizedStandard,
            change.CreatedAtUtc,
            change.UpdatedAtUtc,
            change.ClosedAtUtc,
            Convert.ToBase64String(change.RowVersion),
            ciCount);
}
