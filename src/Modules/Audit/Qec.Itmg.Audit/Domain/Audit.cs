namespace Qec.Itmg.Audit.Domain;

public enum AuditType
{
    Internal = 0,
    External = 1,
    ISA315Profile = 2,
    Other = 3,
}

public enum AuditEngagementStatus
{
    Draft = 0,
    Planned = 1,
    InProgress = 2,
    Fieldwork = 3,
    Reporting = 4,
    Closed = 5,
    Cancelled = 6,
}

public enum AuditScopeTargetType
{
    ConfigurationItem = 0,
    BusinessService = 1,
    InternalControl = 2,
    FrameworkVersion = 3,
}

public enum AuditQuestionResponseType
{
    Text = 0,
    YesNo = 1,
    Choice = 2,
    DocumentReference = 3,
}

public enum AuditQuestionStatus
{
    Open = 0,
    Answered = 1,
    Reviewed = 2,
    NotApplicable = 3,
}

public enum FindingSeverity
{
    Informational = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum FindingStatus
{
    Open = 0,
    InRemediation = 1,
    PendingVerification = 2,
    Closed = 3,
    AcceptedRisk = 4,
}

public enum CorrectiveActionStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Verified = 3,
}

public enum EvidenceRequestStatus
{
    Requested = 0,
    InProgress = 1,
    Fulfilled = 2,
    Cancelled = 3,
}

public sealed class AuditEngagement
{
    private AuditEngagement() { }

