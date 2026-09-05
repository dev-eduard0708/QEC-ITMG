namespace Qec.Itmg.DocumentManagement.Domain;

public enum DocumentType
{
    Policy = 0,
    Procedure = 1,
    Standard = 2,
    Guideline = 3,
    Template = 4,
    Diagram = 5,
}

public enum DocumentClassification
{
    Internal = 0,
    Confidential = 1,
    Restricted = 2,
}

public enum DocumentStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Published = 3,
    Superseded = 4,
    Retired = 5,
}

public sealed class ManagedDocument
{
    private ManagedDocument() { }

    public Guid Id { get; private set; }
    public string DocumentNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public DocumentType DocumentType { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? DesignatedApproverUserId { get; private set; }
    public DocumentClassification Classification { get; private set; }
    public DocumentStatus Status { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public DateTimeOffset? EffectiveDate { get; private set; }
    public DateTimeOffset? ReviewDate { get; private set; }
    public bool RequiresAcknowledgement { get; private set; }
    public bool RequireReAcknowledgement { get; private set; }
    public string? RetirementReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public int? DaysToReview(DateTimeOffset utcNow) =>
        ReviewDate is null ? null : (int)Math.Floor((ReviewDate.Value - utcNow).TotalDays);

    public bool IsReviewOverdue(DateTimeOffset utcNow) =>
        ReviewDate is DateTimeOffset d && d < utcNow && Status == DocumentStatus.Published;

    public bool IsReviewDueSoon(DateTimeOffset utcNow, int withinDays = 30) =>
        ReviewDate is DateTimeOffset d
        && Status == DocumentStatus.Published
        && !IsReviewOverdue(utcNow)
        && DaysToReview(utcNow) <= withinDays;

    public static ManagedDocument Create(
        string documentNumber,
        string title,
        DocumentType documentType,
        Guid ownerUserId,
        DocumentClassification classification,
        DateTimeOffset utcNow,
        Guid? designatedApproverUserId = null,
        DateTimeOffset? effectiveDate = null,
        DateTimeOffset? reviewDate = null,
        bool requiresAcknowledgement = false,
        bool requireReAcknowledgement = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerUserId));
        return new ManagedDocument
        {
            Id = Guid.CreateVersion7(),
            DocumentNumber = documentNumber.Trim(),
            Title = title.Trim(),
            DocumentType = documentType,
            OwnerUserId = ownerUserId,
            DesignatedApproverUserId = Norm(designatedApproverUserId),
            Classification = classification,
            Status = DocumentStatus.Draft,
            EffectiveDate = effectiveDate,
            ReviewDate = reviewDate,
            RequiresAcknowledgement = requiresAcknowledgement,
            RequireReAcknowledgement = requireReAcknowledgement,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateMetadata(
        string title,
        Guid ownerUserId,
        Guid? designatedApproverUserId,
        DocumentClassification classification,
        DateTimeOffset? effectiveDate,
        DateTimeOffset? reviewDate,
        bool requiresAcknowledgement,
        bool requireReAcknowledgement,
        DateTimeOffset utcNow)
    {
        if (Status is DocumentStatus.Retired or DocumentStatus.Superseded)
            throw new InvalidOperationException("Cannot edit a terminal document.");
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner is required.", nameof(ownerUserId));
        Title = title.Trim();
        OwnerUserId = ownerUserId;
        DesignatedApproverUserId = Norm(designatedApproverUserId);
        Classification = classification;
        EffectiveDate = effectiveDate;
        ReviewDate = reviewDate;
        RequiresAcknowledgement = requiresAcknowledgement;
        RequireReAcknowledgement = requireReAcknowledgement;
        UpdatedAtUtc = utcNow;
    }

    public void SetCurrentVersion(Guid versionId, DateTimeOffset utcNow)
    {
        CurrentVersionId = versionId;
        UpdatedAtUtc = utcNow;
    }

    public void SetEffectiveDateIfMissing(DateTimeOffset effectiveDate, DateTimeOffset utcNow)
    {
        if (EffectiveDate is not null) return;
        EffectiveDate = effectiveDate;
        UpdatedAtUtc = utcNow;
    }

    public void TransitionTo(DocumentStatus next, DateTimeOffset utcNow, string? retirementReason = null)
    {
        if (Status == next) return;
        if (!IsAllowed(Status, next))
            throw new InvalidOperationException($"Cannot transition from {Status} to {next}.");
        if (next == DocumentStatus.Retired)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(retirementReason);
            RetirementReason = retirementReason.Trim();
        }

        Status = next;
        UpdatedAtUtc = utcNow;
    }

    public static bool IsAllowed(DocumentStatus from, DocumentStatus to) => (from, to) switch
    {
        (DocumentStatus.Draft, DocumentStatus.InReview) => true,
        (DocumentStatus.Draft, DocumentStatus.Retired) => true,
        (DocumentStatus.InReview, DocumentStatus.Approved) => true,
        (DocumentStatus.InReview, DocumentStatus.Draft) => true,
        (DocumentStatus.Approved, DocumentStatus.Published) => true,
        (DocumentStatus.Approved, DocumentStatus.Draft) => true,
        (DocumentStatus.Published, DocumentStatus.Draft) => true,
        (DocumentStatus.Published, DocumentStatus.Superseded) => true,
        (DocumentStatus.Published, DocumentStatus.Retired) => true,
        _ => false,
    };

    private static Guid? Norm(Guid? v) => v is null || v == Guid.Empty ? null : v;
}

public sealed class DocumentVersion
{
    private DocumentVersion() { }

