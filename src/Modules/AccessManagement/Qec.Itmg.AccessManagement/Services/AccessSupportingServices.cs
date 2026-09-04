using Microsoft.EntityFrameworkCore;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.AccessManagement.Services;

public sealed record AccessReviewCampaignDto(
    Guid Id, string Name, string Type, Guid ReviewerUserId, DateTimeOffset StartsAtUtc, DateTimeOffset DueAtUtc,
    string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    int ItemCount, int PendingCount, bool IsOverdue);

public sealed record AccessReviewCampaignListResult(
    IReadOnlyList<AccessReviewCampaignDto> Items, int TotalCount, int Page, int PageSize, int OverdueCount, int PendingDecisionCount);

public sealed record AccessReviewItemDto(
    Guid Id, Guid CampaignId, Guid? SubjectUserId, Guid? AccountRecordId, Guid? ConfigurationItemId,
    string AccessSummary, string Decision, string? ReviewerComment, DateTimeOffset? ReviewedAtUtc, DateTimeOffset CreatedAtUtc);

public sealed class AccessReviewService(
    AccessManagementDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public async Task<AccessReviewCampaignListResult> ListCampaignsAsync(int page, int pageSize, string? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<AccessReviewCampaign> q = db.AccessReviewCampaigns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, true, out AccessReviewCampaignStatus parsed))
            q = q.Where(x => x.Status == parsed);

        int total = await q.CountAsync(ct);
        List<AccessReviewCampaign> items = await q.OrderByDescending(x => x.DueAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        DateTimeOffset now = clock.UtcNow;
        int overdue = await db.AccessReviewCampaigns.AsNoTracking()
            .CountAsync(x => x.Status == AccessReviewCampaignStatus.Open && x.DueAtUtc < now, ct);
        int pendingDecisions = await db.AccessReviewItems.AsNoTracking()
            .CountAsync(x => x.Decision == AccessReviewDecision.Pending, ct);

        Dictionary<Guid, int> counts = await CountAsync(items.Select(x => x.Id).ToList(), ct);
        Dictionary<Guid, int> pending = await CountPendingAsync(items.Select(x => x.Id).ToList(), ct);
        return new(
            items.Select(x => Map(x, counts.GetValueOrDefault(x.Id), pending.GetValueOrDefault(x.Id), now)).ToList(),
            total, page, pageSize, overdue, pendingDecisions);
    }

    public async Task<AccessReviewCampaignDto?> GetCampaignAsync(Guid id, CancellationToken ct)
    {
        AccessReviewCampaign? item = await db.AccessReviewCampaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        int count = await db.AccessReviewItems.CountAsync(x => x.CampaignId == id, ct);
        int pending = await db.AccessReviewItems.CountAsync(x => x.CampaignId == id && x.Decision == AccessReviewDecision.Pending, ct);
        return Map(item, count, pending, clock.UtcNow);
    }

    public async Task<AccessReviewCampaignDto> CreateCampaignAsync(
        string name, AccessReviewType type, Guid reviewerUserId, DateTimeOffset startsAtUtc, DateTimeOffset dueAtUtc, CancellationToken ct)
    {
        AccessReviewCampaign entity = AccessReviewCampaign.Create(name, type, reviewerUserId, startsAtUtc, dueAtUtc, clock.UtcNow);
        db.AccessReviewCampaigns.Add(entity);
        await businessAudit.AppendAsync(AccessAudit.Created(entity.Id, entity.Name), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity, 0, 0, clock.UtcNow);
    }

    public async Task<AccessReviewCampaignDto> OpenAsync(Guid id, CancellationToken ct)
    {
        AccessReviewCampaign entity = await LoadAsync(id, ct);
        entity.Open(clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(entity.Id, entity.Name, "Status", "Draft", "Open", BusinessAuditAction.StatusChanged), ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, ct))!;
    }

    public async Task<AccessReviewCampaignDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        AccessReviewCampaign entity = await LoadAsync(id, ct);
        bool pending = await db.AccessReviewItems.AnyAsync(x => x.CampaignId == id && x.Decision == AccessReviewDecision.Pending, ct);
        if (pending)
            throw new InvalidOperationException("Cannot complete campaign while review items are pending.");
        entity.Complete(clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(entity.Id, entity.Name, "Status", "Open", "Completed", BusinessAuditAction.StatusChanged), ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, ct))!;
    }

    public async Task<IReadOnlyList<AccessReviewItemDto>> ListItemsAsync(Guid campaignId, CancellationToken ct)
    {
        List<AccessReviewItem> items = await db.AccessReviewItems.AsNoTracking()
            .Where(x => x.CampaignId == campaignId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<AccessReviewItemDto> AddItemAsync(
        Guid campaignId, string accessSummary, Guid? subjectUserId, Guid? accountRecordId, Guid? configurationItemId, CancellationToken ct)
    {
        _ = await LoadAsync(campaignId, ct);
        AccessReviewItem item = AccessReviewItem.Create(campaignId, accessSummary, clock.UtcNow, subjectUserId, accountRecordId, configurationItemId);
        db.AccessReviewItems.Add(item);
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task<AccessReviewItemDto> DecideAsync(Guid campaignId, Guid itemId, AccessReviewDecision decision, string? comment, CancellationToken ct)
    {
        AccessReviewItem item = await db.AccessReviewItems.FirstOrDefaultAsync(x => x.Id == itemId && x.CampaignId == campaignId, ct)
            ?? throw new InvalidOperationException("Review item not found.");
        string from = item.Decision.ToString();
        item.Decide(decision, comment, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(
            campaignId, null, "ReviewDecision", from, decision.ToString(), BusinessAuditAction.Updated, comment), ct);
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    private async Task<AccessReviewCampaign> LoadAsync(Guid id, CancellationToken ct) =>
        await db.AccessReviewCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new InvalidOperationException("Access review campaign not found.");

    private async Task<Dictionary<Guid, int>> CountAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.AccessReviewItems.AsNoTracking().Where(x => ids.Contains(x.CampaignId))
            .GroupBy(x => x.CampaignId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private async Task<Dictionary<Guid, int>> CountPendingAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.AccessReviewItems.AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId) && x.Decision == AccessReviewDecision.Pending)
            .GroupBy(x => x.CampaignId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private static AccessReviewCampaignDto Map(AccessReviewCampaign x, int count, int pending, DateTimeOffset now) =>
        new(x.Id, x.Name, x.Type.ToString(), x.ReviewerUserId, x.StartsAtUtc, x.DueAtUtc, x.Status.ToString(),
            x.CreatedAtUtc, x.UpdatedAtUtc, count, pending,
            x.Status == AccessReviewCampaignStatus.Open && x.DueAtUtc < now);

    private static AccessReviewItemDto Map(AccessReviewItem x) =>
        new(x.Id, x.CampaignId, x.SubjectUserId, x.AccountRecordId, x.ConfigurationItemId,
            x.AccessSummary, x.Decision.ToString(), x.ReviewerComment, x.ReviewedAtUtc, x.CreatedAtUtc);
}

public sealed record ManagedAccountDto(
    Guid Id, string AccountName, string Type, Guid? ConfigurationItemId, Guid? OwnerUserId,
    string Purpose, string Status, DateTimeOffset? LastReviewedAtUtc,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string RowVersion, bool IsPrivileged);

public sealed record ManagedAccountListResult(IReadOnlyList<ManagedAccountDto> Items, int TotalCount, int Page, int PageSize);

public sealed class ManagedAccountService(AccessManagementDbContext db, IClock clock, IBusinessAuditWriter businessAudit)
{
    public async Task<ManagedAccountListResult> ListAsync(int page, int pageSize, string? search, string? type, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<ManagedAccount> q = db.ManagedAccounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse(type, true, out ManagedAccountType parsed))
            q = q.Where(x => x.Type == parsed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(x => x.AccountName.Contains(term) || x.Purpose.Contains(term));
        }

        int total = await q.CountAsync(ct);
        List<ManagedAccount> items = await q.OrderBy(x => x.AccountName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<ManagedAccountDto?> GetAsync(Guid id, CancellationToken ct)
    {
        ManagedAccount? item = await db.ManagedAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<ManagedAccountDto> CreateAsync(
        string accountName, ManagedAccountType type, string purpose, Guid? configurationItemId, Guid? ownerUserId, CancellationToken ct)
    {
        ManagedAccount entity = ManagedAccount.Create(accountName, type, purpose, clock.UtcNow, configurationItemId, ownerUserId);
        db.ManagedAccounts.Add(entity);
        await businessAudit.AppendAsync(AccessAudit.Created(entity.Id, entity.AccountName), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ManagedAccountDto> UpdateAsync(
        Guid id, string accountName, string purpose, Guid? configurationItemId, Guid? ownerUserId,
        ManagedAccountStatus status, DateTimeOffset? lastReviewedAtUtc, CancellationToken ct)
    {
        ManagedAccount entity = await db.ManagedAccounts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Managed account not found.");
        entity.Update(accountName, purpose, configurationItemId, ownerUserId, status, lastReviewedAtUtc, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(entity.Id, entity.AccountName, "Updated", null, status.ToString()), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static ManagedAccountDto Map(ManagedAccount x) =>
        new(x.Id, x.AccountName, x.Type.ToString(), x.ConfigurationItemId, x.OwnerUserId, x.Purpose,
            x.Status.ToString(), x.LastReviewedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc,
            Convert.ToBase64String(x.RowVersion), x.Type == ManagedAccountType.Privileged);
}

public sealed record SodRuleDto(
    Guid Id, string Name, Guid? ApplicationConfigurationItemId, string LeftEntitlementKey, string RightEntitlementKey,
    string Severity, bool IsActive, string? Description, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record SodRuleListResult(IReadOnlyList<SodRuleDto> Items, int TotalCount, int Page, int PageSize);

public sealed class SodService(AccessManagementDbContext db, IClock clock, IBusinessAuditWriter businessAudit)
{
    public async Task<SodRuleListResult> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<SodRule> q = db.SodRules.AsNoTracking();
        if (activeOnly == true) q = q.Where(x => x.IsActive);
        int total = await q.CountAsync(ct);
        List<SodRule> items = await q.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<SodRuleDto?> GetAsync(Guid id, CancellationToken ct)
    {
        SodRule? item = await db.SodRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<SodRuleDto> CreateAsync(
        string name, string left, string right, string severity, Guid? applicationCiId, string? description, CancellationToken ct)
    {
        SodRule entity = SodRule.Create(name, left, right, severity, clock.UtcNow, applicationCiId, description);
        db.SodRules.Add(entity);
        await businessAudit.AppendAsync(AccessAudit.Created(entity.Id, entity.Name), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<SodRuleDto> UpdateAsync(
        Guid id, string name, string left, string right, string severity, bool isActive,
        Guid? applicationCiId, string? description, CancellationToken ct)
    {
        SodRule entity = await db.SodRules.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("SoD rule not found.");
        entity.Update(name, left, right, severity, isActive, applicationCiId, description, clock.UtcNow);
        await businessAudit.AppendAsync(AccessAudit.Field(entity.Id, entity.Name, "Updated", null, isActive.ToString()), ct);
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static SodRuleDto Map(SodRule x) =>
        new(x.Id, x.Name, x.ApplicationConfigurationItemId, x.LeftEntitlementKey, x.RightEntitlementKey,
            x.Severity, x.IsActive, x.Description, x.CreatedAtUtc, x.UpdatedAtUtc);
}

public sealed record AccessEvidenceProjection(
    string SourceType,
    Guid RecordId,
    string? BusinessNumber,
    string Status,
    DateTimeOffset? PeriodStartUtc,
    DateTimeOffset? PeriodEndUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<string> Approvals,
    IReadOnlyList<string> FulfillmentOrReviewDecisions,
    IReadOnlyList<string> LinkedReferences,
    IReadOnlyList<string> ActorHistorySummary);

public sealed class AccessEvidenceService(
    AccessCaseService cases,
    AccessReviewService reviews)
{
    public async Task<AccessEvidenceProjection?> PrepareCaseEvidenceAsync(Guid caseId, CancellationToken ct)
    {
        AccessCaseDto? accessCase = await cases.GetAsync(caseId, ct);
        if (accessCase is null) return null;
        if (accessCase.Status != nameof(AccessCaseStatus.Closed))
            throw new InvalidOperationException("Evidence projection is available for closed access cases only.");

        IReadOnlyList<AccessCaseItemDto> items = await cases.ListItemsAsync(caseId, ct);
        IReadOnlyList<AccessCaseExceptionDto> exceptions = await cases.ListExceptionsAsync(caseId, ct);
        List<string> refs = [];
        if (accessCase.LinkedTicketId is Guid ticket) refs.Add($"Ticket:{ticket}");
        refs.AddRange(exceptions.Select(x => $"Exception:{x.Type}:{x.Id}"));

        return new AccessEvidenceProjection(
            "AccessCase",
            accessCase.Id,
            accessCase.CaseNumber,
            accessCase.Status,
            accessCase.CreatedAtUtc,
            accessCase.ClosedAtUtc,
            accessCase.CreatedAtUtc,
            accessCase.ClosedAtUtc,
            [$"Requester:{accessCase.RequesterUserId}", accessCase.DesignatedApproverUserId is Guid a ? $"Approver:{a}" : "Approver:role-based"],
            items.Select(i => $"{i.Action}:{i.EntitlementKey}:{i.Status}").ToList(),
            refs,
            [
                $"Created {accessCase.CreatedAtUtc:u}",
                $"Closed {accessCase.ClosedAtUtc:u}",
                $"Type {accessCase.Type}",
                $"Items {items.Count}",
            ]);
    }

    public async Task<AccessEvidenceProjection?> PrepareReviewEvidenceAsync(Guid campaignId, CancellationToken ct)
    {
        AccessReviewCampaignDto? campaign = await reviews.GetCampaignAsync(campaignId, ct);
        if (campaign is null) return null;
        if (campaign.Status != nameof(AccessReviewCampaignStatus.Completed))
            throw new InvalidOperationException("Evidence projection is available for completed review campaigns only.");

        IReadOnlyList<AccessReviewItemDto> items = await reviews.ListItemsAsync(campaignId, ct);
        return new AccessEvidenceProjection(
            "AccessReviewCampaign",
            campaign.Id,
            campaign.Name,
            campaign.Status,
            campaign.StartsAtUtc,
            campaign.DueAtUtc,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            [$"Reviewer:{campaign.ReviewerUserId}"],
            items.Select(i => $"{i.AccessSummary}:{i.Decision}").ToList(),
            items.Where(i => i.AccountRecordId.HasValue).Select(i => $"Account:{i.AccountRecordId}").ToList(),
            [
                $"Campaign {campaign.Name}",
                $"Type {campaign.Type}",
                $"Items {items.Count}",
                $"Completed {campaign.UpdatedAtUtc:u}",
            ]);
    }
}