    public Guid Id { get; private set; }
    public string AuditNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public AuditType AuditType { get; private set; }
    public string? Objective { get; private set; }
    public string? ScopeSummary { get; private set; }
    public Guid? LeadAuditorUserId { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public AuditEngagementStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static AuditEngagement Create(
        string auditNumber,
        string title,
        AuditType auditType,
        DateTimeOffset utcNow,
        string? objective = null,
        string? scopeSummary = null,
        Guid? leadAuditorUserId = null,
        Guid? ownerUserId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auditNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new AuditEngagement
        {
            Id = Guid.CreateVersion7(),
            AuditNumber = auditNumber.Trim(),
            Title = title.Trim(),
            AuditType = auditType,
            Objective = TrimOrNull(objective),
            ScopeSummary = TrimOrNull(scopeSummary),
            LeadAuditorUserId = EmptyToNull(leadAuditorUserId),
            OwnerUserId = EmptyToNull(ownerUserId),
            StartDate = startDate,
            EndDate = endDate,
            Status = AuditEngagementStatus.Draft,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title,
        string? objective,
        string? scopeSummary,
        Guid? leadAuditorUserId,
        Guid? ownerUserId,
        DateOnly? startDate,
        DateOnly? endDate,
        DateTimeOffset utcNow)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Objective = TrimOrNull(objective);
        ScopeSummary = TrimOrNull(scopeSummary);
        LeadAuditorUserId = EmptyToNull(leadAuditorUserId);
        OwnerUserId = EmptyToNull(ownerUserId);
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAtUtc = utcNow;
    }

    public void Transition(AuditEngagementStatus next, DateTimeOffset utcNow)
    {
        if (Status == next) return;
        if (!IsAllowed(Status, next))
            throw new InvalidOperationException($"Cannot transition engagement from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
        ClosedAtUtc = next is AuditEngagementStatus.Closed or AuditEngagementStatus.Cancelled ? utcNow : null;
    }

    private void EnsureMutable()
    {
        if (Status is AuditEngagementStatus.Closed or AuditEngagementStatus.Cancelled)
            throw new InvalidOperationException("Closed/cancelled engagements cannot be edited.");
    }

    private static bool IsAllowed(AuditEngagementStatus from, AuditEngagementStatus to) => (from, to) switch
    {
        (AuditEngagementStatus.Draft, AuditEngagementStatus.Planned) => true,
        (AuditEngagementStatus.Draft, AuditEngagementStatus.Cancelled) => true,
        (AuditEngagementStatus.Planned, AuditEngagementStatus.InProgress) => true,
        (AuditEngagementStatus.Planned, AuditEngagementStatus.Cancelled) => true,
        (AuditEngagementStatus.InProgress, AuditEngagementStatus.Fieldwork) => true,
        (AuditEngagementStatus.InProgress, AuditEngagementStatus.Reporting) => true,
        (AuditEngagementStatus.InProgress, AuditEngagementStatus.Cancelled) => true,
        (AuditEngagementStatus.Fieldwork, AuditEngagementStatus.Reporting) => true,
        (AuditEngagementStatus.Fieldwork, AuditEngagementStatus.Cancelled) => true,
        (AuditEngagementStatus.Reporting, AuditEngagementStatus.Closed) => true,
        (AuditEngagementStatus.Reporting, AuditEngagementStatus.Cancelled) => true,
        _ => false,
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? EmptyToNull(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}

public sealed class AuditScopeLink
{
    private AuditScopeLink() { }

    public Guid Id { get; private set; }
    public Guid AuditEngagementId { get; private set; }
    public AuditScopeTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AuditScopeLink Create(
        Guid engagementId, AuditScopeTargetType targetType, Guid targetId, Guid createdByUserId, DateTimeOffset utcNow)
    {
        if (engagementId == Guid.Empty) throw new ArgumentException("Engagement required.", nameof(engagementId));
        if (targetId == Guid.Empty) throw new ArgumentException("Target required.", nameof(targetId));
        return new AuditScopeLink
        {
            Id = Guid.CreateVersion7(),
            AuditEngagementId = engagementId,
            TargetType = targetType,
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class AuditQuestion
{
    private AuditQuestion() { }

    public Guid Id { get; private set; }
    public Guid AuditEngagementId { get; private set; }
    public string? QuestionCode { get; private set; }
    public string Category { get; private set; } = null!;
    public string QuestionText { get; private set; } = null!;
    public Guid? FrameworkRequirementId { get; private set; }
    public Guid? InternalControlId { get; private set; }
    public AuditQuestionResponseType ResponseType { get; private set; }
    public bool Required { get; private set; }
    public int SortOrder { get; private set; }
    public AuditQuestionStatus Status { get; private set; }
    public string? Response { get; private set; }
    public Guid? RespondedByUserId { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    public string? ReviewerNotes { get; private set; }

    public static AuditQuestion Create(
        Guid engagementId,
        string category,
        string questionText,
        AuditQuestionResponseType responseType,
        bool required,
        int sortOrder,
        string? questionCode = null,
        Guid? frameworkRequirementId = null,
        Guid? internalControlId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(questionText);
        return new AuditQuestion
        {
            Id = Guid.CreateVersion7(),
            AuditEngagementId = engagementId,
            QuestionCode = string.IsNullOrWhiteSpace(questionCode) ? null : questionCode.Trim(),
            Category = category.Trim(),
            QuestionText = questionText.Trim(),
            FrameworkRequirementId = frameworkRequirementId == Guid.Empty ? null : frameworkRequirementId,
            InternalControlId = internalControlId == Guid.Empty ? null : internalControlId,
            ResponseType = responseType,
            Required = required,
            SortOrder = sortOrder,
            Status = AuditQuestionStatus.Open,
        };
    }

    public void Answer(string? response, Guid userId, DateTimeOffset utcNow)
    {
        Response = string.IsNullOrWhiteSpace(response) ? null : response.Trim();
        RespondedByUserId = userId;
        RespondedAtUtc = utcNow;
        Status = AuditQuestionStatus.Answered;
    }

    public void MarkReviewed(string? notes)
    {
        if (Status is not (AuditQuestionStatus.Answered or AuditQuestionStatus.Reviewed))
            throw new InvalidOperationException("Only answered questions can be reviewed.");
        ReviewerNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = AuditQuestionStatus.Reviewed;
    }

    public void MarkNotApplicable(string? notes)
    {
        ReviewerNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = AuditQuestionStatus.NotApplicable;
        Response = null;
    }
}

public sealed class Finding
{
    private Finding() { }

    public Guid Id { get; private set; }
    public string FindingNumber { get; private set; } = null!;
    public Guid AuditEngagementId { get; private set; }
    public Guid? InternalControlId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public FindingSeverity Severity { get; private set; }
    public FindingStatus Status { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public string? AcceptedRiskReason { get; private set; }
    public string? ExceptionReference { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Finding Create(
        string findingNumber,
        Guid engagementId,
        string title,
        string description,
        FindingSeverity severity,
        DateTimeOffset utcNow,
        Guid? internalControlId = null,
        Guid? ownerUserId = null,
        DateTimeOffset? dueAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(findingNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new Finding
        {
            Id = Guid.CreateVersion7(),
            FindingNumber = findingNumber.Trim(),
            AuditEngagementId = engagementId,
            InternalControlId = internalControlId == Guid.Empty ? null : internalControlId,
            Title = title.Trim(),
            Description = description.Trim(),
            Severity = severity,
            Status = FindingStatus.Open,
            OwnerUserId = ownerUserId == Guid.Empty ? null : ownerUserId,
            DueAtUtc = dueAtUtc,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title, string description, FindingSeverity severity, Guid? ownerUserId, DateTimeOffset? dueAtUtc, DateTimeOffset utcNow)
    {
        if (Status is FindingStatus.Closed or FindingStatus.AcceptedRisk)
            throw new InvalidOperationException("Closed findings cannot be edited.");
        Title = title.Trim();
        Description = description.Trim();
        Severity = severity;
        OwnerUserId = ownerUserId == Guid.Empty ? null : ownerUserId;
        DueAtUtc = dueAtUtc;
        UpdatedAtUtc = utcNow;
    }

    public void Transition(FindingStatus next, DateTimeOffset utcNow, string? acceptedRiskReason = null, string? exceptionReference = null)
    {
        if (Status == next) return;
        if (!IsAllowed(Status, next))
            throw new InvalidOperationException($"Cannot transition finding from {Status} to {next}.");
        if (next == FindingStatus.AcceptedRisk)
        {
            if (string.IsNullOrWhiteSpace(acceptedRiskReason))
                throw new InvalidOperationException("AcceptedRisk requires a documented reason.");
            AcceptedRiskReason = acceptedRiskReason.Trim();
            ExceptionReference = string.IsNullOrWhiteSpace(exceptionReference) ? null : exceptionReference.Trim();
        }

        Status = next;
        UpdatedAtUtc = utcNow;
        ClosedAtUtc = next is FindingStatus.Closed or FindingStatus.AcceptedRisk ? utcNow : null;
    }

    private static bool IsAllowed(FindingStatus from, FindingStatus to) => (from, to) switch
    {
        (FindingStatus.Open, FindingStatus.InRemediation) => true,
        (FindingStatus.Open, FindingStatus.AcceptedRisk) => true,
        (FindingStatus.Open, FindingStatus.Closed) => true,
        (FindingStatus.InRemediation, FindingStatus.PendingVerification) => true,
        (FindingStatus.InRemediation, FindingStatus.AcceptedRisk) => true,
        (FindingStatus.PendingVerification, FindingStatus.Closed) => true,
        (FindingStatus.PendingVerification, FindingStatus.InRemediation) => true,
        (FindingStatus.PendingVerification, FindingStatus.AcceptedRisk) => true,
        _ => false,
    };
}

public sealed class ManagementResponse
{
    private ManagementResponse() { }

    public Guid Id { get; private set; }
    public Guid FindingId { get; private set; }
    public string ResponseText { get; private set; } = null!;
    public Guid RespondedByUserId { get; private set; }
    public DateTimeOffset RespondedAtUtc { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public Guid? ManagementOwnerUserId { get; private set; }

    public static ManagementResponse Create(
        Guid findingId,
        string responseText,
        Guid respondedByUserId,
        DateTimeOffset utcNow,
        DateOnly? targetDate = null,
        Guid? managementOwnerUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseText);
        return new ManagementResponse
        {
            Id = Guid.CreateVersion7(),
            FindingId = findingId,
            ResponseText = responseText.Trim(),
            RespondedByUserId = respondedByUserId,
            RespondedAtUtc = utcNow,
            TargetDate = targetDate,
            ManagementOwnerUserId = managementOwnerUserId == Guid.Empty ? null : managementOwnerUserId,
        };
    }
}

public sealed class CorrectiveAction
{
    private CorrectiveAction() { }

    public Guid Id { get; private set; }
    public string? ActionNumber { get; private set; }
    public Guid FindingId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public CorrectiveActionStatus Status { get; private set; }
    public bool IsMandatory { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }
    public string? VerificationNotes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsOverdue(DateTimeOffset utcNow) =>
        DueAtUtc is DateTimeOffset due
        && due < utcNow
        && Status != CorrectiveActionStatus.Verified;

    public static CorrectiveAction Create(
        string? actionNumber,
        Guid findingId,
        string title,
        string description,
        Guid ownerUserId,
        DateTimeOffset utcNow,
        DateTimeOffset? dueAtUtc = null,
        bool isMandatory = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.", nameof(ownerUserId));
        return new CorrectiveAction
        {
            Id = Guid.CreateVersion7(),
            ActionNumber = string.IsNullOrWhiteSpace(actionNumber) ? null : actionNumber.Trim(),
            FindingId = findingId,
            Title = title.Trim(),
            Description = description.Trim(),
            OwnerUserId = ownerUserId,
            DueAtUtc = dueAtUtc,
            Status = CorrectiveActionStatus.Open,
            IsMandatory = isMandatory,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Transition(CorrectiveActionStatus next, DateTimeOffset utcNow, Guid? verifiedBy = null, string? notes = null)
    {
        if (Status == next) return;
        if (!IsAllowed(Status, next))
            throw new InvalidOperationException($"Cannot transition CAPA from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
        if (next == CorrectiveActionStatus.Completed)
            CompletedAtUtc = utcNow;
        if (next == CorrectiveActionStatus.Verified)
        {
            VerifiedByUserId = verifiedBy;
            VerifiedAtUtc = utcNow;
            VerificationNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        }
    }

    private static bool IsAllowed(CorrectiveActionStatus from, CorrectiveActionStatus to) => (from, to) switch
    {
        (CorrectiveActionStatus.Open, CorrectiveActionStatus.InProgress) => true,
        (CorrectiveActionStatus.InProgress, CorrectiveActionStatus.Completed) => true,
        (CorrectiveActionStatus.Completed, CorrectiveActionStatus.Verified) => true,
        (CorrectiveActionStatus.Completed, CorrectiveActionStatus.InProgress) => true,
        _ => false,
    };
}

public sealed class EvidenceRequest
{
    private EvidenceRequest() { }

    public Guid Id { get; private set; }
    public Guid AuditEngagementId { get; private set; }
    public Guid? AuditQuestionId { get; private set; }
    public Guid? InternalControlId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid? RequestedFromUserId { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public EvidenceRequestStatus Status { get; private set; }
    public Guid? EvidenceId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? FulfilledAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public static EvidenceRequest Create(
        Guid engagementId,
        string title,
        Guid createdByUserId,
        DateTimeOffset utcNow,
        string? description = null,
        Guid? auditQuestionId = null,
        Guid? internalControlId = null,
        Guid? requestedFromUserId = null,
        DateTimeOffset? dueAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new EvidenceRequest
        {
            Id = Guid.CreateVersion7(),
            AuditEngagementId = engagementId,
            AuditQuestionId = auditQuestionId == Guid.Empty ? null : auditQuestionId,
            InternalControlId = internalControlId == Guid.Empty ? null : internalControlId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            RequestedFromUserId = requestedFromUserId == Guid.Empty ? null : requestedFromUserId,
            DueAtUtc = dueAtUtc,
            Status = EvidenceRequestStatus.Requested,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }

    public void MarkInProgress()
    {
        if (Status is EvidenceRequestStatus.Fulfilled or EvidenceRequestStatus.Cancelled)
            throw new InvalidOperationException("Cannot change a fulfilled/cancelled request.");
        Status = EvidenceRequestStatus.InProgress;
    }

    public void Fulfill(Guid evidenceId, DateTimeOffset utcNow, string? notes = null)
    {
        if (evidenceId == Guid.Empty) throw new ArgumentException("Evidence required.", nameof(evidenceId));
        if (Status == EvidenceRequestStatus.Cancelled)
            throw new InvalidOperationException("Cancelled requests cannot be fulfilled.");
        EvidenceId = evidenceId;
        Status = EvidenceRequestStatus.Fulfilled;
        FulfilledAtUtc = utcNow;
        Notes = string.IsNullOrWhiteSpace(notes) ? Notes : notes.Trim();
    }

    public void Cancel(string? notes = null)
    {
        if (Status == EvidenceRequestStatus.Fulfilled)
            throw new InvalidOperationException("Fulfilled requests cannot be cancelled.");
        Status = EvidenceRequestStatus.Cancelled;
        Notes = string.IsNullOrWhiteSpace(notes) ? Notes : notes.Trim();
    }
}

public sealed class EvidenceRequestNotificationLog
{
    private EvidenceRequestNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid EvidenceRequestId { get; private set; }
    public string EventKey { get; private set; } = null!;
    public DateTimeOffset SentAtUtc { get; private set; }

    public static EvidenceRequestNotificationLog Create(Guid requestId, string eventKey, DateTimeOffset utcNow) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            EvidenceRequestId = requestId,
            EventKey = eventKey,
            SentAtUtc = utcNow,
        };
}
