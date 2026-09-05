using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Identity;
using Qec.Itmg.Security.Domain;
using Qec.Itmg.Security.Persistence;

namespace Qec.Itmg.Security.Services;

public sealed record AwarenessModuleDto(
    Guid Id, string Code, string Title, string? Summary, string Body, int Version, string Status,
    int EstimatedMinutes, int PassThresholdPercent, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AwarenessQuestionDto> Questions);

public sealed record AwarenessQuestionDto(
    Guid Id, string QuestionText, int DisplayOrder, IReadOnlyList<AwarenessAnswerOptionDto> Options);

public sealed record AwarenessAnswerOptionDto(Guid Id, string Text, int DisplayOrder, bool? IsCorrect);

public sealed record EmployeeAwarenessItemDto(
    Guid AssignmentId, Guid CampaignId, Guid? ModuleId, string Title, string? Summary,
    int EstimatedMinutes, DateTimeOffset AssignedAtUtc, DateTimeOffset? DueAtUtc, string Status,
    DateTimeOffset? CompletedAtUtc, int? Score, int AttemptCount, int? ModuleVersion, bool IsOverdue);

public sealed record EmployeeAwarenessSummaryDto(int Assigned, int Completed, int Outstanding, int Overdue);

public sealed record AwarenessQuizSubmitResultDto(
    bool Passed, int Score, int AttemptNumber, string Message, DateTimeOffset? CompletedAtUtc);

public sealed record AwarenessReminderCandidate(
    Guid AssignmentId, Guid UserId, Guid CampaignId, string Title, DateTimeOffset? DueAtUtc, string ReminderKind);

