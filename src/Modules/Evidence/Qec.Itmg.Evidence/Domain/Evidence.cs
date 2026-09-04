namespace Qec.Itmg.Evidence.Domain;

public enum EvidenceSourceType
{
    Manual = 0,
    Ticket = 1,
    Change = 2,
    AccessReview = 3,
    DrTest = 4,
    BackupRestore = 5,
    Export = 6,
    Other = 7,
}

public enum EvidenceType
{
    Screenshot = 0,
    Report = 1,
    Approval = 2,
    Configuration = 3,
    Log = 4,
    TestResult = 5,
    Document = 6,
    Export = 7,
    Other = 8,
}

public enum EvidenceClassification
{
    Internal = 0,
    Confidential = 1,
    Restricted = 2,
}

public enum EvidenceStatus
{
    Draft = 0,
    Submitted = 1,
    Accepted = 2,
    Expired = 3,
    Superseded = 4,
    Withdrawn = 5,
}

public enum EvidenceLinkTargetType
{
    InternalControl = 0,
    ControlAssessment = 1,
    FrameworkRequirement = 2,
    ManagedDocument = 3,
    Ticket = 4,
    ChangeRequest = 5,
    RestoreTest = 6,
    AccessReviewCampaign = 7,
}

public sealed class EvidenceRecord
{
    private EvidenceRecord() { }

