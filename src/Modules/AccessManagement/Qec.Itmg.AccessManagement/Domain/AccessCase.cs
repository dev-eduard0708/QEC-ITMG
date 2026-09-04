namespace Qec.Itmg.AccessManagement.Domain;

public enum AccessCaseType
{
    Joiner = 0,
    Mover = 1,
    Leaver = 2,
    AccessRequest = 3,
}

public enum AccessCaseStatus
{
    Draft = 0,
    Submitted = 1,
    Approval = 2,
    Fulfillment = 3,
    Verification = 4,
    Closed = 5,
    Rejected = 6,
    Cancelled = 7,
}

public enum AccessItemAction
{
    Grant = 0,
    Remove = 1,
    Disable = 2,
    Reassign = 3,
}

public enum AccessItemStatus
{
    Pending = 0,
    Completed = 1,
    NotApplicable = 2,
}

public enum AccessCaseExceptionType
{
    CancelOverride = 0,
    MandatoryItemOverride = 1,
    SodException = 2,
}

public enum AccessReviewType
{
    UserAccess = 0,
    Privileged = 1,
    ServiceAccount = 2,
}

public enum AccessReviewCampaignStatus
{
    Draft = 0,
    Open = 1,
    Completed = 2,
}

public enum AccessReviewDecision
{
    Pending = 0,
    Keep = 1,
    Remove = 2,
    Modify = 3,
}

public enum ManagedAccountType
{
    Privileged = 0,
    Service = 1,
}

public enum ManagedAccountStatus
{
    Active = 0,
    Disabled = 1,
}

public sealed class AccessCase
{
    private AccessCase() { }

