namespace Qec.Itmg.Security.Domain;

public enum AwarenessContentType
{
    Text = 0,
    ExternalLink = 1,
    VideoLink = 2,
    DocumentReference = 3,
}

public enum AwarenessAudienceRuleType
{
    AllHeadOffice = 0,
    Department = 1,
    Position = 2,
    SpecificUser = 3,
}

public enum AwarenessQuestionType
{
    SingleChoice = 0,
    MultipleChoice = 1,
    TrueFalse = 2,
}

public sealed class AwarenessCampaignVersion
{
    private AwarenessCampaignVersion() { }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public int VersionNumber { get; private set; }
    public string TitleEn { get; private set; } = null!;
    public string? TitleAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public Guid PublishedByUserId { get; private set; }
    public bool RequireQuiz { get; private set; }
    public int PassingScorePercent { get; private set; }
    public bool AllowRetry { get; private set; }
    public int? MaxAttempts { get; private set; }
    public int EstimatedMinutes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AwarenessCampaignVersion Create(
        Guid campaignId,
        int versionNumber,
        string titleEn,
        string? titleAr,
        string? descriptionEn,
        string? descriptionAr,
        Guid publishedByUserId,
        bool requireQuiz,
        int passingScorePercent,
        bool allowRetry,
        int? maxAttempts,
        int estimatedMinutes,
        DateTimeOffset utcNow)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign required.");
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(titleEn);
        if (publishedByUserId == Guid.Empty) throw new ArgumentException("Publisher required.");
        if (passingScorePercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(passingScorePercent));
        if (estimatedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(estimatedMinutes));
        return new AwarenessCampaignVersion
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            VersionNumber = versionNumber,
            TitleEn = titleEn.Trim(),
            TitleAr = TrimOrNull(titleAr),
            DescriptionEn = TrimOrNull(descriptionEn),
            DescriptionAr = TrimOrNull(descriptionAr),
            PublishedAtUtc = utcNow,
            PublishedByUserId = publishedByUserId,
            RequireQuiz = requireQuiz,
            PassingScorePercent = passingScorePercent,
            AllowRetry = allowRetry,
            MaxAttempts = maxAttempts,
            EstimatedMinutes = estimatedMinutes,
            CreatedAtUtc = utcNow,
        };
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AwarenessContentBlock
{
    private AwarenessContentBlock() { }

    public Guid Id { get; private set; }
    public Guid? CampaignId { get; private set; }
    public Guid? CampaignVersionId { get; private set; }
    public int SortOrder { get; private set; }
    public AwarenessContentType ContentType { get; private set; }
    public string TitleEn { get; private set; } = null!;
    public string? TitleAr { get; private set; }
    public string? BodyEn { get; private set; }
    public string? BodyAr { get; private set; }
    public string? Url { get; private set; }
    public Guid? DocumentId { get; private set; }
    public int? EstimatedMinutes { get; private set; }

    public static AwarenessContentBlock CreateDraft(
        Guid campaignId,
        int sortOrder,
        AwarenessContentType contentType,
        string titleEn,
        string? titleAr,
        string? bodyEn,
        string? bodyAr,
        string? url,
        Guid? documentId,
        int? estimatedMinutes)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign required.");
        return CreateCore(campaignId, null, sortOrder, contentType, titleEn, titleAr, bodyEn, bodyAr, url, documentId, estimatedMinutes);
    }

    public static AwarenessContentBlock CreateVersionCopy(
        Guid campaignVersionId,
        AwarenessContentBlock source) =>
        CreateCore(
            null,
            campaignVersionId,
            source.SortOrder,
            source.ContentType,
            source.TitleEn,
            source.TitleAr,
            source.BodyEn,
            source.BodyAr,
            source.Url,
            source.DocumentId,
            source.EstimatedMinutes);

    private static AwarenessContentBlock CreateCore(
        Guid? campaignId,
        Guid? campaignVersionId,
        int sortOrder,
        AwarenessContentType contentType,
        string titleEn,
        string? titleAr,
        string? bodyEn,
        string? bodyAr,
        string? url,
        Guid? documentId,
        int? estimatedMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleEn);
        ValidateContent(contentType, bodyEn, url, documentId);
        return new AwarenessContentBlock
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            CampaignVersionId = campaignVersionId,
            SortOrder = sortOrder,
            ContentType = contentType,
            TitleEn = titleEn.Trim(),
            TitleAr = TrimOrNull(titleAr),
            BodyEn = TrimOrNull(bodyEn),
            BodyAr = TrimOrNull(bodyAr),
            Url = TrimOrNull(url),
            DocumentId = documentId == Guid.Empty ? null : documentId,
            EstimatedMinutes = estimatedMinutes,
        };
    }

    public static void ValidateContent(
        AwarenessContentType contentType, string? bodyEn, string? url, Guid? documentId)
    {
        switch (contentType)
        {
            case AwarenessContentType.Text:
                if (string.IsNullOrWhiteSpace(bodyEn))
                    throw new InvalidOperationException("Text content requires BodyEn.");
                break;
            case AwarenessContentType.ExternalLink:
            case AwarenessContentType.VideoLink:
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
                    throw new InvalidOperationException($"{contentType} requires a valid absolute URL.");
                break;
            case AwarenessContentType.DocumentReference:
                if (documentId is null || documentId == Guid.Empty)
                    throw new InvalidOperationException("DocumentReference requires DocumentId.");
                break;
            default:
                throw new InvalidOperationException($"Unsupported content type {contentType}.");
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AwarenessAudienceRule
{
    private AwarenessAudienceRule() { }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public AwarenessAudienceRuleType RuleType { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? PositionId { get; private set; }
    public Guid? UserId { get; private set; }

    public static AwarenessAudienceRule CreateAllHeadOffice(Guid campaignId) =>
        Create(campaignId, AwarenessAudienceRuleType.AllHeadOffice, null, null, null);

    public static AwarenessAudienceRule CreateDepartment(Guid campaignId, Guid departmentId) =>
        Create(campaignId, AwarenessAudienceRuleType.Department, departmentId, null, null);

    public static AwarenessAudienceRule CreatePosition(Guid campaignId, Guid positionId) =>
        Create(campaignId, AwarenessAudienceRuleType.Position, null, positionId, null);

    public static AwarenessAudienceRule CreateSpecificUser(Guid campaignId, Guid userId) =>
        Create(campaignId, AwarenessAudienceRuleType.SpecificUser, null, null, userId);

    private static AwarenessAudienceRule Create(
        Guid campaignId,
        AwarenessAudienceRuleType ruleType,
        Guid? departmentId,
        Guid? positionId,
        Guid? userId)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign required.");
        return ruleType switch
        {
            AwarenessAudienceRuleType.AllHeadOffice => new AwarenessAudienceRule
            {
                Id = Guid.CreateVersion7(),
                CampaignId = campaignId,
                RuleType = ruleType,
            },
            AwarenessAudienceRuleType.Department when departmentId is Guid d && d != Guid.Empty => new AwarenessAudienceRule
            {
                Id = Guid.CreateVersion7(),
                CampaignId = campaignId,
                RuleType = ruleType,
                DepartmentId = d,
            },
            AwarenessAudienceRuleType.Position when positionId is Guid p && p != Guid.Empty => new AwarenessAudienceRule
            {
                Id = Guid.CreateVersion7(),
                CampaignId = campaignId,
                RuleType = ruleType,
                PositionId = p,
            },
            AwarenessAudienceRuleType.SpecificUser when userId is Guid u && u != Guid.Empty => new AwarenessAudienceRule
            {
                Id = Guid.CreateVersion7(),
                CampaignId = campaignId,
                RuleType = ruleType,
                UserId = u,
            },
            _ => throw new ArgumentException("Invalid audience rule."),
        };
    }
}

public sealed class AwarenessCampaignQuestion
{
    private AwarenessCampaignQuestion() { }

    public Guid Id { get; private set; }
    public Guid? CampaignId { get; private set; }
    public Guid? CampaignVersionId { get; private set; }
    public AwarenessQuestionType Type { get; private set; }
    public string QuestionEn { get; private set; } = null!;
    public string? QuestionAr { get; private set; }
    public string? ExplanationEn { get; private set; }
    public string? ExplanationAr { get; private set; }
    public int Points { get; private set; }
    public int SortOrder { get; private set; }

    public static AwarenessCampaignQuestion CreateDraft(
        Guid campaignId,
        AwarenessQuestionType type,
        string questionEn,
        string? questionAr,
        string? explanationEn,
        string? explanationAr,
        int points,
        int sortOrder)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("Campaign required.");
        return CreateCore(campaignId, null, type, questionEn, questionAr, explanationEn, explanationAr, points, sortOrder);
    }

    public static AwarenessCampaignQuestion CreateVersionCopy(
        Guid campaignVersionId,
        AwarenessCampaignQuestion source) =>
        CreateCore(
            null,
            campaignVersionId,
            source.Type,
            source.QuestionEn,
            source.QuestionAr,
            source.ExplanationEn,
            source.ExplanationAr,
            source.Points,
            source.SortOrder);

    private static AwarenessCampaignQuestion CreateCore(
        Guid? campaignId,
        Guid? campaignVersionId,
        AwarenessQuestionType type,
        string questionEn,
        string? questionAr,
        string? explanationEn,
        string? explanationAr,
        int points,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionEn);
        if (points < 1) throw new ArgumentOutOfRangeException(nameof(points));
        return new AwarenessCampaignQuestion
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            CampaignVersionId = campaignVersionId,
            Type = type,
            QuestionEn = questionEn.Trim(),
            QuestionAr = TrimOrNull(questionAr),
            ExplanationEn = TrimOrNull(explanationEn),
            ExplanationAr = TrimOrNull(explanationAr),
            Points = points,
            SortOrder = sortOrder,
        };
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AwarenessCampaignQuestionOption
{
    private AwarenessCampaignQuestionOption() { }

    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public string TextEn { get; private set; } = null!;
    public string? TextAr { get; private set; }
    public bool IsCorrect { get; private set; }
    public int SortOrder { get; private set; }

    public static AwarenessCampaignQuestionOption Create(
        Guid questionId, string textEn, string? textAr, bool isCorrect, int sortOrder)
    {
        if (questionId == Guid.Empty) throw new ArgumentException("Question required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(textEn);
        return new AwarenessCampaignQuestionOption
        {
            Id = Guid.CreateVersion7(),
            QuestionId = questionId,
            TextEn = textEn.Trim(),
            TextAr = string.IsNullOrWhiteSpace(textAr) ? null : textAr.Trim(),
            IsCorrect = isCorrect,
            SortOrder = sortOrder,
        };
    }
}

public sealed class AwarenessQuizAnswer
{
    private AwarenessQuizAnswer() { }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string SelectedOptionIdsJson { get; private set; } = "[]";
    public bool IsCorrect { get; private set; }

    public static AwarenessQuizAnswer Create(
        Guid attemptId, Guid questionId, IReadOnlyList<Guid> selectedOptionIds, bool isCorrect)
    {
        if (attemptId == Guid.Empty) throw new ArgumentException("Attempt required.");
        if (questionId == Guid.Empty) throw new ArgumentException("Question required.");
        string json = System.Text.Json.JsonSerializer.Serialize(
            (selectedOptionIds ?? []).Where(x => x != Guid.Empty).Distinct().ToArray());
        return new AwarenessQuizAnswer
        {
            Id = Guid.CreateVersion7(),
            AttemptId = attemptId,
            QuestionId = questionId,
            SelectedOptionIdsJson = json,
            IsCorrect = isCorrect,
        };
    }
}
