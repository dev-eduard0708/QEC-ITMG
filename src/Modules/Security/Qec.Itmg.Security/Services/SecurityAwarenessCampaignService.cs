using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Contracts.Organization;
using Qec.Itmg.Security.Domain;
using Qec.Itmg.Security.Persistence;

namespace Qec.Itmg.Security.Services;

public sealed record AwarenessDashboardDto(
    int ActiveCampaigns,
    int AssignedEmployees,
    int Completed,
    int Overdue,
    int NotStarted,
    double CompletionRatePercent,
    IReadOnlyList<AwarenessCampaignListItemDto> Campaigns);

public sealed record AwarenessCampaignListItemDto(
    Guid Id,
    string? Number,
    string TitleEn,
    string? TitleAr,
    string Status,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? DueAtUtc,
    string AudienceSummary,
    int AssignedCount,
    int CompletedCount,
    int OutstandingCount,
    int OverdueCount,
    int NotStartedCount,
    double CompletionRatePercent,
    bool RequireQuiz,
    int? EstimatedMinutes,
    string? StarterKey);

public sealed record AwarenessContentBlockDto(
    Guid Id,
    int SortOrder,
    string ContentType,
    string TitleEn,
    string? TitleAr,
    string? BodyEn,
    string? BodyAr,
    string? Url,
    Guid? DocumentId,
    int? EstimatedMinutes);

public sealed record AwarenessAudienceRuleDto(
    Guid Id,
    string RuleType,
    Guid? DepartmentId,
    Guid? PositionId,
    Guid? UserId);

public sealed record AwarenessCampaignQuestionOptionDto(
    Guid Id,
    int SortOrder,
    string TextEn,
    string? TextAr,
    bool? IsCorrect);

public sealed record AwarenessCampaignQuestionDto(
    Guid Id,
    int SortOrder,
    string Type,
    string QuestionEn,
    string? QuestionAr,
    string? ExplanationEn,
    string? ExplanationAr,
    int Points,
    IReadOnlyList<AwarenessCampaignQuestionOptionDto> Options);

public sealed record AwarenessCampaignDetailDto(
    Guid Id,
    string? Number,
    string TitleEn,
    string? TitleAr,
    string? DescriptionEn,
    string? DescriptionAr,
    string Status,
    Guid OwnerUserId,
    Guid? CreatedByUserId,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? DueAtUtc,
    bool RequireQuiz,
    int PassingScorePercent,
    bool AllowRetry,
    int? MaxAttempts,
    bool RequireCompletion,
    Guid? PublishedVersionId,
    int? PublishedVersionNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    byte[] RowVersion,
    int AssignedCount,
    int CompletedCount,
    int OutstandingCount,
    int OverdueCount,
    IReadOnlyList<AwarenessAudienceRuleDto> AudienceRules,
    IReadOnlyList<AwarenessContentBlockDto> ContentBlocks,
    IReadOnlyList<AwarenessCampaignQuestionDto> Questions,
    string? StarterKey);

public sealed record AwarenessCampaignReportEmployeeDto(
    Guid AssignmentId,
    Guid UserId,
    string DisplayName,
    string Upn,
    string? DepartmentName,
    string? PositionNames,
    string Status,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int? Score,
    bool? Passed,
    int AttemptCount);

public sealed record AwarenessCampaignReportDepartmentDto(
    string DepartmentName,
    int Assigned,
    int Completed,
    double CompletionRatePercent);

public sealed record AwarenessCampaignReportDto(
    Guid CampaignId,
    string? Number,
    string TitleEn,
    int? VersionNumber,
    int Assigned,
    int Completed,
    int InProgress,
    int NotStarted,
    int Overdue,
    int Passed,
    int NotYetPassed,
    IReadOnlyList<AwarenessCampaignReportDepartmentDto> ByDepartment,
    IReadOnlyList<AwarenessCampaignReportEmployeeDto> Employees);

public sealed record EmployeeAwarenessV1ItemDto(
    Guid AssignmentId,
    Guid CampaignId,
    Guid? CampaignVersionId,
    string? Number,
    string TitleEn,
    string? TitleAr,
    string? DescriptionEn,
    string? DescriptionAr,
    int EstimatedMinutes,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? DueAtUtc,
    string Status,
    DateTimeOffset? CompletedAtUtc,
    int? Score,
    int AttemptCount,
    bool RequireQuiz,
    bool AllowRetry,
    int? MaxAttempts,
    bool IsOverdue);

public sealed record EmployeeAwarenessV1DetailDto(
    EmployeeAwarenessV1ItemDto Assignment,
    IReadOnlyList<AwarenessContentBlockDto> ContentBlocks,
    IReadOnlyList<AwarenessCampaignQuestionDto> Questions,
    int PassingScorePercent,
    bool RequireQuiz,
    bool AllowRetry,
    int? MaxAttempts,
    int AttemptsUsed);

public sealed record AwarenessQuizAttemptDto(
    Guid AttemptId,
    int AttemptNumber,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    int? ScorePercent,
    bool? Passed);

public sealed record AwarenessQuizSubmitV1ResultDto(
    Guid AttemptId,
    int AttemptNumber,
    int ScorePercent,
    bool Passed,
    int PassingScorePercent,
    string Message,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<AwarenessQuizAnswerResultDto> Answers);

public sealed record AwarenessQuizAnswerResultDto(
    Guid QuestionId,
    bool IsCorrect,
    IReadOnlyList<Guid> SelectedOptionIds,
    IReadOnlyList<Guid> CorrectOptionIds);

public sealed record CreateAwarenessCampaignRequest(
    string TitleEn,
    string? TitleAr,
    string? DescriptionEn,
    string? DescriptionAr,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? DueAtUtc,
    bool? RequireQuiz,
    int? PassingScorePercent,
    bool? AllowRetry,
    int? MaxAttempts,
    bool? RequireCompletion);

public sealed record UpdateAwarenessCampaignRequest(
    string TitleEn,
    string? TitleAr,
    string? DescriptionEn,
    string? DescriptionAr,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? DueAtUtc,
    bool RequireQuiz,
    int PassingScorePercent,
    bool AllowRetry,
    int? MaxAttempts,
    bool RequireCompletion,
    byte[]? RowVersion);

