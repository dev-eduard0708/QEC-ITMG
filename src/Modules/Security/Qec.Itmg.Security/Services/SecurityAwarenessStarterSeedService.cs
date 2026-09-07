using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Security.Domain;
using Qec.Itmg.Security.Persistence;

namespace Qec.Itmg.Security.Services;

/// <summary>
/// Idempotent production-safe bootstrap of Head Office Security Awareness starter Draft campaigns.
/// Never launches, assigns, notifies, or overwrites an existing StarterKey campaign.
/// </summary>
public sealed class SecurityAwarenessStarterSeedService(
    SecurityDbContext db,
    IClock clock,
    INumberSequenceService numbers,
    IBusinessAuditWriter businessAudit,
    ILogger<SecurityAwarenessStarterSeedService> logger)
{
    /// <summary>Stable actor id for catalog seed writes (not a real employee).</summary>
    public static readonly Guid SeedActorId = Guid.Parse("01990000-0000-7000-8000-0000000000a1");

    public async Task EnsureStarterCampaignsAsync(CancellationToken ct = default)
    {
        int created = 0;
        int skipped = 0;
        foreach (StarterCampaignSeed seed in SecurityAwarenessStarterCatalog.All)
        {
            bool exists = await db.AwarenessCampaigns.AsNoTracking()
                .AnyAsync(x => x.StarterKey == seed.StarterKey, ct);
            if (exists)
            {
                skipped++;
                continue;
            }

            await CreateStarterDraftAsync(seed, ct);
            created++;
        }

        logger.LogInformation(
            "Security Awareness starter campaigns seed complete. Created={Created}, SkippedExisting={Skipped}.",
            created, skipped);
    }

    private async Task CreateStarterDraftAsync(StarterCampaignSeed seed, CancellationToken ct)
    {
        string number = await numbers.NextAsync("security-awareness", "SA", ct);
        DateTimeOffset utcNow = clock.UtcNow;
        AwarenessCampaign campaign = AwarenessCampaign.CreateDraft(
            number,
            seed.TitleEn,
            SeedActorId,
            SeedActorId,
            utcNow,
            seed.TitleAr,
            seed.DescriptionEn,
            seed.DescriptionAr,
            startAtUtc: null,
            dueAtUtc: null,
            requireQuiz: true,
            passingScorePercent: 80,
            allowRetry: true,
            maxAttempts: null,
            requireCompletion: true,
            starterKey: seed.StarterKey);

        db.AwarenessCampaigns.Add(campaign);

        int sort = 1;
        foreach (StarterBlockSeed block in seed.Blocks)
        {
            db.AwarenessContentBlocks.Add(AwarenessContentBlock.CreateDraft(
                campaign.Id,
                sort++,
                AwarenessContentType.Text,
                block.TitleEn,
                block.TitleAr,
                block.BodyEn,
                block.BodyAr,
                url: null,
                documentId: null,
                estimatedMinutes: block.EstimatedMinutes));
        }

        int qSort = 1;
        foreach (StarterQuestionSeed questionSeed in seed.Questions)
        {
            AwarenessCampaignQuestion question = AwarenessCampaignQuestion.CreateDraft(
                campaign.Id,
                questionSeed.Type,
                questionSeed.QuestionEn,
                questionSeed.QuestionAr,
                questionSeed.ExplanationEn,
                questionSeed.ExplanationAr,
                questionSeed.Points,
                qSort++);
            db.AwarenessCampaignQuestions.Add(question);

            int oSort = 1;
            foreach (StarterOptionSeed option in questionSeed.Options)
            {
                db.AwarenessCampaignQuestionOptions.Add(AwarenessCampaignQuestionOption.Create(
                    question.Id,
                    option.TextEn,
                    option.TextAr,
                    option.IsCorrect,
                    oSort++));
            }
        }

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Risk,
            AggregateId = campaign.Id,
            BusinessNumber = campaign.Number,
            Action = BusinessAuditAction.Created,
            FieldName = "AwarenessStarterCampaignSeeded",
            NewValue = seed.StarterKey,
            Source = AuditSource.Job,
        }, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded Security Awareness starter Draft {StarterKey} as {Number}.",
            seed.StarterKey, campaign.Number);
    }
}