    public Guid Id { get; private set; }
    public string CaseNumber { get; private set; } = null!;
    public AccessCaseType Type { get; private set; }
    public AccessCaseStatus Status { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid? SubjectUserId { get; private set; }
    public string? SubjectName { get; private set; }
    public string? SubjectEmail { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public Guid? DesignatedApproverUserId { get; private set; }
    public Guid? LinkedTicketId { get; private set; }
    public DateTimeOffset? EffectiveAtUtc { get; private set; }
    public string Reason { get; private set; } = null!;
    public bool ExistingAccessConfirmed { get; private set; }
    public DateTimeOffset? ExistingAccessConfirmedAtUtc { get; private set; }
    public Guid? ExistingAccessConfirmedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static AccessCase Create(
        string caseNumber,
        AccessCaseType type,
        Guid requesterUserId,
        string reason,
        DateTimeOffset utcNow,
        Guid? subjectUserId = null,
        string? subjectName = null,
        string? subjectEmail = null,
        Guid? departmentId = null,
        Guid? managerUserId = null,
        Guid? designatedApproverUserId = null,
        DateTimeOffset? effectiveAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (requesterUserId == Guid.Empty) throw new ArgumentException("Requester is required.", nameof(requesterUserId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        return new AccessCase
        {
            Id = Guid.CreateVersion7(),
            CaseNumber = caseNumber.Trim(),
            Type = type,
            Status = AccessCaseStatus.Draft,
            RequesterUserId = requesterUserId,
            SubjectUserId = Norm(subjectUserId),
            SubjectName = Norm(subjectName),
            SubjectEmail = Norm(subjectEmail),
            DepartmentId = Norm(departmentId),
            ManagerUserId = Norm(managerUserId),
            DesignatedApproverUserId = Norm(designatedApproverUserId),
            EffectiveAtUtc = effectiveAtUtc,
            Reason = reason.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateDraft(
        string reason,
        Guid? subjectUserId,
        string? subjectName,
        string? subjectEmail,
        Guid? departmentId,
        Guid? managerUserId,
        Guid? designatedApproverUserId,
        DateTimeOffset? effectiveAtUtc,
        DateTimeOffset utcNow)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason.Trim();
        SubjectUserId = Norm(subjectUserId);
        SubjectName = Norm(subjectName);
        SubjectEmail = Norm(subjectEmail);
        DepartmentId = Norm(departmentId);
        ManagerUserId = Norm(managerUserId);
        DesignatedApproverUserId = Norm(designatedApproverUserId);
        EffectiveAtUtc = effectiveAtUtc;
        UpdatedAtUtc = utcNow;
    }

    public void LinkTicket(Guid ticketId, DateTimeOffset utcNow)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("Ticket is required.", nameof(ticketId));
        LinkedTicketId = ticketId;
        UpdatedAtUtc = utcNow;
    }

    public void ConfirmExistingAccess(Guid userId, DateTimeOffset utcNow)
    {
        if (Type != AccessCaseType.Mover)
            throw new InvalidOperationException("Existing access confirmation applies to Mover cases only.");
        if (Status is AccessCaseStatus.Closed or AccessCaseStatus.Rejected or AccessCaseStatus.Cancelled)
            throw new InvalidOperationException("Cannot confirm existing access on a terminal case.");
        ExistingAccessConfirmed = true;
        ExistingAccessConfirmedAtUtc = utcNow;
        ExistingAccessConfirmedByUserId = userId;
        UpdatedAtUtc = utcNow;
    }

    public void ClearExistingAccessConfirmation(DateTimeOffset utcNow)
    {
        if (Type != AccessCaseType.Mover)
            throw new InvalidOperationException("Existing access confirmation applies to Mover cases only.");
        ExistingAccessConfirmed = false;
        ExistingAccessConfirmedAtUtc = null;
        ExistingAccessConfirmedByUserId = null;
        UpdatedAtUtc = utcNow;
    }

    public void TransitionTo(AccessCaseStatus next, DateTimeOffset utcNow, bool hasCancelOverride = false)
    {
        if (Status == next) return;
        if (!IsAllowedTransition(Status, next, Type, hasCancelOverride))
            throw new InvalidOperationException($"Cannot transition from {Status} to {next} for {Type}.");
        Status = next;
        UpdatedAtUtc = utcNow;
        if (next is AccessCaseStatus.Closed or AccessCaseStatus.Rejected or AccessCaseStatus.Cancelled)
            ClosedAtUtc = utcNow;
    }

    public static bool IsAllowedTransition(
        AccessCaseStatus from,
        AccessCaseStatus to,
        AccessCaseType type,
        bool hasCancelOverride)
    {
        return (from, to) switch
        {
            (AccessCaseStatus.Draft, AccessCaseStatus.Submitted) => true,
            (AccessCaseStatus.Draft, AccessCaseStatus.Cancelled) => true,
            (AccessCaseStatus.Submitted, AccessCaseStatus.Approval) => true,
            (AccessCaseStatus.Submitted, AccessCaseStatus.Cancelled) => true,
            (AccessCaseStatus.Approval, AccessCaseStatus.Fulfillment) => true,
            (AccessCaseStatus.Approval, AccessCaseStatus.Rejected) => true,
            (AccessCaseStatus.Approval, AccessCaseStatus.Cancelled) => true,
            (AccessCaseStatus.Fulfillment, AccessCaseStatus.Verification) => true,
            (AccessCaseStatus.Fulfillment, AccessCaseStatus.Cancelled) =>
                type != AccessCaseType.Leaver || hasCancelOverride,
            (AccessCaseStatus.Verification, AccessCaseStatus.Closed) => true,
            (AccessCaseStatus.Verification, AccessCaseStatus.Fulfillment) => true,
            _ => false,
        };
    }

    private void EnsureDraft()
    {
        if (Status != AccessCaseStatus.Draft)
            throw new InvalidOperationException("Only draft cases can be edited.");
    }

    private static Guid? Norm(Guid? v) => v is null || v == Guid.Empty ? null : v;
    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class AccessCaseItem
{
    private AccessCaseItem() { }

    public Guid Id { get; private set; }
    public Guid AccessCaseId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public string EntitlementKey { get; private set; } = null!;
    public AccessItemAction Action { get; private set; }
    public bool IsPrivileged { get; private set; }
    public bool IsMandatory { get; private set; }
    public AccessItemStatus Status { get; private set; }
    public Guid? FulfilledByUserId { get; private set; }
    public DateTimeOffset? FulfilledAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AccessCaseItem Create(
        Guid accessCaseId,
        string entitlementKey,
        AccessItemAction action,
        DateTimeOffset utcNow,
        Guid? configurationItemId = null,
        bool isPrivileged = false,
        bool isMandatory = false,
        string? notes = null)
    {
        if (accessCaseId == Guid.Empty) throw new ArgumentException("Case is required.", nameof(accessCaseId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementKey);
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        return new AccessCaseItem
        {
            Id = Guid.CreateVersion7(),
            AccessCaseId = accessCaseId,
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            EntitlementKey = entitlementKey.Trim(),
            Action = action,
            IsPrivileged = isPrivileged,
            IsMandatory = isMandatory,
            Status = AccessItemStatus.Pending,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = utcNow,
        };
    }

    public void MarkCompleted(Guid userId, DateTimeOffset utcNow, string? notes = null)
    {
        Status = AccessItemStatus.Completed;
        FulfilledByUserId = userId;
        FulfilledAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes)) Notes = notes.Trim();
    }

    public void MarkNotApplicable(Guid userId, DateTimeOffset utcNow, string? notes = null)
    {
        Status = AccessItemStatus.NotApplicable;
        FulfilledByUserId = userId;
        FulfilledAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes)) Notes = notes.Trim();
    }
}

public sealed class ExistingAccessSnapshotItem
{
    private ExistingAccessSnapshotItem() { }

    public Guid Id { get; private set; }
    public Guid AccessCaseId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public string EntitlementKey { get; private set; } = null!;
    public string? AccessSummary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ExistingAccessSnapshotItem Create(
        Guid accessCaseId,
        string entitlementKey,
        DateTimeOffset utcNow,
        Guid? configurationItemId = null,
        string? accessSummary = null)
    {
        if (accessCaseId == Guid.Empty) throw new ArgumentException("Case is required.", nameof(accessCaseId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementKey);
        return new ExistingAccessSnapshotItem
        {
            Id = Guid.CreateVersion7(),
            AccessCaseId = accessCaseId,
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            EntitlementKey = entitlementKey.Trim(),
            AccessSummary = string.IsNullOrWhiteSpace(accessSummary) ? null : accessSummary.Trim(),
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class AccessCaseException
{
    private AccessCaseException() { }

    public Guid Id { get; private set; }
    public Guid AccessCaseId { get; private set; }
    public AccessCaseExceptionType Type { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid AuthorizedByUserId { get; private set; }
    public Guid? RelatedSodRuleId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AccessCaseException Create(
        Guid accessCaseId,
        AccessCaseExceptionType type,
        string reason,
        Guid authorizedByUserId,
        DateTimeOffset utcNow,
        Guid? relatedSodRuleId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (accessCaseId == Guid.Empty) throw new ArgumentException("Case is required.", nameof(accessCaseId));
        if (authorizedByUserId == Guid.Empty) throw new ArgumentException("Authorizer is required.", nameof(authorizedByUserId));
        return new AccessCaseException
        {
            Id = Guid.CreateVersion7(),
            AccessCaseId = accessCaseId,
            Type = type,
            Reason = reason.Trim(),
            AuthorizedByUserId = authorizedByUserId,
            RelatedSodRuleId = relatedSodRuleId is null || relatedSodRuleId == Guid.Empty ? null : relatedSodRuleId,
            CreatedAtUtc = utcNow,
        };
    }
}
