namespace Qec.Itmg.ChangeManagement.Domain;

public enum ChangeType
{
    Standard = 0,
    Normal = 1,
    Emergency = 2,
}

public enum ChangeStatus
{
    Draft = 0,
    Assessment = 1,
    Approval = 2,
    Scheduled = 3,
    Implementation = 4,
    Validation = 5,
    PostImplementationReview = 6,
    Closed = 7,
    Rejected = 8,
    Failed = 9,
    RolledBack = 10,
    RequiresFollowUp = 11,
    Cancelled = 12,
}

public enum ChangeRiskRating
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public enum ChangeResult
{
    Pending = 0,
    Successful = 1,
    Failed = 2,
    RolledBack = 3,
}

public enum ApprovalDecision
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public sealed class ChangeRequest
{
    private ChangeRequest()
    {
    }

    public Guid Id { get; private set; }
    public string ChangeNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public ChangeType Type { get; private set; }
    public ChangeStatus Status { get; private set; }
    public ChangeRiskRating RiskRating { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public string? BusinessImpact { get; private set; }
    public string? TechnicalImpact { get; private set; }
    public string? SecurityImpact { get; private set; }
    public string? ImplementationPlan { get; private set; }
    public string? TestPlan { get; private set; }
    public string? RollbackPlan { get; private set; }
    public DateTimeOffset? ScheduledStartUtc { get; private set; }
    public DateTimeOffset? ScheduledEndUtc { get; private set; }
    public DateTimeOffset? ImplementationStartedAtUtc { get; private set; }
    public DateTimeOffset? ImplementationCompletedAtUtc { get; private set; }
    public ChangeResult Result { get; private set; }
    public string? ValidationNotes { get; private set; }
    public string? PirNotes { get; private set; }
    public bool IsRetrospective { get; private set; }
    public bool IsPreAuthorizedStandard { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ChangeRequest Create(
        string changeNumber,
        string title,
        string description,
        ChangeType type,
        Guid requesterUserId,
        DateTimeOffset utcNow,
        ChangeRiskRating riskRating = ChangeRiskRating.Medium,
        Guid? ownerUserId = null,
        bool isRetrospective = false,
        bool isPreAuthorizedStandard = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(riskRating)) throw new ArgumentOutOfRangeException(nameof(riskRating));
        if (requesterUserId == Guid.Empty) throw new ArgumentException("Requester is required.", nameof(requesterUserId));
        if (isPreAuthorizedStandard && type != ChangeType.Standard)
        {
            throw new InvalidOperationException("Only Standard changes may be pre-authorized.");
        }

        return new ChangeRequest
        {
            Id = Guid.CreateVersion7(),
            ChangeNumber = changeNumber.Trim(),
            Title = title.Trim(),
            Description = description.Trim(),
            Type = type,
            Status = ChangeStatus.Draft,
            RiskRating = riskRating,
            RequesterUserId = requesterUserId,
            OwnerUserId = NormalizeGuid(ownerUserId),
            Result = ChangeResult.Pending,
            IsRetrospective = isRetrospective,
            IsPreAuthorizedStandard = isPreAuthorizedStandard && type == ChangeType.Standard,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateDetails(
        string title,
        string description,
        ChangeType type,
        ChangeRiskRating riskRating,
        Guid? ownerUserId,
        string? businessImpact,
        string? technicalImpact,
        string? securityImpact,
        string? implementationPlan,
        string? testPlan,
        string? rollbackPlan,
        DateTimeOffset? scheduledStartUtc,
        DateTimeOffset? scheduledEndUtc,
        bool isPreAuthorizedStandard,
        string rowVersion,
        DateTimeOffset utcNow)
    {
        if (Status is ChangeStatus.Closed or ChangeStatus.Cancelled)
        {
            throw new InvalidOperationException("Closed or cancelled changes cannot be edited.");
        }

        EnsureRowVersion(rowVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(riskRating)) throw new ArgumentOutOfRangeException(nameof(riskRating));
        if (isPreAuthorizedStandard && type != ChangeType.Standard)
        {
            throw new InvalidOperationException("Only Standard changes may be pre-authorized.");
        }

        Title = title.Trim();
        Description = description.Trim();
        Type = type;
        RiskRating = riskRating;
        OwnerUserId = NormalizeGuid(ownerUserId);
        BusinessImpact = NormalizeOptional(businessImpact);
        TechnicalImpact = NormalizeOptional(technicalImpact);
        SecurityImpact = NormalizeOptional(securityImpact);
        ImplementationPlan = NormalizeOptional(implementationPlan);
        TestPlan = NormalizeOptional(testPlan);
        RollbackPlan = NormalizeOptional(rollbackPlan);
        ScheduledStartUtc = scheduledStartUtc;
        ScheduledEndUtc = scheduledEndUtc;
        IsPreAuthorizedStandard = isPreAuthorizedStandard && type == ChangeType.Standard;
        UpdatedAtUtc = utcNow;
    }

    public void TransitionTo(
        ChangeStatus target,
        DateTimeOffset utcNow,
        string rowVersion,
        string? validationNotes = null,
        string? pirNotes = null,
        ChangeResult? result = null)
    {
        EnsureRowVersion(rowVersion);
        if (!Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(target));
        if (!IsTransitionAllowed(Status, target))
        {
            throw new InvalidOperationException($"Cannot transition change from {Status} to {target}.");
        }

        Status = target;
        UpdatedAtUtc = utcNow;

        if (target == ChangeStatus.Implementation)
        {
            ImplementationStartedAtUtc ??= utcNow;
        }

        if (target is ChangeStatus.Validation or ChangeStatus.Failed or ChangeStatus.RolledBack)
        {
            ImplementationCompletedAtUtc ??= utcNow;
            if (result is ChangeResult r)
            {
                Result = r;
            }
            else if (target == ChangeStatus.Failed)
            {
                Result = ChangeResult.Failed;
            }
            else if (target == ChangeStatus.RolledBack)
            {
                Result = ChangeResult.RolledBack;
            }
        }

        if (target == ChangeStatus.Validation && result is ChangeResult validationResult)
        {
            Result = validationResult;
        }

        if (!string.IsNullOrWhiteSpace(validationNotes))
        {
            ValidationNotes = validationNotes.Trim();
        }

        if (target == ChangeStatus.PostImplementationReview && !string.IsNullOrWhiteSpace(pirNotes))
        {
            PirNotes = pirNotes.Trim();
        }

        if (target is ChangeStatus.Closed or ChangeStatus.Cancelled or ChangeStatus.Rejected)
        {
            ClosedAtUtc = utcNow;
        }
    }

    public void SetPirNotes(string? pirNotes, DateTimeOffset utcNow)
    {
        PirNotes = NormalizeOptional(pirNotes);
        UpdatedAtUtc = utcNow;
    }

    public bool RequiresPirBeforeClose()
    {
        if (Type == ChangeType.Emergency) return true;
        if (Type == ChangeType.Normal && RiskRating is ChangeRiskRating.High or ChangeRiskRating.Critical) return true;
        return false;
    }

    public bool HasAssessmentContent() =>
        !string.IsNullOrWhiteSpace(BusinessImpact)
        && !string.IsNullOrWhiteSpace(TechnicalImpact)
        && !string.IsNullOrWhiteSpace(ImplementationPlan)
        && !string.IsNullOrWhiteSpace(TestPlan)
        && !string.IsNullOrWhiteSpace(RollbackPlan);

    public bool HasScheduleAndPlans() =>
        ScheduledStartUtc is not null
        && ScheduledEndUtc is not null
        && !string.IsNullOrWhiteSpace(ImplementationPlan)
        && !string.IsNullOrWhiteSpace(TestPlan)
        && !string.IsNullOrWhiteSpace(RollbackPlan);

    public static bool IsTransitionAllowed(ChangeStatus from, ChangeStatus to)
    {
        if (from == to) return true;
        return from switch
        {
            ChangeStatus.Draft => to is ChangeStatus.Assessment or ChangeStatus.Cancelled,
            ChangeStatus.Assessment => to is ChangeStatus.Approval or ChangeStatus.Draft or ChangeStatus.Cancelled,
            ChangeStatus.Approval => to is ChangeStatus.Scheduled or ChangeStatus.Rejected or ChangeStatus.Assessment or ChangeStatus.Cancelled,
            ChangeStatus.Scheduled => to is ChangeStatus.Implementation or ChangeStatus.Approval or ChangeStatus.Cancelled,
            ChangeStatus.Implementation => to is ChangeStatus.Validation or ChangeStatus.Failed or ChangeStatus.RolledBack,
            ChangeStatus.Validation => to is ChangeStatus.PostImplementationReview or ChangeStatus.Closed or ChangeStatus.RequiresFollowUp,
            ChangeStatus.PostImplementationReview => to is ChangeStatus.Closed or ChangeStatus.RequiresFollowUp,
            ChangeStatus.RequiresFollowUp => to is ChangeStatus.Implementation or ChangeStatus.PostImplementationReview or ChangeStatus.Closed,
            ChangeStatus.Failed => to is ChangeStatus.RolledBack or ChangeStatus.RequiresFollowUp or ChangeStatus.Closed,
            ChangeStatus.RolledBack => to is ChangeStatus.RequiresFollowUp or ChangeStatus.Closed or ChangeStatus.PostImplementationReview,
            ChangeStatus.Rejected => false,
            ChangeStatus.Closed => false,
            ChangeStatus.Cancelled => false,
            _ => false,
        };
    }

    private void EnsureRowVersion(string expectedBase64)
    {
        if (!MatchesRowVersion(RowVersion, expectedBase64))
        {
            throw new InvalidOperationException("The change was modified by another user.");
        }
    }

    private static bool MatchesRowVersion(byte[] current, string expectedBase64)
    {
        if (string.IsNullOrWhiteSpace(expectedBase64)) return current.Length == 0;
        try
        {
            return current.AsSpan().SequenceEqual(Convert.FromBase64String(expectedBase64.Trim()));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Guid? NormalizeGuid(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ChangeConfigurationItem
{
    private ChangeConfigurationItem()
    {
    }

    public Guid ChangeRequestId { get; private set; }
    public Guid ConfigurationItemId { get; private set; }
    public DateTimeOffset LinkedAtUtc { get; private set; }
    public Guid LinkedByUserId { get; private set; }

    public static ChangeConfigurationItem Create(
        Guid changeRequestId,
        Guid configurationItemId,
        Guid linkedByUserId,
        DateTimeOffset utcNow)
    {
        if (changeRequestId == Guid.Empty) throw new ArgumentException("Change id is required.", nameof(changeRequestId));
        if (configurationItemId == Guid.Empty) throw new ArgumentException("CI id is required.", nameof(configurationItemId));
        if (linkedByUserId == Guid.Empty) throw new ArgumentException("User is required.", nameof(linkedByUserId));

        return new ChangeConfigurationItem
        {
            ChangeRequestId = changeRequestId,
            ConfigurationItemId = configurationItemId,
            LinkedByUserId = linkedByUserId,
            LinkedAtUtc = utcNow,
        };
    }
}

public sealed class ChangeApproval
{
    private ChangeApproval()
    {
    }

    public Guid Id { get; private set; }
    public Guid ChangeRequestId { get; private set; }
    public Guid ApproverUserId { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ChangeApproval CreatePending(
        Guid changeRequestId,
        Guid approverUserId,
        DateTimeOffset utcNow)
    {
        if (changeRequestId == Guid.Empty) throw new ArgumentException("Change id is required.", nameof(changeRequestId));
        if (approverUserId == Guid.Empty) throw new ArgumentException("Approver is required.", nameof(approverUserId));

        return new ChangeApproval
        {
            Id = Guid.CreateVersion7(),
            ChangeRequestId = changeRequestId,
            ApproverUserId = approverUserId,
            Decision = ApprovalDecision.Pending,
            CreatedAtUtc = utcNow,
        };
    }

    public void Decide(ApprovalDecision decision, string? comment, DateTimeOffset utcNow)
    {
        if (decision is not (ApprovalDecision.Approved or ApprovalDecision.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (Decision != ApprovalDecision.Pending)
        {
            throw new InvalidOperationException("Approval already decided.");
        }

        Decision = decision;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        DecidedAtUtc = utcNow;
    }
}

public sealed class ChangeStatusHistory
{
    private ChangeStatusHistory()
    {
    }

    public Guid Id { get; private set; }
    public Guid ChangeRequestId { get; private set; }
    public ChangeStatus FromStatus { get; private set; }
    public ChangeStatus ToStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }

    public static ChangeStatusHistory Create(
        Guid changeRequestId,
        ChangeStatus from,
        ChangeStatus to,
        Guid changedByUserId,
        DateTimeOffset utcNow,
        string? comment = null)
    {
        return new ChangeStatusHistory
        {
            Id = Guid.CreateVersion7(),
            ChangeRequestId = changeRequestId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = changedByUserId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            ChangedAtUtc = utcNow,
        };
    }
}
