namespace Qec.Itmg.BusinessContinuity.Domain;

public enum BiaStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Retired = 3,
}

public enum ContinuityPlanType
{
    BusinessContinuity = 0,
    ITDisasterRecovery = 1,
}

public enum ContinuityPlanStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2,
}

public enum DrTestType
{
    Tabletop = 0,
    TechnicalRecovery = 1,
    Failover = 2,
    FullExercise = 3,
    Other = 4,
}

public enum DrTestStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum DrTestResult
{
    Passed = 0,
    PassedWithGaps = 1,
    Failed = 2,
    NotCompleted = 3,
}

public enum ContinuityLinkTargetType
{
    ConfigurationItem = 0,
    BusinessService = 1,
    Risk = 2,
    InternalControl = 3,
    ManagedDocument = 4,
    BiaRecord = 5,
    RestoreTest = 6,
    Evidence = 7,
    Finding = 8,
    RecoveryProcedure = 9,
}

public sealed class BiaRecord
{
    private BiaRecord() { }

    public Guid Id { get; private set; }
    public string BiaNumber { get; private set; } = null!;
    public Guid BusinessServiceId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string? BusinessProcessName { get; private set; }
    public string BusinessImpactSummary { get; private set; } = null!;
    public string? FinancialImpact { get; private set; }
    public string? OperationalImpact { get; private set; }
    public string? RegulatoryImpact { get; private set; }
    public string? ReputationalImpact { get; private set; }
    public int? MaximumTolerableDowntimeMinutes { get; private set; }
    public string Criticality { get; private set; } = null!;
    public BiaStatus Status { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static BiaRecord Create(
        string number, Guid businessServiceId, Guid ownerUserId, string impactSummary, string criticality,
        DateTimeOffset utcNow, string? processName = null, string? financial = null, string? operational = null,
        string? regulatory = null, string? reputational = null, int? mtdMinutes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(impactSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(criticality);
        if (businessServiceId == Guid.Empty) throw new ArgumentException("Business service required.");
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.");
        return new BiaRecord
        {
            Id = Guid.CreateVersion7(),
            BiaNumber = number.Trim(),
            BusinessServiceId = businessServiceId,
            OwnerUserId = ownerUserId,
            BusinessProcessName = TrimOrNull(processName),
            BusinessImpactSummary = impactSummary.Trim(),
            FinancialImpact = TrimOrNull(financial),
            OperationalImpact = TrimOrNull(operational),
            RegulatoryImpact = TrimOrNull(regulatory),
            ReputationalImpact = TrimOrNull(reputational),
            MaximumTolerableDowntimeMinutes = mtdMinutes,
            Criticality = criticality.Trim(),
            Status = BiaStatus.Draft,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string impactSummary, string criticality, Guid ownerUserId, string? processName,
        string? financial, string? operational, string? regulatory, string? reputational,
        int? mtdMinutes, DateTimeOffset utcNow)
    {
        if (Status is BiaStatus.Approved or BiaStatus.Retired)
            throw new InvalidOperationException("Approved/retired BIA cannot be edited; create a new revision workflow via status.");
        BusinessImpactSummary = impactSummary.Trim();
        Criticality = criticality.Trim();
        OwnerUserId = ownerUserId;
        BusinessProcessName = TrimOrNull(processName);
        FinancialImpact = TrimOrNull(financial);
        OperationalImpact = TrimOrNull(operational);
        RegulatoryImpact = TrimOrNull(regulatory);
        ReputationalImpact = TrimOrNull(reputational);
        MaximumTolerableDowntimeMinutes = mtdMinutes;
        UpdatedAtUtc = utcNow;
    }

    public void Transition(BiaStatus next, Guid? actorUserId, DateTimeOffset utcNow)
    {
        if (Status == next) return;
        bool ok = (Status, next) switch
        {
            (BiaStatus.Draft, BiaStatus.InReview) => true,
            (BiaStatus.InReview, BiaStatus.Approved) => true,
            (BiaStatus.InReview, BiaStatus.Draft) => true,
            (BiaStatus.Approved, BiaStatus.Retired) => true,
            (BiaStatus.Draft, BiaStatus.Retired) => true,
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"Cannot transition BIA from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
        if (next == BiaStatus.InReview) ReviewedAtUtc = utcNow;
        if (next == BiaStatus.Approved)
        {
            ApprovedByUserId = actorUserId;
            ApprovedAtUtc = utcNow;
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ContinuityPlan
{
    private ContinuityPlan() { }

    public Guid Id { get; private set; }
    public string PlanNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public ContinuityPlanType PlanType { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? ManagedDocumentId { get; private set; }
    public ContinuityPlanStatus Status { get; private set; }
    public DateTimeOffset? EffectiveAtUtc { get; private set; }
    public DateTimeOffset? ReviewAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsReviewOverdue(DateTimeOffset utcNow) =>
        Status == ContinuityPlanStatus.Active && ReviewAtUtc is DateTimeOffset d && d < utcNow;

    public static ContinuityPlan Create(
        string number, string title, ContinuityPlanType planType, Guid ownerUserId, DateTimeOffset utcNow,
        Guid? managedDocumentId = null, DateTimeOffset? effectiveAtUtc = null, DateTimeOffset? reviewAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.");
        return new ContinuityPlan
        {
            Id = Guid.CreateVersion7(),
            PlanNumber = number.Trim(),
            Title = title.Trim(),
            PlanType = planType,
            OwnerUserId = ownerUserId,
            ManagedDocumentId = managedDocumentId == Guid.Empty ? null : managedDocumentId,
            Status = ContinuityPlanStatus.Draft,
            EffectiveAtUtc = effectiveAtUtc,
            ReviewAtUtc = reviewAtUtc,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title, Guid ownerUserId, Guid? managedDocumentId, DateTimeOffset? effectiveAtUtc,
        DateTimeOffset? reviewAtUtc, DateTimeOffset utcNow)
    {
        Title = title.Trim();
        OwnerUserId = ownerUserId;
        ManagedDocumentId = managedDocumentId == Guid.Empty ? null : managedDocumentId;
        EffectiveAtUtc = effectiveAtUtc;
        ReviewAtUtc = reviewAtUtc;
        UpdatedAtUtc = utcNow;
    }

    public void Transition(ContinuityPlanStatus next, DateTimeOffset utcNow)
    {
        if (Status == next) return;
        bool ok = (Status, next) switch
        {
            (ContinuityPlanStatus.Draft, ContinuityPlanStatus.Active) => true,
            (ContinuityPlanStatus.Active, ContinuityPlanStatus.Retired) => true,
            (ContinuityPlanStatus.Draft, ContinuityPlanStatus.Retired) => true,
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"Cannot transition plan from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
        if (next == ContinuityPlanStatus.Active && EffectiveAtUtc is null)
            EffectiveAtUtc = utcNow;
    }
}

public sealed class ContinuityScopeLink
{
    private ContinuityScopeLink() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string OwnerType { get; private set; } = null!;
    public ContinuityLinkTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ContinuityScopeLink Create(
        Guid ownerId, string ownerType, ContinuityLinkTargetType targetType, Guid targetId,
        Guid createdByUserId, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);
        if (targetId == Guid.Empty) throw new ArgumentException("Target required.");
        return new ContinuityScopeLink
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            OwnerType = ownerType.Trim(),
            TargetType = targetType,
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class RecoveryProcedure
{
    private RecoveryProcedure() { }

    public Guid Id { get; private set; }
    public string ProcedureNumber { get; private set; } = null!;
    public Guid ContinuityPlanId { get; private set; }
    public string Title { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }
    public Guid? ManagedDocumentId { get; private set; }
    public int? Sequence { get; private set; }
    public string? RecoveryStage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static RecoveryProcedure Create(
        string number, Guid planId, string title, Guid ownerUserId, DateTimeOffset utcNow,
        Guid? managedDocumentId = null, int? sequence = null, string? recoveryStage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new RecoveryProcedure
        {
            Id = Guid.CreateVersion7(),
            ProcedureNumber = number.Trim(),
            ContinuityPlanId = planId,
            Title = title.Trim(),
            OwnerUserId = ownerUserId,
            ManagedDocumentId = managedDocumentId == Guid.Empty ? null : managedDocumentId,
            Sequence = sequence,
            RecoveryStage = string.IsNullOrWhiteSpace(recoveryStage) ? null : recoveryStage.Trim(),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string title, Guid ownerUserId, Guid? managedDocumentId, int? sequence, string? stage, bool isActive, DateTimeOffset utcNow)
    {
        Title = title.Trim();
        OwnerUserId = ownerUserId;
        ManagedDocumentId = managedDocumentId == Guid.Empty ? null : managedDocumentId;
        Sequence = sequence;
        RecoveryStage = string.IsNullOrWhiteSpace(stage) ? null : stage.Trim();
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class DrTest
{
    private DrTest() { }

    public Guid Id { get; private set; }
    public string DrTestNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public Guid? ContinuityPlanId { get; private set; }
    public Guid BusinessServiceId { get; private set; }
    public DrTestType TestType { get; private set; }
    public DateTimeOffset PlannedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public DrTestStatus Status { get; private set; }
    public DrTestResult? Result { get; private set; }
    public int? ObservedRtoMinutes { get; private set; }
    public int? ObservedRpoMinutes { get; private set; }
    public string? Summary { get; private set; }
    public string? Gaps { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static DrTest Create(
        string number, string title, Guid businessServiceId, DrTestType testType, DateTimeOffset plannedAtUtc,
        Guid ownerUserId, DateTimeOffset utcNow, Guid? continuityPlanId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (businessServiceId == Guid.Empty) throw new ArgumentException("Business service required.");
        return new DrTest
        {
            Id = Guid.CreateVersion7(),
            DrTestNumber = number.Trim(),
            Title = title.Trim(),
            ContinuityPlanId = continuityPlanId == Guid.Empty ? null : continuityPlanId,
            BusinessServiceId = businessServiceId,
            TestType = testType,
            PlannedAtUtc = plannedAtUtc,
            OwnerUserId = ownerUserId,
            Status = DrTestStatus.Planned,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Start(DateTimeOffset utcNow)
    {
        if (Status != DrTestStatus.Planned)
            throw new InvalidOperationException("Only planned tests can start.");
        Status = DrTestStatus.InProgress;
        StartedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void Complete(
        DrTestResult result, DateTimeOffset utcNow, int? observedRto = null, int? observedRpo = null,
        string? summary = null, string? gaps = null)
    {
        if (Status is not (DrTestStatus.Planned or DrTestStatus.InProgress))
            throw new InvalidOperationException("Cannot complete test in current status.");
        Status = DrTestStatus.Completed;
        Result = result;
        ObservedRtoMinutes = observedRto;
        ObservedRpoMinutes = observedRpo;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        Gaps = string.IsNullOrWhiteSpace(gaps) ? null : gaps.Trim();
        CompletedAtUtc = utcNow;
        StartedAtUtc ??= utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        if (Status is DrTestStatus.Completed or DrTestStatus.Cancelled)
            throw new InvalidOperationException("Cannot cancel completed/cancelled test.");
        Status = DrTestStatus.Cancelled;
        Result = DrTestResult.NotCompleted;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class ContinuityNotificationLog
{
    private ContinuityNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid ResourceId { get; private set; }
    public string EventKey { get; private set; } = null!;
    public DateTimeOffset SentAtUtc { get; private set; }

    public static ContinuityNotificationLog Create(Guid resourceId, string eventKey, DateTimeOffset utcNow) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ResourceId = resourceId,
            EventKey = eventKey,
            SentAtUtc = utcNow,
        };
}
