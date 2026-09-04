namespace Qec.Itmg.Compliance.Domain;

public enum FrameworkRequirementType
{
    Domain = 0,
    Objective = 1,
    Clause = 2,
    Practice = 3,
    Control = 4,
    Question = 5,
    Other = 6,
}

public enum MappingRelationship
{
    Primary = 0,
    Supporting = 1,
}

public enum AssessmentStatus
{
    NotStarted = 0,
    InProgress = 1,
    Complete = 2,
}

public enum AssessmentResult
{
    Compliant = 0,
    PartiallyCompliant = 1,
    NonCompliant = 2,
    NotApplicable = 3,
    NotTested = 4,
}

public enum CalendarItemType
{
    ControlAssessment = 0,
    PolicyReview = 1,
    AccessReview = 2,
    Other = 3,
}

public enum CalendarItemStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

public sealed class Framework
{
    private Framework() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Publisher { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Framework Create(
        string code, string name, string publisher, DateTimeOffset utcNow, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        return new Framework
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Publisher = publisher.Trim(),
            Description = TrimOrNull(description),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string name, string publisher, string? description, bool isActive, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        Name = name.Trim();
        Publisher = publisher.Trim();
        Description = TrimOrNull(description);
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class FrameworkVersion
{
    private FrameworkVersion() { }

    public Guid Id { get; private set; }
    public Guid FrameworkId { get; private set; }
    public string VersionCode { get; private set; } = null!;
    public string? Title { get; private set; }
    public DateOnly? PublishedDate { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FrameworkVersion Create(
        Guid frameworkId, string versionCode, DateTimeOffset utcNow,
        string? title = null, DateOnly? publishedDate = null, DateOnly? effectiveDate = null, bool isCurrent = false)
    {
        if (frameworkId == Guid.Empty) throw new ArgumentException("Framework required.", nameof(frameworkId));
        ArgumentException.ThrowIfNullOrWhiteSpace(versionCode);
        return new FrameworkVersion
        {
            Id = Guid.CreateVersion7(),
            FrameworkId = frameworkId,
            VersionCode = versionCode.Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            PublishedDate = publishedDate,
            EffectiveDate = effectiveDate,
            IsCurrent = isCurrent,
            CreatedAtUtc = utcNow,
        };
    }

    public void SetCurrent(bool isCurrent) => IsCurrent = isCurrent;
}

public sealed class FrameworkRequirement
{
    private FrameworkRequirement() { }

    public Guid Id { get; private set; }
    public Guid FrameworkVersionId { get; private set; }
    public Guid? ParentRequirementId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Text { get; private set; }
    public FrameworkRequirementType RequirementType { get; private set; }
    public int? SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static FrameworkRequirement Create(
        Guid frameworkVersionId, string code, string title, FrameworkRequirementType type,
        Guid? parentRequirementId = null, string? text = null, int? sortOrder = null)
    {
        if (frameworkVersionId == Guid.Empty) throw new ArgumentException("Version required.", nameof(frameworkVersionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new FrameworkRequirement
        {
            Id = Guid.CreateVersion7(),
            FrameworkVersionId = frameworkVersionId,
            ParentRequirementId = parentRequirementId == Guid.Empty ? null : parentRequirementId,
            Code = code.Trim(),
            Title = title.Trim(),
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            RequirementType = type,
            SortOrder = sortOrder,
            IsActive = true,
        };
    }

    public void Update(string title, string? text, FrameworkRequirementType type, int? sortOrder, bool isActive, Guid? parentRequirementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        RequirementType = type;
        SortOrder = sortOrder;
        IsActive = isActive;
        ParentRequirementId = parentRequirementId == Guid.Empty ? null : parentRequirementId;
    }
}

public sealed class ControlMapping
{
    private ControlMapping() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public Guid FrameworkRequirementId { get; private set; }
    public MappingRelationship Relationship { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ControlMapping Create(
        Guid internalControlId, Guid frameworkRequirementId, MappingRelationship relationship,
        Guid createdByUserId, DateTimeOffset utcNow, string? notes = null)
    {
        if (internalControlId == Guid.Empty) throw new ArgumentException("Control required.", nameof(internalControlId));
        if (frameworkRequirementId == Guid.Empty) throw new ArgumentException("Requirement required.", nameof(frameworkRequirementId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("User required.", nameof(createdByUserId));
        return new ControlMapping
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = internalControlId,
            FrameworkRequirementId = frameworkRequirementId,
            Relationship = relationship,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class ControlAssessment
{
    private ControlAssessment() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public Guid? FrameworkVersionId { get; private set; }
    public DateOnly? PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
    public AssessmentStatus Status { get; private set; }
    public AssessmentResult Result { get; private set; }
    public Guid? AssessorUserId { get; private set; }
    public DateTimeOffset? AssessmentDateUtc { get; private set; }
    public string? Notes { get; private set; }
    public Guid? TestProcedureId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ControlAssessment Create(
        Guid internalControlId, DateTimeOffset utcNow,
        Guid? frameworkVersionId = null, DateOnly? periodStart = null, DateOnly? periodEnd = null,
        Guid? assessorUserId = null, Guid? testProcedureId = null, string? notes = null)
    {
        if (internalControlId == Guid.Empty) throw new ArgumentException("Control required.", nameof(internalControlId));
        return new ControlAssessment
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = internalControlId,
            FrameworkVersionId = frameworkVersionId == Guid.Empty ? null : frameworkVersionId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = AssessmentStatus.NotStarted,
            Result = AssessmentResult.NotTested,
            AssessorUserId = assessorUserId == Guid.Empty ? null : assessorUserId,
            TestProcedureId = testProcedureId == Guid.Empty ? null : testProcedureId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Start(Guid? assessorUserId, DateTimeOffset utcNow)
    {
        if (Status == AssessmentStatus.Complete)
            throw new InvalidOperationException("Completed assessments cannot be restarted.");
        Status = AssessmentStatus.InProgress;
        if (assessorUserId is Guid id && id != Guid.Empty) AssessorUserId = id;
        UpdatedAtUtc = utcNow;
    }

    public void RecordResult(AssessmentResult result, string? notes, DateTimeOffset utcNow)
    {
        if (Status == AssessmentStatus.NotStarted)
            Status = AssessmentStatus.InProgress;
        if (Status == AssessmentStatus.Complete)
            throw new InvalidOperationException("Completed assessments cannot be edited. Create a new assessment.");
        Result = result;
        if (notes is not null) Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void Complete(AssessmentResult result, Guid? assessorUserId, string? notes, DateTimeOffset utcNow)
    {
        if (Status == AssessmentStatus.Complete) return;
        Result = result;
        if (assessorUserId is Guid id && id != Guid.Empty) AssessorUserId = id;
        if (notes is not null) Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        AssessmentDateUtc = utcNow;
        Status = AssessmentStatus.Complete;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class ComplianceCalendarItem
{
    private ComplianceCalendarItem() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public CalendarItemType ItemType { get; private set; }
    public Guid? InternalControlId { get; private set; }
    public Guid? FrameworkVersionId { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public CalendarItemStatus Status { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ComplianceCalendarItem Create(
        string title, CalendarItemType itemType, DateTimeOffset dueAtUtc, DateTimeOffset utcNow,
        Guid? internalControlId = null, Guid? frameworkVersionId = null, Guid? ownerUserId = null, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new ComplianceCalendarItem
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            ItemType = itemType,
            InternalControlId = internalControlId == Guid.Empty ? null : internalControlId,
            FrameworkVersionId = frameworkVersionId == Guid.Empty ? null : frameworkVersionId,
            DueAtUtc = dueAtUtc,
            OwnerUserId = ownerUserId == Guid.Empty ? null : ownerUserId,
            Status = CalendarItemStatus.Planned,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string title, DateTimeOffset dueAtUtc, Guid? ownerUserId, string? notes, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        DueAtUtc = dueAtUtc;
        OwnerUserId = ownerUserId == Guid.Empty ? null : ownerUserId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void SetStatus(CalendarItemStatus status, DateTimeOffset utcNow)
    {
        Status = status;
        CompletedAtUtc = status == CalendarItemStatus.Completed ? utcNow : null;
        UpdatedAtUtc = utcNow;
    }
}