public sealed record SetAwarenessAudienceRequest(
    bool AllHeadOffice,
    IReadOnlyList<Guid>? DepartmentIds,
    IReadOnlyList<Guid>? PositionIds,
    IReadOnlyList<Guid>? UserIds);

public sealed record SetAwarenessContentBlockRequest(
    string ContentType,
    string TitleEn,
    string? TitleAr,
    string? BodyEn,
    string? BodyAr,
    string? Url,
    Guid? DocumentId,
    int? EstimatedMinutes);

public sealed record SetAwarenessQuestionOptionRequest(
    string TextEn,
    string? TextAr,
    bool IsCorrect);

public sealed record SetAwarenessQuestionRequest(
    string Type,
    string QuestionEn,
    string? QuestionAr,
    string? ExplanationEn,
    string? ExplanationAr,
    int? Points,
    IReadOnlyList<SetAwarenessQuestionOptionRequest> Options);

public sealed record SubmitAwarenessQuizV1Request(
    IReadOnlyList<SubmitAwarenessQuizAnswerRequest> Answers);

public sealed record SubmitAwarenessQuizAnswerRequest(
    Guid QuestionId,
    IReadOnlyList<Guid> SelectedOptionIds);

public sealed class SecurityAwarenessCampaignService(
    SecurityDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    INumberSequenceService numbers,
    IHeadOfficeAudienceResolver audienceResolver)
{
    public async Task<AwarenessDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        IReadOnlyList<AwarenessCampaignListItemDto> campaigns = await ListCampaignsAsync(null, ct);
        List<AwarenessCampaignListItemDto> active = campaigns.Where(x => x.Status == "Active").ToList();
        int assigned = active.Sum(x => x.AssignedCount);
        int completed = active.Sum(x => x.CompletedCount);
        int overdue = active.Sum(x => x.OverdueCount);
        int notStarted = active.Sum(x => x.NotStartedCount);
        double rate = assigned == 0 ? 0 : Math.Round(100.0 * completed / assigned, 1, MidpointRounding.AwayFromZero);
        return new AwarenessDashboardDto(active.Count, assigned, completed, overdue, notStarted, rate, campaigns);
    }

    public async Task<IReadOnlyList<AwarenessCampaignListItemDto>> ListCampaignsAsync(string? status, CancellationToken ct)
    {
        List<AwarenessCampaign> campaigns = await db.AwarenessCampaigns.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse(status, true, out AwarenessCampaignStatus parsed))
            campaigns = campaigns.Where(x => x.Status == parsed).ToList();

        List<Guid> ids = campaigns.Select(x => x.Id).ToList();
        List<AwarenessCompletion> completions = ids.Count == 0
            ? []
            : await db.AwarenessCompletions.AsNoTracking().Where(x => ids.Contains(x.CampaignId)).ToListAsync(ct);
        List<AwarenessAudienceRule> rules = ids.Count == 0
            ? []
            : await db.AwarenessAudienceRules.AsNoTracking().Where(x => ids.Contains(x.CampaignId)).ToListAsync(ct);
        List<AwarenessContentBlock> blocks = ids.Count == 0
            ? []
            : await db.AwarenessContentBlocks.AsNoTracking()
                .Where(x => x.CampaignId != null && ids.Contains(x.CampaignId.Value)).ToListAsync(ct);
        List<AwarenessCampaignVersion> versions = await db.AwarenessCampaignVersions.AsNoTracking()
            .Where(x => ids.Contains(x.CampaignId)).ToListAsync(ct);

        DateTimeOffset now = clock.UtcNow;
        return campaigns.Select(c =>
        {
            List<AwarenessCompletion> rows = completions.Where(x => x.CampaignId == c.Id).ToList();
            AwarenessCampaignVersion? published = c.PublishedVersionId is Guid vid
                ? versions.FirstOrDefault(v => v.Id == vid)
                : null;
            int estimated = published?.EstimatedMinutes
                ?? blocks.Where(b => b.CampaignId == c.Id).Sum(b => b.EstimatedMinutes ?? 0);
            return MapListItem(c, rows, rules.Where(r => r.CampaignId == c.Id).ToList(), estimated, now);
        }).ToList();
    }

    public async Task<AwarenessCampaignDetailDto> CreateDraftAsync(
        CreateAwarenessCampaignRequest req, Guid actorUserId, CancellationToken ct)
    {
        string number = await numbers.NextAsync("security-awareness", "SA", ct);
        AwarenessCampaign campaign = AwarenessCampaign.CreateDraft(
            number,
            req.TitleEn,
            actorUserId,
            actorUserId,
            clock.UtcNow,
            req.TitleAr,
            req.DescriptionEn,
            req.DescriptionAr,
            req.StartAtUtc,
            req.DueAtUtc,
            req.RequireQuiz == true,
            req.PassingScorePercent ?? 80,
            req.AllowRetry ?? true,
            req.MaxAttempts,
            req.RequireCompletion ?? true);
        db.AwarenessCampaigns.Add(campaign);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessCampaignCreated", null, campaign.TitleEn, ct);
        await db.SaveChangesAsync(ct);
        return await GetCampaignAsync(campaign.Id, includeAnswerKeys: true, ct)
            ?? throw new InvalidOperationException("Campaign create failed.");
    }

    public async Task<AwarenessCampaignDetailDto?> GetCampaignAsync(Guid id, bool includeAnswerKeys, CancellationToken ct)
    {
        AwarenessCampaign? campaign = await db.AwarenessCampaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (campaign is null) return null;

        List<AwarenessCompletion> completions = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        List<AwarenessAudienceRule> rules = await db.AwarenessAudienceRules.AsNoTracking()
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        List<AwarenessContentBlock> blocks = await db.AwarenessContentBlocks.AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderBy(x => x.SortOrder).ToListAsync(ct);
        List<AwarenessCampaignQuestion> questions = await db.AwarenessCampaignQuestions.AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderBy(x => x.SortOrder).ToListAsync(ct);
        HashSet<Guid> qids = questions.Select(x => x.Id).ToHashSet();
        List<AwarenessCampaignQuestionOption> options = qids.Count == 0
            ? []
            : await db.AwarenessCampaignQuestionOptions.AsNoTracking()
                .Where(x => qids.Contains(x.QuestionId))
                .OrderBy(x => x.SortOrder).ToListAsync(ct);

        AwarenessCampaignVersion? published = null;
        if (campaign.PublishedVersionId is Guid vid)
            published = await db.AwarenessCampaignVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vid, ct);

        DateTimeOffset now = clock.UtcNow;
        int assigned = completions.Count;
        int completed = completions.Count(x => x.Status is AwarenessCompletionStatus.Completed or AwarenessCompletionStatus.Exempt);
        int outstanding = completions.Count(x => x.Status == AwarenessCompletionStatus.Assigned);
        int overdue = completions.Count(x =>
            x.Status == AwarenessCompletionStatus.Assigned
            && (x.DueAtUtc ?? campaign.DueAtUtc) is DateTimeOffset due
            && due < now);

        return new AwarenessCampaignDetailDto(
            campaign.Id,
            campaign.Number,
            campaign.TitleEn,
            campaign.TitleAr,
            campaign.DescriptionEn,
            campaign.DescriptionAr,
            campaign.Status.ToString(),
            campaign.OwnerUserId,
            campaign.CreatedByUserId,
            campaign.StartsAtUtc,
            campaign.DueAtUtc,
            campaign.RequireQuiz,
            campaign.PassThresholdPercent,
            campaign.AllowRetry,
            campaign.MaxAttempts,
            campaign.RequireCompletion,
            campaign.PublishedVersionId,
            published?.VersionNumber,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.RowVersion,
            assigned,
            completed,
            outstanding,
            overdue,
            rules.Select(r => new AwarenessAudienceRuleDto(
                r.Id, r.RuleType.ToString(), r.DepartmentId, r.PositionId, r.UserId)).ToList(),
            blocks.Select(MapContentBlock).ToList(),
            questions.Select(q => MapQuestion(q, options.Where(o => o.QuestionId == q.Id).ToList(), includeAnswerKeys)).ToList(),
            campaign.StarterKey);
    }

    public async Task<AwarenessCampaignDetailDto> UpdateDetailsAsync(
        Guid id, UpdateAwarenessCampaignRequest req, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        ApplyRowVersion(db, campaign, req.RowVersion);
        campaign.UpdateDraftDetails(
            req.TitleEn, req.TitleAr, req.DescriptionEn, req.DescriptionAr,
            req.StartAtUtc, req.DueAtUtc, req.RequireQuiz, req.PassingScorePercent,
            req.AllowRetry, req.MaxAttempts, req.RequireCompletion, clock.UtcNow);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessCampaignUpdated", null, campaign.TitleEn, ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
    }

    public async Task<AwarenessCampaignDetailDto> SetAudienceAsync(
        Guid id, SetAwarenessAudienceRequest req, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        campaign.EnsureDraft();

        List<AwarenessAudienceRule> existing = await db.AwarenessAudienceRules
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        db.AwarenessAudienceRules.RemoveRange(existing);

        if (req.AllHeadOffice)
            db.AwarenessAudienceRules.Add(AwarenessAudienceRule.CreateAllHeadOffice(id));
        foreach (Guid deptId in (req.DepartmentIds ?? []).Where(x => x != Guid.Empty).Distinct())
            db.AwarenessAudienceRules.Add(AwarenessAudienceRule.CreateDepartment(id, deptId));
        foreach (Guid posId in (req.PositionIds ?? []).Where(x => x != Guid.Empty).Distinct())
            db.AwarenessAudienceRules.Add(AwarenessAudienceRule.CreatePosition(id, posId));
        foreach (Guid userId in (req.UserIds ?? []).Where(x => x != Guid.Empty).Distinct())
            db.AwarenessAudienceRules.Add(AwarenessAudienceRule.CreateSpecificUser(id, userId));

        if (!req.AllHeadOffice
            && (req.DepartmentIds is null || req.DepartmentIds.Count == 0)
            && (req.PositionIds is null || req.PositionIds.Count == 0)
            && (req.UserIds is null || req.UserIds.Count == 0))
            throw new InvalidOperationException("At least one audience rule is required.");

        campaign.Touch(clock.UtcNow);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessAudienceChanged", null,
            req.AllHeadOffice ? "AllHeadOffice" : "Rules", ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
    }

    public async Task<AwarenessCampaignDetailDto> SetContentAsync(
        Guid id, IReadOnlyList<SetAwarenessContentBlockRequest> blocks, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        campaign.EnsureDraft();

        List<AwarenessContentBlock> existing = await db.AwarenessContentBlocks
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        db.AwarenessContentBlocks.RemoveRange(existing);

        int order = 1;
        foreach (SetAwarenessContentBlockRequest block in blocks ?? [])
        {
            if (!Enum.TryParse(block.ContentType, true, out AwarenessContentType type))
                throw new InvalidOperationException($"Invalid content type '{block.ContentType}'.");
            db.AwarenessContentBlocks.Add(AwarenessContentBlock.CreateDraft(
                id, order++, type, block.TitleEn, block.TitleAr, block.BodyEn, block.BodyAr,
                block.Url, block.DocumentId, block.EstimatedMinutes));
        }

        campaign.Touch(clock.UtcNow);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessContentChanged", null, $"{order - 1} blocks", ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
    }

    public async Task<AwarenessCampaignDetailDto> SetQuizAsync(
        Guid id, IReadOnlyList<SetAwarenessQuestionRequest> questions, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        campaign.EnsureDraft();

        List<AwarenessCampaignQuestion> existingQuestions = await db.AwarenessCampaignQuestions
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        HashSet<Guid> existingIds = existingQuestions.Select(x => x.Id).ToHashSet();
        List<AwarenessCampaignQuestionOption> existingOptions = existingIds.Count == 0
            ? []
            : await db.AwarenessCampaignQuestionOptions.Where(x => existingIds.Contains(x.QuestionId)).ToListAsync(ct);
        db.AwarenessCampaignQuestionOptions.RemoveRange(existingOptions);
        db.AwarenessCampaignQuestions.RemoveRange(existingQuestions);

        int qOrder = 1;
        foreach (SetAwarenessQuestionRequest q in questions ?? [])
        {
            if (!Enum.TryParse(q.Type, true, out AwarenessQuestionType type))
                throw new InvalidOperationException($"Invalid question type '{q.Type}'.");
            IReadOnlyList<SetAwarenessQuestionOptionRequest> opts = q.Options ?? [];
            ValidateQuizOptions(type, opts);
            AwarenessCampaignQuestion question = AwarenessCampaignQuestion.CreateDraft(
                id, type, q.QuestionEn, q.QuestionAr, q.ExplanationEn, q.ExplanationAr,
                q.Points is > 0 ? q.Points.Value : 1, qOrder++);
            db.AwarenessCampaignQuestions.Add(question);
            int oOrder = 1;
            foreach (SetAwarenessQuestionOptionRequest opt in opts)
            {
                db.AwarenessCampaignQuestionOptions.Add(
                    AwarenessCampaignQuestionOption.Create(question.Id, opt.TextEn, opt.TextAr, opt.IsCorrect, oOrder++));
            }
        }

        campaign.Touch(clock.UtcNow);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessQuizChanged", null, $"{qOrder - 1} questions", ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
    }

    public async Task<HeadOfficeAudiencePreview> PreviewAudienceAsync(Guid id, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        _ = campaign;
        List<AwarenessAudienceRule> rules = await db.AwarenessAudienceRules.AsNoTracking()
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        return await PreviewFromRulesAsync(rules, ct);
    }

    public async Task<(AwarenessCampaignDetailDto Campaign, IReadOnlyList<AwarenessCompletionDto> CreatedAssignments)> LaunchAsync(
        Guid id, Guid actorUserId, byte[]? rowVersion, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        ApplyRowVersion(db, campaign, rowVersion);

        if (campaign.Status == AwarenessCampaignStatus.Active)
        {
            AwarenessCampaignDetailDto existing = (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
            return (existing, Array.Empty<AwarenessCompletionDto>());
        }

        campaign.EnsureDraft();
        List<AwarenessContentBlock> draftBlocks = await db.AwarenessContentBlocks
            .Where(x => x.CampaignId == id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        if (draftBlocks.Count == 0)
            throw new InvalidOperationException("Add at least one training content block before launch.");

        List<AwarenessCampaignQuestion> draftQuestions = await db.AwarenessCampaignQuestions
            .Where(x => x.CampaignId == id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        if (campaign.RequireQuiz && draftQuestions.Count == 0)
            throw new InvalidOperationException("Quiz is required but no questions are configured.");

        List<AwarenessAudienceRule> rules = await db.AwarenessAudienceRules
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        if (rules.Count == 0)
            throw new InvalidOperationException("Configure an audience before launch.");

        HeadOfficeAudiencePreview preview = await PreviewFromRulesAsync(rules, ct);
        if (preview.UniqueEmployees == 0)
            throw new InvalidOperationException("Audience resolved to zero eligible Head Office employees.");

        int nextVersion = 1 + await db.AwarenessCampaignVersions.CountAsync(x => x.CampaignId == id, ct);
        int estimated = draftBlocks.Sum(x => x.EstimatedMinutes ?? 0);
        AwarenessCampaignVersion version = AwarenessCampaignVersion.Create(
            id, nextVersion, campaign.TitleEn, campaign.TitleAr, campaign.DescriptionEn, campaign.DescriptionAr,
            actorUserId, campaign.RequireQuiz, campaign.PassThresholdPercent, campaign.AllowRetry,
            campaign.MaxAttempts, estimated, clock.UtcNow);
        db.AwarenessCampaignVersions.Add(version);

        foreach (AwarenessContentBlock block in draftBlocks)
            db.AwarenessContentBlocks.Add(AwarenessContentBlock.CreateVersionCopy(version.Id, block));

        HashSet<Guid> draftQids = draftQuestions.Select(x => x.Id).ToHashSet();
        List<AwarenessCampaignQuestionOption> draftOptions = draftQids.Count == 0
            ? []
            : await db.AwarenessCampaignQuestionOptions.Where(x => draftQids.Contains(x.QuestionId)).ToListAsync(ct);
        foreach (AwarenessCampaignQuestion q in draftQuestions)
        {
            AwarenessCampaignQuestion copy = AwarenessCampaignQuestion.CreateVersionCopy(version.Id, q);
            db.AwarenessCampaignQuestions.Add(copy);
            foreach (AwarenessCampaignQuestionOption opt in draftOptions.Where(o => o.QuestionId == q.Id).OrderBy(o => o.SortOrder))
                db.AwarenessCampaignQuestionOptions.Add(
                    AwarenessCampaignQuestionOption.Create(copy.Id, opt.TextEn, opt.TextAr, opt.IsCorrect, opt.SortOrder));
        }

        campaign.SetPublishedVersion(version.Id, clock.UtcNow);
        campaign.Activate(clock.UtcNow);

        List<AwarenessCompletion> created = [];
        foreach (HeadOfficeAudienceMember member in preview.Members)
        {
            bool exists = await db.AwarenessCompletions.AnyAsync(
                x => x.CampaignId == id && x.UserId == member.UserId, ct);
            if (exists) continue;

            string source = ResolveAssignmentSource(rules, member);
            AwarenessCompletion assignment = AwarenessCompletion.AssignSnapshot(
                id, version.Id, member.UserId, clock.UtcNow, campaign.DueAtUtc, source,
                member.DisplayName, member.Upn, member.DepartmentId, member.PrimaryPositionId,
                member.DepartmentName, member.PositionNames);
            db.AwarenessCompletions.Add(assignment);
            created.Add(assignment);
        }

        await AuditAsync(campaign.Id, campaign.Number, "AwarenessCampaignLaunched", "Draft", "Active", ct);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessAssigned", null, $"{created.Count}", ct);
        await db.SaveChangesAsync(ct);

        AwarenessCampaignDetailDto detail = (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
        return (detail, created.Select(MapCompletionDto).ToList());
    }

    public async Task<AwarenessCampaignDetailDto> CloseAsync(Guid id, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        campaign.Close(clock.UtcNow);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessCampaignClosed", "Active", "Closed", ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
    }

    public async Task<AwarenessCampaignDetailDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        string from = campaign.Status.ToString();
        campaign.Archive(clock.UtcNow);
        await AuditAsync(campaign.Id, campaign.Number, "AwarenessCampaignArchived", from, "Archived", ct);
        await db.SaveChangesAsync(ct);
        return (await GetCampaignAsync(id, includeAnswerKeys: true, ct))!;
    }

    public async Task<AwarenessCampaignReportDto> GetReportAsync(Guid id, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        AwarenessCampaignVersion? version = campaign.PublishedVersionId is Guid vid
            ? await db.AwarenessCampaignVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vid, ct)
            : null;
        List<AwarenessCompletion> rows = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.CampaignId == id).ToListAsync(ct);
        DateTimeOffset now = clock.UtcNow;

        List<AwarenessCampaignReportEmployeeDto> employees = rows
            .OrderBy(x => x.SnapshotDisplayName ?? x.UserId.ToString())
            .Select(x => MapReportEmployee(x, campaign, now))
            .ToList();

        var byDept = employees
            .GroupBy(x => string.IsNullOrWhiteSpace(x.DepartmentName) ? "(Unspecified)" : x.DepartmentName!)
            .Select(g =>
            {
                int assigned = g.Count();
                int completed = g.Count(x => x.Status == "Completed");
                double rate = assigned == 0 ? 0 : Math.Round(100.0 * completed / assigned, 1, MidpointRounding.AwayFromZero);
                return new AwarenessCampaignReportDepartmentDto(g.Key, assigned, completed, rate);
            })
            .OrderBy(x => x.DepartmentName)
            .ToList();

        int assignedCount = employees.Count;
        int completedCount = employees.Count(x => x.Status == "Completed");
        int inProgress = employees.Count(x => x.Status == "InProgress");
        int notStarted = employees.Count(x => x.Status == "Assigned" || x.Status == "NotStarted");
        int overdue = employees.Count(x => x.Status == "Overdue");
        int passed = employees.Count(x => x.Passed == true);
        int notYetPassed = employees.Count(x => x.Status != "Completed" && x.Passed != true);

        return new AwarenessCampaignReportDto(
            campaign.Id, campaign.Number, campaign.TitleEn, version?.VersionNumber,
            assignedCount, completedCount, inProgress, notStarted, overdue, passed, notYetPassed,
            byDept, employees);
    }

    public async Task<string> ExportReportCsvAsync(Guid id, CancellationToken ct)
    {
        AwarenessCampaignReportDto report = await GetReportAsync(id, ct);
        StringBuilder sb = new();
        sb.AppendLine("Campaign Number,Campaign Version,Employee,Email,Department,Positions,Assigned,Due,Started,Completed,Status,Score,Passed,Attempts");
        foreach (AwarenessCampaignReportEmployeeDto row in report.Employees)
        {
            sb.Append(Csv(report.Number)).Append(',')
                .Append(report.VersionNumber?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                .Append(Csv(row.DisplayName)).Append(',')
                .Append(Csv(row.Upn)).Append(',')
                .Append(Csv(row.DepartmentName)).Append(',')
                .Append(Csv(row.PositionNames)).Append(',')
                .Append(Csv(row.AssignedAtUtc.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(row.DueAtUtc?.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(row.StartedAtUtc?.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(row.CompletedAtUtc?.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(row.Status)).Append(',')
                .Append(row.Score?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                .Append(row.Passed is null ? "" : row.Passed.Value ? "true" : "false").Append(',')
                .Append(row.AttemptCount).AppendLine();
        }

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.ReportExport,
            AggregateId = id,
            BusinessNumber = report.Number,
            Action = BusinessAuditAction.Created,
            FieldName = "AwarenessCompletionExport",
            NewValue = report.Employees.Count.ToString(CultureInfo.InvariantCulture),
            Source = AuditSource.Api,
        }, ct);

        return sb.ToString();
    }

    public async Task<IReadOnlyList<EmployeeAwarenessV1ItemDto>> ListMyAssignmentsAsync(
        Guid userId, string? filter, CancellationToken ct)
    {
        List<AwarenessCompletion> assignments = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.UserId == userId).ToListAsync(ct);
        if (assignments.Count == 0) return [];

        HashSet<Guid> campaignIds = assignments.Select(x => x.CampaignId).ToHashSet();
        Dictionary<Guid, AwarenessCampaign> campaigns = await db.AwarenessCampaigns.AsNoTracking()
            .Where(x => campaignIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        HashSet<Guid> versionIds = assignments.Where(x => x.CampaignVersionId.HasValue)
            .Select(x => x.CampaignVersionId!.Value).ToHashSet();
        Dictionary<Guid, AwarenessCampaignVersion> versions = versionIds.Count == 0
            ? new Dictionary<Guid, AwarenessCampaignVersion>()
            : await db.AwarenessCampaignVersions.AsNoTracking()
                .Where(x => versionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        DateTimeOffset now = clock.UtcNow;
        List<EmployeeAwarenessV1ItemDto> items = [];
        foreach (AwarenessCompletion a in assignments)
        {
            if (!campaigns.TryGetValue(a.CampaignId, out AwarenessCampaign? campaign)) continue;
            if (campaign.Status == AwarenessCampaignStatus.Draft) continue;
            if (campaign.Status is AwarenessCampaignStatus.Closed or AwarenessCampaignStatus.Archived
                && a.Status == AwarenessCompletionStatus.Assigned)
                continue;

            AwarenessCampaignVersion? version = a.CampaignVersionId is Guid vid && versions.TryGetValue(vid, out AwarenessCampaignVersion? v)
                ? v
                : null;
            items.Add(MapEmployeeItem(a, campaign, version, now));
        }

        filter = (filter ?? "outstanding").Trim().ToLowerInvariant();
        return filter switch
        {
            "completed" => items.Where(x => x.Status == "Completed").OrderByDescending(x => x.CompletedAtUtc).ToList(),
            "all" => items.OrderBy(x => x.Status == "Completed").ThenBy(x => x.DueAtUtc).ToList(),
            _ => items.Where(x => x.Status is "Assigned" or "Overdue" or "InProgress" or "NotStarted")
                .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue).ToList(),
        };
    }

    public async Task<EmployeeAwarenessV1DetailDto?> GetMyAssignmentAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        AwarenessCompletion? assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct);
        if (assignment is null) return null;

        AwarenessCampaign campaign = await db.AwarenessCampaigns.AsNoTracking()
            .FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status == AwarenessCampaignStatus.Draft) return null;

        AwarenessCampaignVersion? version = assignment.CampaignVersionId is Guid vid
            ? await db.AwarenessCampaignVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vid, ct)
            : null;

        List<AwarenessContentBlock> blocks;
        List<AwarenessCampaignQuestion> questions;
        if (version is not null)
        {
            blocks = await db.AwarenessContentBlocks.AsNoTracking()
                .Where(x => x.CampaignVersionId == version.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
            questions = await db.AwarenessCampaignQuestions.AsNoTracking()
                .Where(x => x.CampaignVersionId == version.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        }
        else
        {
            blocks = [];
            questions = [];
        }

        HashSet<Guid> qids = questions.Select(x => x.Id).ToHashSet();
        List<AwarenessCampaignQuestionOption> options = qids.Count == 0
            ? []
            : await db.AwarenessCampaignQuestionOptions.AsNoTracking()
                .Where(x => qids.Contains(x.QuestionId)).OrderBy(x => x.SortOrder).ToListAsync(ct);

        bool requireQuiz = version?.RequireQuiz ?? campaign.RequireQuiz;
        bool allowRetry = version?.AllowRetry ?? campaign.AllowRetry;
        int? maxAttempts = version?.MaxAttempts ?? campaign.MaxAttempts;
        int passing = version?.PassingScorePercent ?? campaign.PassThresholdPercent;

        return new EmployeeAwarenessV1DetailDto(
            MapEmployeeItem(assignment, campaign, version, clock.UtcNow),
            blocks.Select(MapContentBlock).ToList(),
            questions.Select(q => MapQuestion(q, options.Where(o => o.QuestionId == q.Id).ToList(), includeAnswerKeys: false)).ToList(),
            passing,
            requireQuiz,
            allowRetry,
            maxAttempts,
            assignment.AttemptCount);
    }

    public async Task<EmployeeAwarenessV1ItemDto> StartAssignmentAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        AwarenessCompletion assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Assignment not found.");
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status != AwarenessCampaignStatus.Active)
            throw new InvalidOperationException("Campaign is not active.");
        if (assignment.Status == AwarenessCompletionStatus.Completed)
            return MapEmployeeItem(assignment, campaign,
                await LoadVersionAsync(assignment.CampaignVersionId, ct), clock.UtcNow);

        bool wasStarted = assignment.StartedAtUtc is not null;
        assignment.MarkStarted(clock.UtcNow);
        if (!wasStarted)
            await AuditAsync(assignment.Id, campaign.Number, "AwarenessStarted", null, userId.ToString(), ct);
        await db.SaveChangesAsync(ct);
        return MapEmployeeItem(assignment, campaign,
            await LoadVersionAsync(assignment.CampaignVersionId, ct), clock.UtcNow);
    }

    public async Task<AwarenessQuizAttemptDto> StartQuizAttemptAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        AwarenessCompletion assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Assignment not found.");
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status != AwarenessCampaignStatus.Active)
            throw new InvalidOperationException("Campaign is not active.");
        if (assignment.Status == AwarenessCompletionStatus.Completed)
            throw new InvalidOperationException("Assignment already completed.");
        if (assignment.CampaignVersionId is null)
            throw new InvalidOperationException("Assignment has no published version.");

        AwarenessCampaignVersion version = await db.AwarenessCampaignVersions
            .FirstAsync(x => x.Id == assignment.CampaignVersionId, ct);
        if (!version.RequireQuiz)
            throw new InvalidOperationException("This campaign does not require a quiz.");

        AwarenessAttempt? open = await db.AwarenessAttempts
            .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId && x.SubmittedAtUtc == null, ct);
        if (open is not null)
            return new AwarenessQuizAttemptDto(open.Id, open.AttemptNumber, open.StartedAtUtc, null, null, null);

        if (!version.AllowRetry && assignment.AttemptCount > 0)
            throw new InvalidOperationException("Retries are not allowed.");
        if (version.MaxAttempts is int max && assignment.AttemptCount >= max)
            throw new InvalidOperationException("Maximum quiz attempts reached.");

        assignment.MarkStarted(clock.UtcNow);
        int attemptNumber = assignment.AttemptCount + 1;
        AwarenessAttempt attempt = AwarenessAttempt.Start(assignment.Id, version.Id, attemptNumber, clock.UtcNow);
        db.AwarenessAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);
        return new AwarenessQuizAttemptDto(attempt.Id, attempt.AttemptNumber, attempt.StartedAtUtc, null, null, null);
    }

    public async Task<AwarenessQuizSubmitV1ResultDto> SubmitQuizAttemptAsync(
        Guid userId, Guid assignmentId, Guid attemptId, SubmitAwarenessQuizV1Request req, CancellationToken ct)
    {
        AwarenessCompletion assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Assignment not found.");
        AwarenessAttempt attempt = await db.AwarenessAttempts
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.AssignmentId == assignmentId, ct)
            ?? throw new InvalidOperationException("Attempt not found.");
        if (attempt.SubmittedAtUtc is not null)
        {
            return new AwarenessQuizSubmitV1ResultDto(
                attempt.Id, attempt.AttemptNumber, attempt.Score ?? 0, attempt.Passed == true,
                0, "Already submitted.", assignment.CompletedAtUtc, []);
        }

        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status != AwarenessCampaignStatus.Active)
            throw new InvalidOperationException("Campaign is not active.");
        AwarenessCampaignVersion version = await db.AwarenessCampaignVersions
            .FirstAsync(x => x.Id == assignment.CampaignVersionId, ct);

        List<AwarenessCampaignQuestion> questions = await db.AwarenessCampaignQuestions
            .Where(x => x.CampaignVersionId == version.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        HashSet<Guid> qids = questions.Select(x => x.Id).ToHashSet();
        List<AwarenessCampaignQuestionOption> options = await db.AwarenessCampaignQuestionOptions
            .Where(x => qids.Contains(x.QuestionId)).ToListAsync(ct);

        Dictionary<Guid, List<Guid>> answers = (req.Answers ?? [])
            .ToDictionary(x => x.QuestionId, x => (x.SelectedOptionIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList());
        if (answers.Count < questions.Count)
            throw new InvalidOperationException("Answer all questions before submitting.");

        int earned = 0;
        int totalPoints = 0;
        List<AwarenessQuizAnswerResultDto> results = [];
        foreach (AwarenessCampaignQuestion q in questions)
        {
            if (!answers.TryGetValue(q.Id, out List<Guid>? selected))
                throw new InvalidOperationException("Answer all questions before submitting.");
            List<Guid> correctIds = options.Where(o => o.QuestionId == q.Id && o.IsCorrect).Select(o => o.Id).OrderBy(x => x).ToList();
            List<Guid> selectedOrdered = selected.OrderBy(x => x).ToList();
            foreach (Guid sid in selectedOrdered)
            {
                if (!options.Any(o => o.Id == sid && o.QuestionId == q.Id))
                    throw new InvalidOperationException("Invalid answer option.");
            }

            bool isCorrect = correctIds.SequenceEqual(selectedOrdered);
            totalPoints += q.Points;
            if (isCorrect) earned += q.Points;
            db.AwarenessQuizAnswers.Add(AwarenessQuizAnswer.Create(attempt.Id, q.Id, selectedOrdered, isCorrect));
            results.Add(new AwarenessQuizAnswerResultDto(q.Id, isCorrect, selectedOrdered, correctIds));
        }

        int score = totalPoints == 0
            ? 0
            : (int)Math.Round(100.0 * earned / totalPoints, MidpointRounding.AwayFromZero);
        bool passed = score >= version.PassingScorePercent;
        attempt.Submit(score, passed, clock.UtcNow);
        assignment.RecordAttempt(score, passed, clock.UtcNow);

        await AuditAsync(assignment.Id, campaign.Number, "AwarenessQuizSubmitted", null, $"{score}:{passed}", ct);
        if (passed)
            await AuditAsync(assignment.Id, campaign.Number, "AwarenessCompleted", null, $"{score}", ct);
        await db.SaveChangesAsync(ct);

        return new AwarenessQuizSubmitV1ResultDto(
            attempt.Id, attempt.AttemptNumber, score, passed, version.PassingScorePercent,
            passed ? "Completed. Thank you." : "Review the material and try again.",
            assignment.CompletedAtUtc, results);
    }

    public async Task<EmployeeAwarenessV1ItemDto> CompleteWithoutQuizAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        AwarenessCompletion assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Assignment not found.");
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status != AwarenessCampaignStatus.Active)
            throw new InvalidOperationException("Campaign is not active.");
        if (assignment.Status == AwarenessCompletionStatus.Completed)
            return MapEmployeeItem(assignment, campaign, await LoadVersionAsync(assignment.CampaignVersionId, ct), clock.UtcNow);

        AwarenessCampaignVersion? version = await LoadVersionAsync(assignment.CampaignVersionId, ct);
        bool requireQuiz = version?.RequireQuiz ?? campaign.RequireQuiz;
        if (requireQuiz)
            throw new InvalidOperationException("This campaign requires a passing quiz to complete.");

        assignment.MarkStarted(clock.UtcNow);
        assignment.Complete(clock.UtcNow);
        await AuditAsync(assignment.Id, campaign.Number, "AwarenessCompleted", null, "no-quiz", ct);
        await db.SaveChangesAsync(ct);
        return MapEmployeeItem(assignment, campaign, version, clock.UtcNow);
    }

    private async Task<HeadOfficeAudiencePreview> PreviewFromRulesAsync(
        IReadOnlyList<AwarenessAudienceRule> rules, CancellationToken ct)
    {
        bool all = rules.Any(x => x.RuleType == AwarenessAudienceRuleType.AllHeadOffice);
        List<Guid> departments = rules.Where(x => x.RuleType == AwarenessAudienceRuleType.Department && x.DepartmentId.HasValue)
            .Select(x => x.DepartmentId!.Value).Distinct().ToList();
        List<Guid> positions = rules.Where(x => x.RuleType == AwarenessAudienceRuleType.Position && x.PositionId.HasValue)
            .Select(x => x.PositionId!.Value).Distinct().ToList();
        List<Guid> users = rules.Where(x => x.RuleType == AwarenessAudienceRuleType.SpecificUser && x.UserId.HasValue)
            .Select(x => x.UserId!.Value).Distinct().ToList();
        return await audienceResolver.PreviewAsync(all, departments, positions, users, ct);
    }

    private static string ResolveAssignmentSource(
        IReadOnlyList<AwarenessAudienceRule> rules, HeadOfficeAudienceMember member)
    {
        if (rules.Any(x => x.RuleType == AwarenessAudienceRuleType.SpecificUser && x.UserId == member.UserId))
            return "SpecificUser";
        if (member.PrimaryPositionId is Guid pid &&
            rules.Any(x => x.RuleType == AwarenessAudienceRuleType.Position && x.PositionId == pid))
            return "Position";
        if (member.DepartmentId is Guid did &&
            rules.Any(x => x.RuleType == AwarenessAudienceRuleType.Department && x.DepartmentId == did))
            return "Department";
        if (rules.Any(x => x.RuleType == AwarenessAudienceRuleType.AllHeadOffice))
            return "AllHeadOffice";
        return "Audience";
    }

    private static void ValidateQuizOptions(AwarenessQuestionType type, IReadOnlyList<SetAwarenessQuestionOptionRequest> opts)
    {
        if (opts.Count < 2)
            throw new InvalidOperationException("Each question needs at least two options.");
        int correct = opts.Count(x => x.IsCorrect);
        switch (type)
        {
            case AwarenessQuestionType.SingleChoice:
            case AwarenessQuestionType.TrueFalse:
                if (correct != 1)
                    throw new InvalidOperationException($"{type} requires exactly one correct option.");
                break;
            case AwarenessQuestionType.MultipleChoice:
                if (correct < 1)
                    throw new InvalidOperationException("MultipleChoice requires one or more correct options.");
                break;
            default:
                throw new InvalidOperationException($"Unsupported question type {type}.");
        }
    }

    private static void ApplyRowVersion(SecurityDbContext db, AwarenessCampaign campaign, byte[]? rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0) return;
        db.Entry(campaign).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    private async Task<AwarenessCampaignVersion?> LoadVersionAsync(Guid? versionId, CancellationToken ct)
    {
        if (versionId is null) return null;
        return await db.AwarenessCampaignVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId, ct);
    }

    private async Task AuditAsync(
        Guid id, string? number, string field, string? oldValue, string? newValue, CancellationToken ct) =>
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Risk,
            AggregateId = id,
            BusinessNumber = number,
            Action = BusinessAuditAction.Updated,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        }, ct);

    private static AwarenessCampaignListItemDto MapListItem(
        AwarenessCampaign c,
        IReadOnlyList<AwarenessCompletion> completions,
        IReadOnlyList<AwarenessAudienceRule> rules,
        int estimatedMinutes,
        DateTimeOffset now)
    {
        int assigned = completions.Count;
        int completed = completions.Count(x => x.Status is AwarenessCompletionStatus.Completed or AwarenessCompletionStatus.Exempt);
        int outstanding = completions.Count(x => x.Status == AwarenessCompletionStatus.Assigned);
        int overdue = completions.Count(x =>
            x.Status == AwarenessCompletionStatus.Assigned
            && (x.DueAtUtc ?? c.DueAtUtc) is DateTimeOffset due
            && due < now);
        int notStarted = completions.Count(x =>
            x.Status == AwarenessCompletionStatus.Assigned && x.StartedAtUtc is null);
        double rate = assigned == 0 ? 0 : Math.Round(100.0 * completed / assigned, 1, MidpointRounding.AwayFromZero);
        return new AwarenessCampaignListItemDto(
            c.Id, c.Number, c.TitleEn, c.TitleAr, c.Status.ToString(), c.StartsAtUtc, c.DueAtUtc,
            SummarizeAudience(rules), assigned, completed, outstanding, overdue, notStarted, rate,
            c.RequireQuiz, estimatedMinutes == 0 ? null : estimatedMinutes, c.StarterKey);
    }

    private static string SummarizeAudience(IReadOnlyList<AwarenessAudienceRule> rules)
    {
        if (rules.Any(x => x.RuleType == AwarenessAudienceRuleType.AllHeadOffice))
            return "All Head Office";
        List<string> parts = [];
        int depts = rules.Count(x => x.RuleType == AwarenessAudienceRuleType.Department);
        int positions = rules.Count(x => x.RuleType == AwarenessAudienceRuleType.Position);
        int users = rules.Count(x => x.RuleType == AwarenessAudienceRuleType.SpecificUser);
        if (depts > 0) parts.Add($"{depts} dept");
        if (positions > 0) parts.Add($"{positions} position");
        if (users > 0) parts.Add($"{users} user");
        return parts.Count == 0 ? "Not set" : string.Join(", ", parts);
    }

    private static AwarenessContentBlockDto MapContentBlock(AwarenessContentBlock b) =>
        new(b.Id, b.SortOrder, b.ContentType.ToString(), b.TitleEn, b.TitleAr, b.BodyEn, b.BodyAr, b.Url, b.DocumentId, b.EstimatedMinutes);

    private static AwarenessCampaignQuestionDto MapQuestion(
        AwarenessCampaignQuestion q, IReadOnlyList<AwarenessCampaignQuestionOption> options, bool includeAnswerKeys) =>
        new(q.Id, q.SortOrder, q.Type.ToString(), q.QuestionEn, q.QuestionAr,
            includeAnswerKeys ? q.ExplanationEn : null,
            includeAnswerKeys ? q.ExplanationAr : null,
            q.Points,
            options.Select(o => new AwarenessCampaignQuestionOptionDto(
                o.Id, o.SortOrder, o.TextEn, o.TextAr, includeAnswerKeys ? o.IsCorrect : null)).ToList());

    private static EmployeeAwarenessV1ItemDto MapEmployeeItem(
        AwarenessCompletion a, AwarenessCampaign campaign, AwarenessCampaignVersion? version, DateTimeOffset now)
    {
        bool overdue = a.Status == AwarenessCompletionStatus.Assigned
            && (a.DueAtUtc ?? campaign.DueAtUtc) is DateTimeOffset due && due < now;
        string status = a.Status == AwarenessCompletionStatus.Completed
            ? "Completed"
            : a.Status == AwarenessCompletionStatus.Exempt
                ? "Exempt"
                : overdue
                    ? "Overdue"
                    : a.StartedAtUtc is not null
                        ? "InProgress"
                        : "NotStarted";
        return new EmployeeAwarenessV1ItemDto(
            a.Id, a.CampaignId, a.CampaignVersionId, campaign.Number,
            version?.TitleEn ?? campaign.TitleEn,
            version?.TitleAr ?? campaign.TitleAr,
            version?.DescriptionEn ?? campaign.DescriptionEn,
            version?.DescriptionAr ?? campaign.DescriptionAr,
            version?.EstimatedMinutes ?? 5,
            a.AssignedAtUtc, a.DueAtUtc ?? campaign.DueAtUtc, status,
            a.CompletedAtUtc, a.Score, a.AttemptCount,
            version?.RequireQuiz ?? campaign.RequireQuiz,
            version?.AllowRetry ?? campaign.AllowRetry,
            version?.MaxAttempts ?? campaign.MaxAttempts,
            overdue);
    }

    private static AwarenessCampaignReportEmployeeDto MapReportEmployee(
        AwarenessCompletion x, AwarenessCampaign campaign, DateTimeOffset now)
    {
        bool overdue = x.Status == AwarenessCompletionStatus.Assigned
            && (x.DueAtUtc ?? campaign.DueAtUtc) is DateTimeOffset due && due < now;
        string status = x.Status == AwarenessCompletionStatus.Completed
            ? "Completed"
            : x.Status == AwarenessCompletionStatus.Exempt
                ? "Exempt"
                : overdue
                    ? "Overdue"
                    : x.StartedAtUtc is not null
                        ? "InProgress"
                        : "NotStarted";
        return new AwarenessCampaignReportEmployeeDto(
            x.Id, x.UserId,
            x.SnapshotDisplayName ?? x.UserId.ToString(),
            x.SnapshotUpn ?? string.Empty,
            x.SnapshotDepartmentName,
            x.SnapshotPositionNames,
            status,
            x.AssignedAtUtc,
            x.DueAtUtc ?? campaign.DueAtUtc,
            x.StartedAtUtc,
            x.CompletedAtUtc,
            x.Score,
            x.PassedAtUtc is not null ? true : x.Status == AwarenessCompletionStatus.Completed && x.Score is not null ? x.Score >= campaign.PassThresholdPercent : null,
            x.AttemptCount);
    }

    private static AwarenessCompletionDto MapCompletionDto(AwarenessCompletion x) => new(
        x.Id, x.CampaignId, x.UserId, x.Status.ToString(), x.CompletedAtUtc, x.EvidenceId, x.Notes,
        x.AssignedAtUtc, x.DueAtUtc, x.StartedAtUtc, x.Score, x.AttemptCount, x.ModuleVersion);

    private static string Csv(string? value)
    {
        string v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