    public Guid Id { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? ChangeSummary { get; private set; }
    public string? ContentText { get; private set; }
    public Guid? AttachmentId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }

    public static DocumentVersion Create(
        Guid managedDocumentId,
        int versionNumber,
        Guid createdByUserId,
        DateTimeOffset utcNow,
        string? changeSummary = null,
        Guid? attachmentId = null,
        Guid? supersedesVersionId = null,
        string? contentText = null)
    {
        if (managedDocumentId == Guid.Empty) throw new ArgumentException("Document is required.", nameof(managedDocumentId));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdByUserId));
        return new DocumentVersion
        {
            Id = Guid.CreateVersion7(),
            ManagedDocumentId = managedDocumentId,
            VersionNumber = versionNumber,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
            ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? null : changeSummary.Trim(),
            ContentText = string.IsNullOrWhiteSpace(contentText) ? null : contentText.Trim(),
            AttachmentId = attachmentId is null || attachmentId == Guid.Empty ? null : attachmentId,
            SupersedesVersionId = supersedesVersionId is null || supersedesVersionId == Guid.Empty ? null : supersedesVersionId,
        };
    }

    public void SetContentText(string? contentText)
    {
        if (ApprovedAtUtc is not null || PublishedAtUtc is not null)
            throw new InvalidOperationException("Published or approved versions are immutable.");
        ContentText = string.IsNullOrWhiteSpace(contentText) ? null : contentText.Trim();
    }

    public void Attach(Guid attachmentId)
    {
        if (ApprovedAtUtc is not null || PublishedAtUtc is not null)
            throw new InvalidOperationException("Published or approved versions are immutable.");
        if (attachmentId == Guid.Empty) throw new ArgumentException("Attachment is required.", nameof(attachmentId));
        AttachmentId = attachmentId;
    }

    public void MarkApproved(Guid approverUserId, DateTimeOffset utcNow)
    {
        if (ApprovedAtUtc is not null) return;
        ApprovedByUserId = approverUserId;
        ApprovedAtUtc = utcNow;
    }

    public void MarkPublished(DateTimeOffset utcNow)
    {
        if (PublishedAtUtc is not null) return;
        PublishedAtUtc = utcNow;
    }
}

public enum PolicyAssignmentScope
{
    AllEmployees = 0,
    SpecificUser = 1,
}

public sealed class PolicyAssignment
{
    private PolicyAssignment() { }