public sealed class SecurityAwarenessWorkflowService(
    SecurityDbContext db,
    IClock clock,
    IBusinessAuditWriter businessAudit,
    IActiveEmployeeLookup employees)
{
    public const string ReminderDue7 = "due_7";
    public const string ReminderDue1 = "due_1";
    public const string ReminderOverdue = "overdue";

    public async Task EnsureStarterModulesAsync(CancellationToken ct)
    {
        foreach (var template in StarterTemplates())
        {
            if (await db.AwarenessModules.AnyAsync(x => x.Code == template.Code, ct)) continue;
            AwarenessModule module = AwarenessModule.Create(
                template.Code, template.Title, template.Body, clock.UtcNow, template.Summary,
                estimatedMinutes: template.Minutes, passThresholdPercent: template.PassPercent,
                status: AwarenessModuleStatus.Draft);
            db.AwarenessModules.Add(module);
            int qOrder = 1;
            foreach (var q in template.Questions)
            {
                AwarenessQuestion question = AwarenessQuestion.Create(module.Id, q.Text, qOrder++);
                db.AwarenessQuestions.Add(question);
                int aOrder = 1;
                foreach (var a in q.Answers)
                {
                    db.AwarenessAnswerOptions.Add(
                        AwarenessAnswerOption.Create(question.Id, a.Text, a.Correct, aOrder++));
                }
            }

            await businessAudit.AppendAsync(new BusinessAuditEntry
            {
                AggregateType = AuditAggregateType.Risk,
                AggregateId = module.Id,
                BusinessNumber = module.Code,
                Action = BusinessAuditAction.Created,
                FieldName = "AwarenessModuleSeeded",
                NewValue = module.Title,
                Source = AuditSource.Api,
            }, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AwarenessModuleDto>> ListModulesAsync(bool includeInactive, CancellationToken ct)
    {
        IQueryable<AwarenessModule> q = db.AwarenessModules.AsNoTracking();
        if (!includeInactive)
            q = q.Where(x => x.Status != AwarenessModuleStatus.Inactive);
        List<AwarenessModule> modules = await q.OrderBy(x => x.Code).ToListAsync(ct);
        return await MapModulesAsync(modules, includeCorrectAnswers: true, ct);
    }

    public async Task ActivateModuleAsync(Guid moduleId, CancellationToken ct)
    {
        AwarenessModule module = await db.AwarenessModules.FirstOrDefaultAsync(x => x.Id == moduleId, ct)
            ?? throw new InvalidOperationException("Module not found.");
        module.Activate(clock.UtcNow);
        await businessAudit.AppendAsync(Field(module.Id, module.Code, "AwarenessModuleActivated", null, "Active"), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AwarenessCampaignDto> CreateCampaignForModuleAsync(
        Guid moduleId, string? title, Guid ownerUserId, DateTimeOffset? dueAtUtc, CancellationToken ct)
    {
        AwarenessModule module = await db.AwarenessModules.FirstOrDefaultAsync(x => x.Id == moduleId, ct)
            ?? throw new InvalidOperationException("Module not found.");
        if (module.Status != AwarenessModuleStatus.Active)
            throw new InvalidOperationException("Only active modules can be used in campaigns.");

        AwarenessCampaign campaign = AwarenessCampaign.Create(
            string.IsNullOrWhiteSpace(title) ? module.Title : title.Trim(),
            ownerUserId, clock.UtcNow, clock.UtcNow, module.Summary, dueAtUtc,
            module.Id, module.Version, module.PassThresholdPercent);
        db.AwarenessCampaigns.Add(campaign);
        await businessAudit.AppendAsync(Field(campaign.Id, campaign.Title, "AwarenessCampaignCreated", null, module.Code), ct);
        await db.SaveChangesAsync(ct);
        return new AwarenessCampaignDto(
            campaign.Id, campaign.Title, campaign.Description, campaign.StartsAtUtc, campaign.DueAtUtc,
            campaign.Status.ToString(), campaign.OwnerUserId, campaign.CreatedAtUtc, 0, 0, 0, 0,
            campaign.ModuleId, campaign.ModuleVersion, campaign.PassThresholdPercent);
    }

    public async Task<IReadOnlyList<AwarenessCompletionDto>> OpenAndAssignAsync(
        Guid campaignId, bool allEmployees, IReadOnlyList<Guid>? userIds, Guid actorUserId, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        if (campaign.Status == AwarenessCampaignStatus.Draft)
        {
            campaign.Open();
            await businessAudit.AppendAsync(Field(campaign.Id, campaign.Title, "AwarenessCampaignActivated", "Draft", "Open"), ct);
        }

        HashSet<Guid> targets = [];
        if (allEmployees)
        {
            foreach (ActiveEmployeeInfo emp in await employees.ListActiveAsync(ct))
                targets.Add(emp.Id);
        }
        else
        {
            foreach (Guid id in userIds ?? [])
                if (id != Guid.Empty) targets.Add(id);
        }

        if (targets.Count == 0)
            throw new InvalidOperationException("No employees to assign.");

        List<AwarenessCompletion> created = [];
        foreach (Guid userId in targets)
        {
            bool exists = await db.AwarenessCompletions.AnyAsync(
                x => x.CampaignId == campaignId && x.UserId == userId, ct);
            if (exists) continue;
            AwarenessCompletion assignment = AwarenessCompletion.Assign(
                campaignId, userId, clock.UtcNow, campaign.DueAtUtc, campaign.ModuleVersion);
            db.AwarenessCompletions.Add(assignment);
            created.Add(assignment);
        }

        await businessAudit.AppendAsync(Field(
            campaign.Id, campaign.Title, "AwarenessAssigned", null, $"{targets.Count}:{actorUserId}"), ct);
        await db.SaveChangesAsync(ct);
        return created.Select(x => new AwarenessCompletionDto(
            x.Id, x.CampaignId, x.UserId, x.Status.ToString(), x.CompletedAtUtc, x.EvidenceId, x.Notes,
            x.AssignedAtUtc, x.DueAtUtc, x.StartedAtUtc, x.Score, x.AttemptCount, x.ModuleVersion)).ToList();
    }

    public async Task CloseCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        campaign.Close();
        await businessAudit.AppendAsync(Field(campaign.Id, campaign.Title, "AwarenessCampaignClosed", "Open", "Closed"), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<EmployeeAwarenessSummaryDto> GetEmployeeSummaryAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<EmployeeAwarenessItemDto> items = await ListEmployeeAssignmentsAsync(userId, "all", ct);
        int assigned = items.Count;
        int completed = items.Count(x => x.Status == "Completed");
        int outstanding = items.Count(x => x.Status is "Assigned" or "Overdue" or "InProgress");
        int overdue = items.Count(x => x.Status == "Overdue");
        return new EmployeeAwarenessSummaryDto(assigned, completed, outstanding, overdue);
    }

    public async Task<IReadOnlyList<EmployeeAwarenessItemDto>> ListEmployeeAssignmentsAsync(
        Guid userId, string filter, CancellationToken ct)
    {
        List<AwarenessCompletion> assignments = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.UserId == userId).ToListAsync(ct);
        if (assignments.Count == 0) return [];

        HashSet<Guid> campaignIds = assignments.Select(x => x.CampaignId).ToHashSet();
        Dictionary<Guid, AwarenessCampaign> campaigns = await db.AwarenessCampaigns.AsNoTracking()
            .Where(x => campaignIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        HashSet<Guid> moduleIds = campaigns.Values.Where(x => x.ModuleId.HasValue).Select(x => x.ModuleId!.Value).ToHashSet();
        Dictionary<Guid, AwarenessModule> modules = await db.AwarenessModules.AsNoTracking()
            .Where(x => moduleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        DateTimeOffset now = clock.UtcNow;
        List<EmployeeAwarenessItemDto> items = [];
        foreach (AwarenessCompletion a in assignments)
        {
            if (!campaigns.TryGetValue(a.CampaignId, out AwarenessCampaign? campaign)) continue;
            if (campaign.Status == AwarenessCampaignStatus.Draft) continue;
            if (campaign.Status == AwarenessCampaignStatus.Closed && a.Status == AwarenessCompletionStatus.Assigned)
                continue;

            AwarenessModule? module = campaign.ModuleId is Guid mid && modules.TryGetValue(mid, out AwarenessModule? m) ? m : null;
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
                            : "Assigned";

            items.Add(new EmployeeAwarenessItemDto(
                a.Id, a.CampaignId, campaign.ModuleId,
                module?.Title ?? campaign.Title,
                module?.Summary ?? campaign.Description,
                module?.EstimatedMinutes ?? 5,
                a.AssignedAtUtc, a.DueAtUtc ?? campaign.DueAtUtc, status,
                a.CompletedAtUtc, a.Score, a.AttemptCount, a.ModuleVersion ?? campaign.ModuleVersion, overdue));
        }

        filter = (filter ?? "outstanding").Trim().ToLowerInvariant();
        return filter switch
        {
            "completed" => items.Where(x => x.Status == "Completed").OrderByDescending(x => x.CompletedAtUtc).ToList(),
            "all" => items.OrderBy(x => x.Status == "Completed").ThenBy(x => x.DueAtUtc).ToList(),
            _ => items.Where(x => x.Status is "Assigned" or "Overdue" or "InProgress")
                .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue).ToList(),
        };
    }

    public async Task<AwarenessModuleDto?> GetAssignmentContentAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        AwarenessCompletion? assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct);
        if (assignment is null) return null;
        AwarenessCampaign campaign = await db.AwarenessCampaigns.AsNoTracking()
            .FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status == AwarenessCampaignStatus.Draft) return null;
        if (campaign.ModuleId is null) return null;

        if (assignment.StartedAtUtc is null && assignment.Status == AwarenessCompletionStatus.Assigned)
        {
            assignment.MarkStarted(clock.UtcNow);
            await businessAudit.AppendAsync(Field(assignment.Id, campaign.Title, "AwarenessStarted", null, userId.ToString()), ct);
            await db.SaveChangesAsync(ct);
        }

        AwarenessModule module = await db.AwarenessModules.AsNoTracking()
            .FirstAsync(x => x.Id == campaign.ModuleId, ct);
        IReadOnlyList<AwarenessModuleDto> mapped = await MapModulesAsync([module], includeCorrectAnswers: false, ct);
        return mapped.FirstOrDefault();
    }

    public async Task<AwarenessQuizSubmitResultDto> SubmitQuizAsync(
        Guid userId, Guid assignmentId, IReadOnlyDictionary<Guid, Guid> answersByQuestionId, CancellationToken ct)
    {
        AwarenessCompletion assignment = await db.AwarenessCompletions
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Assignment not found.");
        if (assignment.Status == AwarenessCompletionStatus.Completed)
        {
            return new AwarenessQuizSubmitResultDto(
                true, assignment.Score ?? 100, assignment.AttemptCount,
                "Already completed.", assignment.CompletedAtUtc);
        }

        AwarenessCampaign campaign = await db.AwarenessCampaigns.FirstAsync(x => x.Id == assignment.CampaignId, ct);
        if (campaign.Status != AwarenessCampaignStatus.Open)
            throw new InvalidOperationException("Campaign is not active.");
        if (campaign.ModuleId is null)
            throw new InvalidOperationException("Campaign has no module.");

        List<AwarenessQuestion> questions = await db.AwarenessQuestions
            .Where(x => x.ModuleId == campaign.ModuleId).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        if (questions.Count == 0)
            throw new InvalidOperationException("Module has no questions.");
        if (answersByQuestionId.Count < questions.Count)
            throw new InvalidOperationException("Answer all questions before submitting.");

        List<Guid> questionIds = questions.Select(x => x.Id).ToList();
        List<AwarenessAnswerOption> options = await db.AwarenessAnswerOptions
            .Where(x => questionIds.Contains(x.QuestionId)).ToListAsync(ct);

        int correct = 0;
        foreach (AwarenessQuestion q in questions)
        {
            if (!answersByQuestionId.TryGetValue(q.Id, out Guid optionId))
                throw new InvalidOperationException("Answer all questions before submitting.");
            AwarenessAnswerOption? selected = options.FirstOrDefault(x => x.Id == optionId && x.QuestionId == q.Id);
            if (selected is null)
                throw new InvalidOperationException("Invalid answer option.");
            if (selected.IsCorrect) correct++;
        }

        int score = (int)Math.Round(100.0 * correct / questions.Count, MidpointRounding.AwayFromZero);
        int threshold = campaign.PassThresholdPercent;
        // For 3 questions prefer all correct when threshold is 80
        bool passed = questions.Count == 3
            ? correct == 3
            : score >= threshold;

        assignment.MarkStarted(clock.UtcNow);
        int attemptNumber = assignment.AttemptCount + 1;
        assignment.RecordAttempt(score, passed, clock.UtcNow);
        db.AwarenessAttempts.Add(AwarenessAttempt.Create(assignment.Id, attemptNumber, score, passed, clock.UtcNow));

        await businessAudit.AppendAsync(Field(
            assignment.Id, campaign.Title, "AwarenessAttemptSubmitted", null, $"{score}:{passed}"), ct);
        if (passed)
        {
            await businessAudit.AppendAsync(Field(
                assignment.Id, campaign.Title, "AwarenessCompleted", null, $"{score}"), ct);
        }

        await db.SaveChangesAsync(ct);
        return new AwarenessQuizSubmitResultDto(
            passed, score, attemptNumber,
            passed ? "Completed. Thank you." : "Review the material and try again.",
            assignment.CompletedAtUtc);
    }

    public async Task<string> ExportCompletionsCsvAsync(Guid campaignId, CancellationToken ct)
    {
        AwarenessCampaign campaign = await db.AwarenessCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == campaignId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        List<AwarenessCompletion> rows = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.CampaignId == campaignId).ToListAsync(ct);
        StringBuilder sb = new();
        sb.AppendLine("Employee,UPN,AssignedAt,DueAt,Status,CompletedAt,Score,Attempts");
        foreach (AwarenessCompletion row in rows.OrderBy(x => x.Status).ThenBy(x => x.UserId))
        {
            ActiveEmployeeInfo? emp = await employees.GetAsync(row.UserId, ct);
            sb.Append(Csv(emp?.DisplayName)).Append(',')
                .Append(Csv(emp?.Upn)).Append(',')
                .Append(Csv(row.AssignedAtUtc.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv((row.DueAtUtc ?? campaign.DueAtUtc)?.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(row.Status.ToString())).Append(',')
                .Append(Csv(row.CompletedAtUtc?.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(row.Score?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
                .Append(row.AttemptCount).AppendLine();
        }

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.ReportExport,
            AggregateId = campaignId,
            BusinessNumber = campaign.Title,
            Action = BusinessAuditAction.Created,
            FieldName = "AwarenessCompletionExport",
            NewValue = rows.Count.ToString(CultureInfo.InvariantCulture),
            Source = AuditSource.Api,
        }, ct);

        return sb.ToString();
    }

    public async Task<IReadOnlyList<AwarenessReminderCandidate>> FindReminderCandidatesAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        List<AwarenessCompletion> outstanding = await db.AwarenessCompletions.AsNoTracking()
            .Where(x => x.Status == AwarenessCompletionStatus.Assigned).ToListAsync(ct);
        HashSet<Guid> campaignIds = outstanding.Select(x => x.CampaignId).ToHashSet();
        Dictionary<Guid, AwarenessCampaign> campaigns = await db.AwarenessCampaigns.AsNoTracking()
            .Where(x => campaignIds.Contains(x.Id) && x.Status == AwarenessCampaignStatus.Open)
            .ToDictionaryAsync(x => x.Id, ct);
        HashSet<(Guid, string)> sent = (await db.AwarenessReminderLogs.AsNoTracking()
            .Select(x => new { x.AssignmentId, x.ReminderKind }).ToListAsync(ct))
            .Select(x => (x.AssignmentId, x.ReminderKind)).ToHashSet();

        List<AwarenessReminderCandidate> due = [];
        foreach (AwarenessCompletion a in outstanding)
        {
            if (!campaigns.TryGetValue(a.CampaignId, out AwarenessCampaign? campaign)) continue;
            DateTimeOffset? dueAt = a.DueAtUtc ?? campaign.DueAtUtc;
            if (dueAt is null) continue;
            double days = (dueAt.Value - now).TotalDays;
            string? kind = days < 0 ? ReminderOverdue : days <= 1 ? ReminderDue1 : days <= 7 ? ReminderDue7 : null;
            if (kind is null || sent.Contains((a.Id, kind))) continue;
            due.Add(new AwarenessReminderCandidate(a.Id, a.UserId, a.CampaignId, campaign.Title, dueAt, kind));
        }

        return due;
    }

    public async Task MarkReminderSentAsync(Guid assignmentId, Guid userId, string reminderKind, CancellationToken ct)
    {
        if (await db.AwarenessReminderLogs.AnyAsync(
                x => x.AssignmentId == assignmentId && x.ReminderKind == reminderKind, ct))
            return;
        db.AwarenessReminderLogs.Add(AwarenessReminderLog.Create(assignmentId, userId, reminderKind, clock.UtcNow));
        await businessAudit.AppendAsync(Field(assignmentId, null, "AwarenessReminderSent", null, reminderKind, AuditSource.Job), ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<AwarenessModuleDto>> MapModulesAsync(
        List<AwarenessModule> modules, bool includeCorrectAnswers, CancellationToken ct)
    {
        if (modules.Count == 0) return [];
        HashSet<Guid> ids = modules.Select(x => x.Id).ToHashSet();
        List<AwarenessQuestion> questions = await db.AwarenessQuestions.AsNoTracking()
            .Where(x => ids.Contains(x.ModuleId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        HashSet<Guid> qids = questions.Select(x => x.Id).ToHashSet();
        List<AwarenessAnswerOption> options = await db.AwarenessAnswerOptions.AsNoTracking()
            .Where(x => qids.Contains(x.QuestionId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);

        return modules.Select(m =>
        {
            List<AwarenessQuestionDto> qs = questions.Where(q => q.ModuleId == m.Id).Select(q =>
                new AwarenessQuestionDto(
                    q.Id, q.QuestionText, q.DisplayOrder,
                    options.Where(o => o.QuestionId == q.Id).Select(o =>
                        new AwarenessAnswerOptionDto(
                            o.Id, o.Text, o.DisplayOrder, includeCorrectAnswers ? o.IsCorrect : null)).ToList())).ToList();
            return new AwarenessModuleDto(
                m.Id, m.Code, m.Title, m.Summary, m.Body, m.Version, m.Status.ToString(),
                m.EstimatedMinutes, m.PassThresholdPercent, m.CreatedAtUtc, m.UpdatedAtUtc, qs);
        }).ToList();
    }

    private static BusinessAuditEntry Field(
        Guid id, string? number, string field, string? oldValue, string? newValue,
        AuditSource source = AuditSource.Api) => new()
    {
        AggregateType = AuditAggregateType.Risk,
        AggregateId = id,
        BusinessNumber = number,
        Action = BusinessAuditAction.Updated,
        FieldName = field,
        OldValue = oldValue,
        NewValue = newValue,
        Source = source,
    };

    private static string Csv(string? value)
    {
        string v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }

    private static IEnumerable<(string Code, string Title, string Summary, string Body, int Minutes, int PassPercent,
        (string Text, (string Text, bool Correct)[] Answers)[] Questions)> StarterTemplates()
    {
        yield return (
            "SEC-AWARE-PHISHING",
            "Phishing & Suspicious Emails",
            "Spot and report suspicious messages safely.",
            """
            What is phishing?
            Phishing emails try to trick you into sharing passwords, codes, or private information.

            How to spot it
            - Unexpected urgent requests for money, passwords, or codes
            - Strange sender addresses or display names
            - Links that look almost right but not quite
            - Attachments you did not ask for

            What to do
            - Do not click suspicious links or open unexpected attachments
            - Never share passwords or one-time codes
            - Report the message using Report a Security Concern
            - When unsure, ask IT Security

            Fake login pages
            Stop if a page asks for your password after a link from email. Go to the official site yourself instead.
            """,
            5, 80,
            [
                ("You receive an urgent email asking for your password. What should you do?",
                [
                    ("Send the password so work can continue", false),
                    ("Ignore and delete without reporting", false),
                    ("Do not share the password and report it as suspicious", true),
                ]),
                ("A link looks almost like the company site but the address is slightly wrong. What is safest?",
                [
                    ("Click and check if the page looks real", false),
                    ("Do not click; open the official site yourself and report the email", true),
                    ("Forward it to friends for advice", false),
                ]),
                ("Someone asks you for your SMS or authenticator code. What should you do?",
                [
                    ("Share it if they say they are from IT", false),
                    ("Never share codes; report the request", true),
                    ("Share it only once", false),
                ]),
            ]);

        yield return (
            "SEC-AWARE-PASSWORDS",
            "Passwords, MFA & Account Security",
            "Protect your account with strong habits.",
            """
            Strong account habits
            - Use unique passwords or an approved password manager
            - Turn on multi-factor authentication (MFA) where available
            - Never share passwords or MFA codes
            - Lock your screen when you step away

            Warning signs
            - Unexpected login prompts
            - Password reset emails you did not start
            - Someone asking you to disable MFA

            If you think your account is compromised
            Change your password using the approved company process and report a security concern immediately.
            """,
            5, 80,
            [
                ("Should you reuse the same password across work systems?",
                [
                    ("Yes, it is easier to remember", false),
                    ("No, use unique passwords or an approved manager", true),
                    ("Only for low-importance systems", false),
                ]),
                ("Someone on the phone asks for your MFA code. What should you do?",
                [
                    ("Give it if they sound official", false),
                    ("Refuse and report the request", true),
                    ("Give half the code", false),
                ]),
                ("You see a login prompt you did not expect. What is safest?",
                [
                    ("Enter your password to see what happens", false),
                    ("Stop, close it, and report if it feels wrong", true),
                    ("Disable MFA so it stops", false),
                ]),
            ]);

        yield return (
            "SEC-AWARE-DATA",
            "Protecting Company Information",
            "Handle company and personal data carefully.",
            """
            Protect information
            - Share only with people who need it for work
            - Use approved storage and sharing tools
            - Check recipients before sending email or files
            - Do not send confidential data to personal accounts

            Accidental disclosure
            If you sent information to the wrong person, report it quickly. Fast reporting helps reduce harm.
            """,
            4, 80,
            [
                ("You need to send a confidential file. What is best?",
                [
                    ("Use personal email so it is faster", false),
                    ("Use approved company tools and verify recipients", true),
                    ("Post it in a public chat", false),
                ]),
                ("You emailed the wrong person by mistake. What should you do?",
                [
                    ("Hope they delete it and say nothing", false),
                    ("Report it as a security concern right away", true),
                    ("Wait a week to see if anything happens", false),
                ]),
                ("Confidential data should usually be stored:",
                [
                    ("On any cloud account you like", false),
                    ("In approved company systems", true),
                    ("On a USB stick left on the desk", false),
                ]),
            ]);

        yield return (
            "SEC-AWARE-DEVICES",
            "Safe Use of Company Devices",
            "Keep company devices secure day to day.",
            """
            Device basics
            - Lock your screen when you leave
            - Install updates when prompted
            - Do not install unauthorized software
            - Do not use unknown USB devices

            Lost or stolen device
            Report immediately using Report a Security Concern so IT can protect company data.

            Public or shared computers
            Avoid signing into company accounts on shared/public devices when possible.
            """,
            4, 80,
            [
                ("You leave your desk for a meeting. What should you do?",
                [
                    ("Leave the screen unlocked for convenience", false),
                    ("Lock the screen", true),
                    ("Hide the keyboard", false),
                ]),
                ("Your company laptop is missing. What should you do first?",
                [
                    ("Wait a few days", false),
                    ("Report a security concern immediately", true),
                    ("Post on social media", false),
                ]),
                ("A free USB stick is left in a public place. What is safest?",
                [
                    ("Plug it into your work PC to check contents", false),
                    ("Do not plug it in; report if needed", true),
                    ("Use it for personal files only", false),
                ]),
            ]);

        yield return (
            "SEC-AWARE-REMOTE",
            "Remote Work & Remote Support Safety",
            "Stay safe when working remotely or allowing IT support.",
            """
            Remote work
            - Prefer trusted networks; use required VPN when instructed
            - Keep conversations private in public places
            - Protect screens from shoulder surfing

            Remote support
            - Allow remote support only through official ITMG consent
            - Verify the request and technician context
            - End the session when finished
            - Report unexpected remote access requests

            Never give remote control to strangers who contact you unexpectedly.
            """,
            5, 80,
            [
                ("Someone emails you asking to install remote software right now. What should you do?",
                [
                    ("Install it so they can help", false),
                    ("Do not install; use official IT channels and report if suspicious", true),
                    ("Share your password so they can connect", false),
                ]),
                ("When IT asks for remote support in ITMG, you should:",
                [
                    ("Allow only after reviewing the consent request", true),
                    ("Always click Allow without reading", false),
                    ("Share your MFA codes in chat", false),
                ]),
                ("On public Wi-Fi for sensitive work, you should:",
                [
                    ("Ignore network risk", false),
                    ("Follow company guidance such as VPN when required", true),
                    ("Disable device updates", false),
                ]),
            ]);
    }
}