    public Guid Id { get; private set; }
    public string EvidenceNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public EvidenceSourceType SourceType { get; private set; }
    public Guid? SourceRecordId { get; private set; }
    public EvidenceType EvidenceType { get; private set; }
    public EvidenceClassification Classification { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
    public EvidenceStatus Status { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public string? WithdrawalReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public int? DaysToExpiry(DateTimeOffset utcNow) =>
        ValidTo is null ? null : (int)Math.Floor((ValidTo.Value - utcNow).TotalDays);

    public bool IsExpired(DateTimeOffset utcNow) =>
        Status == EvidenceStatus.Expired
        || (ValidTo is DateTimeOffset d && d < utcNow && Status == EvidenceStatus.Accepted);

    public bool IsExpiringSoon(DateTimeOffset utcNow, int withinDays = 30) =>
        Status == EvidenceStatus.Accepted
        && ValidTo is DateTimeOffset d
        && !IsExpired(utcNow)
        && DaysToExpiry(utcNow) <= withinDays;

    public static EvidenceRecord Create(
        string evidenceNumber,
        string title,
        Guid ownerUserId,
        EvidenceSourceType sourceType,
        EvidenceType evidenceType,
        EvidenceClassification classification,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset utcNow,
        string? description = null,
        Guid? sourceRecordId = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.", nameof(ownerUserId));
        return new EvidenceRecord
        {
            Id = Guid.CreateVersion7(),
            EvidenceNumber = evidenceNumber.Trim(),
            Title = title.Trim(),
            Description = TrimOrNull(description),
            OwnerUserId = ownerUserId,
            SourceType = sourceType,
            SourceRecordId = sourceRecordId == Guid.Empty ? null : sourceRecordId,
            EvidenceType = evidenceType,
            Classification = classification,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CapturedAtUtc = capturedAtUtc,
            Status = EvidenceStatus.Draft,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateMetadata(
        string title, string? description, EvidenceType evidenceType, EvidenceClassification classification,
        DateTimeOffset? validFrom, DateTimeOffset? validTo, DateTimeOffset utcNow)
    {
        if (Status is EvidenceStatus.Accepted or EvidenceStatus.Expired or EvidenceStatus.Superseded or EvidenceStatus.Withdrawn)
            throw new InvalidOperationException("Cannot edit metadata in current status.");
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Description = TrimOrNull(description);
        EvidenceType = evidenceType;
        Classification = classification;
        ValidFrom = validFrom;
        ValidTo = validTo;
        UpdatedAtUtc = utcNow;
    }

    public void SetCurrentVersion(Guid versionId, DateTimeOffset utcNow)
    {
        CurrentVersionId = versionId;
        UpdatedAtUtc = utcNow;
    }

    public void Submit(DateTimeOffset utcNow)
    {
        if (Status != EvidenceStatus.Draft)
            throw new InvalidOperationException("Only draft evidence can be submitted.");
        if (CurrentVersionId is null)
            throw new InvalidOperationException("Attach a file version before submitting.");
        Status = EvidenceStatus.Submitted;
        UpdatedAtUtc = utcNow;
    }

    public void ReturnToDraft(DateTimeOffset utcNow)
    {
        if (Status != EvidenceStatus.Submitted)
            throw new InvalidOperationException("Only submitted evidence can be returned to draft.");
        Status = EvidenceStatus.Draft;
        UpdatedAtUtc = utcNow;
    }

    public void Accept(Guid acceptorUserId, DateTimeOffset utcNow)
    {
        if (Status != EvidenceStatus.Submitted)
            throw new InvalidOperationException("Only submitted evidence can be accepted.");
        if (acceptorUserId == Guid.Empty) throw new ArgumentException("Acceptor required.", nameof(acceptorUserId));
        if (acceptorUserId == OwnerUserId)
            throw new InvalidOperationException("Uploader cannot accept their own evidence.");
        Status = EvidenceStatus.Accepted;
        AcceptedByUserId = acceptorUserId;
        AcceptedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void MarkExpired(DateTimeOffset utcNow)
    {
        if (Status != EvidenceStatus.Accepted) return;
        Status = EvidenceStatus.Expired;
        UpdatedAtUtc = utcNow;
    }

    public void MarkSuperseded(DateTimeOffset utcNow)
    {
        if (Status is EvidenceStatus.Withdrawn or EvidenceStatus.Superseded) return;
        Status = EvidenceStatus.Superseded;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Opens a new draft cycle on the same evidence after an accepted version; prior versions remain immutable.</summary>
    public void StartRevision(DateTimeOffset utcNow)
    {
        if (Status is not (EvidenceStatus.Accepted or EvidenceStatus.Expired))
            throw new InvalidOperationException("Only accepted or expired evidence can start a revision.");
        Status = EvidenceStatus.Draft;
        AcceptedByUserId = null;
        AcceptedAtUtc = null;
        UpdatedAtUtc = utcNow;
    }

    public void Withdraw(string reason, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status is EvidenceStatus.Withdrawn or EvidenceStatus.Superseded)
            throw new InvalidOperationException("Evidence cannot be withdrawn in current status.");
        Status = EvidenceStatus.Withdrawn;
        WithdrawalReason = reason.Trim();
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class EvidenceVersion
{
    private EvidenceVersion() { }

    public Guid Id { get; private set; }
    public Guid EvidenceId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid AttachmentId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? ChangeSummary { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }

    public static EvidenceVersion Create(
        Guid evidenceId, int versionNumber, Guid attachmentId, Guid createdByUserId,
        DateTimeOffset utcNow, string? changeSummary = null, Guid? supersedesVersionId = null)
    {
        if (evidenceId == Guid.Empty) throw new ArgumentException("Evidence required.", nameof(evidenceId));
        if (attachmentId == Guid.Empty) throw new ArgumentException("Attachment required.", nameof(attachmentId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("User required.", nameof(createdByUserId));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        return new EvidenceVersion
        {
            Id = Guid.CreateVersion7(),
            EvidenceId = evidenceId,
            VersionNumber = versionNumber,
            AttachmentId = attachmentId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
            ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? null : changeSummary.Trim(),
            SupersedesVersionId = supersedesVersionId == Guid.Empty ? null : supersedesVersionId,
        };
    }
}

public sealed class EvidenceLink
{
    private EvidenceLink() { }

    public Guid Id { get; private set; }
    public Guid EvidenceId { get; private set; }
    public EvidenceLinkTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public static EvidenceLink Create(
        Guid evidenceId, EvidenceLinkTargetType targetType, Guid targetId, Guid createdByUserId, DateTimeOffset utcNow)
    {
        if (evidenceId == Guid.Empty) throw new ArgumentException("Evidence required.", nameof(evidenceId));
        if (targetId == Guid.Empty) throw new ArgumentException("Target required.", nameof(targetId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("User required.", nameof(createdByUserId));
        return new EvidenceLink
        {
            Id = Guid.CreateVersion7(),
            EvidenceId = evidenceId,
            TargetType = targetType,
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class EvidenceExpiryNotificationLog
{
    private EvidenceExpiryNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid EvidenceId { get; private set; }
    public DateTimeOffset ValidToUtc { get; private set; }
    public int ThresholdDays { get; private set; }
    public DateTimeOffset NotifiedAtUtc { get; private set; }

    public static EvidenceExpiryNotificationLog Create(
        Guid evidenceId, DateTimeOffset validToUtc, int thresholdDays, DateTimeOffset utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        EvidenceId = evidenceId,
        ValidToUtc = validToUtc,
        ThresholdDays = thresholdDays,
        NotifiedAtUtc = utcNow,
    };
}
