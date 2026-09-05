using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;

namespace Qec.Itmg.Host.Integrations;

/// <summary>
/// Executes approved AccessCase checklist items against the directory provider.
/// Never bypasses approvals/SoD — only cases already in Fulfillment are eligible.
/// </summary>
public sealed class DirectoryJmlFulfillmentService(
    AccessManagementDbContext access,
    IdentityDbContext identity,
    IDirectorySyncClient directory,
    IBusinessAuditWriter audit,
    IClock clock,
    ILogger<DirectoryJmlFulfillmentService> logger)
{
    public async Task<int> ExecuteEligibleAsync(CancellationToken ct)
    {
        IntegrationReadiness readiness = directory.GetReadiness();
        if (!readiness.Enabled || !readiness.Configured)
            return 0;

        List<AccessCase> cases = await access.AccessCases
            .Where(x => x.Status == AccessCaseStatus.Fulfillment)
            .Take(50)
            .ToListAsync(ct);

        int executed = 0;
        foreach (AccessCase accessCase in cases)
        {
            if (accessCase.SubjectUserId is null)
                continue;
            User? user = await identity.Users.FirstOrDefaultAsync(u => u.Id == accessCase.SubjectUserId.Value, ct);
            if (user is null || string.IsNullOrWhiteSpace(user.DirectoryObjectId))
                continue;

            List<AccessCaseItem> items = await access.AccessCaseItems
                .Where(i => i.AccessCaseId == accessCase.Id && i.Status == AccessItemStatus.Pending)
                .ToListAsync(ct);

            foreach (AccessCaseItem item in items)
            {
                DirectoryJmlActionKind kind = MapAction(accessCase.Type, item);

                DirectoryJmlActionResult result = await directory.ExecuteJmlActionAsync(
                    new DirectoryJmlActionRequest(
                        accessCase.Id,
                        accessCase.CaseNumber,
                        user.Id,
                        user.DirectoryObjectId!,
                        kind,
                        item.EntitlementKey,
                        item.EntitlementKey,
                        $"{accessCase.Id:N}:{item.Id:N}"),
                    ct);

                await audit.AppendAsync(new BusinessAuditEntry
                {
                    AggregateType = AuditAggregateType.Access,
                    AggregateId = accessCase.Id,
                    BusinessNumber = accessCase.CaseNumber,
                    Action = BusinessAuditAction.Updated,
                    FieldName = "DirectoryJml",
                    NewValue =
                        $"{{\"provider\":\"{result.Provider}\",\"action\":\"{kind}\",\"targetUser\":\"{user.Id}\",\"succeeded\":{result.Succeeded.ToString().ToLowerInvariant()},\"skipped\":{result.Skipped.ToString().ToLowerInvariant()},\"external\":\"{result.ExternalReference}\",\"at\":\"{clock.UtcNow:o}\"}}",
                    Source = AuditSource.Integration,
                }, ct);

                if (result.Succeeded)
                {
                    item.MarkCompleted(user.Id, clock.UtcNow, "Directory JML action executed.");
                    executed++;
                }
                else if (!result.Skipped)
                {
                    logger.LogWarning("Directory JML failed for case {Case} item {Item}", accessCase.CaseNumber, item.Id);
                }
            }
        }

        await access.SaveChangesAsync(ct);
        return executed;
    }

    private static DirectoryJmlActionKind MapAction(AccessCaseType type, AccessCaseItem item) =>
        item.Action switch
        {
            AccessItemAction.Disable => DirectoryJmlActionKind.DisableUser,
            AccessItemAction.Remove => DirectoryJmlActionKind.RemoveGroupMembership,
            AccessItemAction.Grant => DirectoryJmlActionKind.AddGroupMembership,
            AccessItemAction.Reassign => DirectoryJmlActionKind.SyncMetadata,
            _ => type switch
            {
                AccessCaseType.Leaver => DirectoryJmlActionKind.DisableUser,
                AccessCaseType.Joiner => DirectoryJmlActionKind.EnableUser,
                _ => DirectoryJmlActionKind.SyncMetadata,
            },
        };
}