    public Guid Id { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public PolicyAssignmentScope AssignmentScope { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public bool IsRequired { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static PolicyAssignment Create(
        Guid managedDocumentId,
        Guid documentVersionId,
        PolicyAssignmentScope scope,
        Guid assignedByUserId,
        DateTimeOffset utcNow,
        Guid? userId = null,
        DateTimeOffset? dueAtUtc = null,
        bool isRequired = true)
    {
        if (managedDocumentId == Guid.Empty) throw new ArgumentException("Document is required.", nameof(managedDocumentId));
        if (documentVersionId == Guid.Empty) throw new ArgumentException("Version is required.", nameof(documentVersionId));
        if (assignedByUserId == Guid.Empty) throw new ArgumentException("Assigner is required.", nameof(assignedByUserId));
        if (scope == PolicyAssignmentScope.SpecificUser && (userId is null || userId == Guid.Empty))
            throw new ArgumentException("User is required for specific-user assignment.", nameof(userId));
        if (scope == PolicyAssignmentScope.AllEmployees && userId is not null)
            throw new ArgumentException("All-employees assignment must not target a user.", nameof(userId));

        return new PolicyAssignment
        {
            Id = Guid.CreateVersion7(),
            ManagedDocumentId = managedDocumentId,
            DocumentVersionId = documentVersionId,
            AssignmentScope = scope,
            UserId = scope == PolicyAssignmentScope.SpecificUser ? userId : null,
            AssignedAtUtc = utcNow,
            DueAtUtc = dueAtUtc,
            AssignedByUserId = assignedByUserId,
            IsRequired = isRequired,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class PolicyAcknowledgement
{
    public const string StatementKeyV1 = "policy.ack.statement.v1";
    public const string StatementTextV1 =
        "I acknowledge that I have read and understood this policy and understand my responsibility to follow it.";
    public const string SourceWeb = "Web";

    private PolicyAcknowledgement() { }

    public Guid Id { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid? PolicyAssignmentId { get; private set; }
    public Guid UserId { get; private set; }
    public string? PolicyNumberSnapshot { get; private set; }
    public string? PolicyTitleSnapshot { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTimeOffset? AssignedAtUtc { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public DateTimeOffset AcknowledgedAtUtc { get; private set; }
    public string AcknowledgementStatementVersion { get; private set; } = null!;
    public string AcknowledgementText { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string? ClientIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static PolicyAcknowledgement Create(
        Guid managedDocumentId,
        Guid documentVersionId,
        Guid userId,
        DateTimeOffset utcNow,
        string policyNumber,
        string policyTitle,
        int versionNumber,
        Guid? policyAssignmentId = null,
        DateTimeOffset? assignedAtUtc = null,
        DateTimeOffset? dueAtUtc = null,
        string? clientIp = null,
        string? userAgent = null,
        string statementVersion = StatementKeyV1,
        string? acknowledgementText = null,
        string source = SourceWeb)
    {
        if (managedDocumentId == Guid.Empty) throw new ArgumentException("Document is required.", nameof(managedDocumentId));
        if (documentVersionId == Guid.Empty) throw new ArgumentException("Version is required.", nameof(documentVersionId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(policyNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyTitle);
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));

        return new PolicyAcknowledgement
        {
            Id = Guid.CreateVersion7(),
            ManagedDocumentId = managedDocumentId,
            DocumentVersionId = documentVersionId,
            PolicyAssignmentId = policyAssignmentId is null || policyAssignmentId == Guid.Empty ? null : policyAssignmentId,
            UserId = userId,
            PolicyNumberSnapshot = policyNumber.Trim(),
            PolicyTitleSnapshot = policyTitle.Trim(),
            VersionNumber = versionNumber,
            AssignedAtUtc = assignedAtUtc,
            DueAtUtc = dueAtUtc,
            AcknowledgedAtUtc = utcNow,
            AcknowledgementStatementVersion = statementVersion.Trim(),
            AcknowledgementText = string.IsNullOrWhiteSpace(acknowledgementText)
                ? StatementTextV1
                : acknowledgementText.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? SourceWeb : source.Trim(),
            ClientIp = Truncate(clientIp, 64),
            UserAgent = Truncate(userAgent, 512),
            CreatedAtUtc = utcNow,
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

public sealed class PolicyAcknowledgementReminderLog
{
    private PolicyAcknowledgementReminderLog() { }

    public Guid Id { get; private set; }
    public Guid PolicyAssignmentId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public string ReminderKind { get; private set; } = null!;
    public DateTimeOffset NotifiedAtUtc { get; private set; }

    public static PolicyAcknowledgementReminderLog Create(
        Guid policyAssignmentId,
        Guid userId,
        Guid documentVersionId,
        string reminderKind,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reminderKind);
        return new PolicyAcknowledgementReminderLog
        {
            Id = Guid.CreateVersion7(),
            PolicyAssignmentId = policyAssignmentId,
            UserId = userId,
            DocumentVersionId = documentVersionId,
            ReminderKind = reminderKind.Trim(),
            NotifiedAtUtc = utcNow,
        };
    }
}

public sealed class DocumentReviewNotificationLog
{
    private DocumentReviewNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public DateTimeOffset ReviewDateUtc { get; private set; }
    public int ThresholdDays { get; private set; }
    public DateTimeOffset NotifiedAtUtc { get; private set; }

    public static DocumentReviewNotificationLog Create(
        Guid managedDocumentId,
        DateTimeOffset reviewDateUtc,
        int thresholdDays,
        DateTimeOffset utcNow)
    {
        return new DocumentReviewNotificationLog
        {
            Id = Guid.CreateVersion7(),
            ManagedDocumentId = managedDocumentId,
            ReviewDateUtc = reviewDateUtc,
            ThresholdDays = thresholdDays,
            NotifiedAtUtc = utcNow,
        };
    }
}

/// <summary>Future seam for P11/P12 control/framework links — unused until those phases.</summary>
public sealed class DocumentGovernanceLink
{
    private DocumentGovernanceLink() { }

    public Guid Id { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public Guid? DocumentVersionId { get; private set; }
    public string LinkKind { get; private set; } = null!;
    public string TargetKey { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DocumentGovernanceLink Create(
        Guid managedDocumentId,
        string linkKind,
        string targetKey,
        DateTimeOffset utcNow,
        Guid? documentVersionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        return new DocumentGovernanceLink
        {
            Id = Guid.CreateVersion7(),
            ManagedDocumentId = managedDocumentId,
            DocumentVersionId = documentVersionId is null || documentVersionId == Guid.Empty ? null : documentVersionId,
            LinkKind = linkKind.Trim(),
            TargetKey = targetKey.Trim(),
            CreatedAtUtc = utcNow,
        };
    }
}
