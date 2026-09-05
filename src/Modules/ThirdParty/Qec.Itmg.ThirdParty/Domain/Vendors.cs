namespace Qec.Itmg.ThirdParty.Domain;

public enum VendorStatus
{
    Active = 0,
    Inactive = 1,
}

public enum VendorCriticality
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public enum ContractStatus
{
    Draft = 0,
    Active = 1,
    Expired = 2,
    Terminated = 3,
}

public enum VendorAssessmentType
{
    DueDiligence = 0,
    Security = 1,
    Risk = 2,
    Performance = 3,
    AnnualReview = 4,
    Other = 5,
}

public enum VendorAssessmentStatus
{
    Scheduled = 0,
    InProgress = 1,
    Review = 2,
    Complete = 3,
}

public enum VendorAssessmentResult
{
    Satisfactory = 0,
    NeedsImprovement = 1,
    Unsatisfactory = 2,
    NotApplicable = 3,
}

public enum VendorLinkTargetType
{
    ConfigurationItem = 0,
    Evidence = 1,
    Risk = 2,
    InternalControl = 3,
    ManagedDocument = 4,
    AccessCase = 5,
    ManagedAccount = 6,
}

public sealed class Vendor
{
    private Vendor() { }

    public Guid Id { get; private set; }
    public string VendorNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? LegalName { get; private set; }
    public VendorStatus Status { get; private set; }
    public VendorCriticality Criticality { get; private set; }
    public string? ServiceDescription { get; private set; }
    public string? PrimaryContactName { get; private set; }
    public string? PrimaryContactEmail { get; private set; }
    public string? PrimaryContactPhone { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public Guid? RiskId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Vendor Create(
        string number, string name, VendorCriticality criticality, DateTimeOffset utcNow,
        string? legalName = null, string? serviceDescription = null,
        string? primaryContactName = null, string? primaryContactEmail = null, string? primaryContactPhone = null,
        Guid? ownerUserId = null, Guid? riskId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Vendor
        {
            Id = Guid.CreateVersion7(),
            VendorNumber = number.Trim(),
            Name = name.Trim(),
            LegalName = Trim(legalName),
            Status = VendorStatus.Active,
            Criticality = criticality,
            ServiceDescription = Trim(serviceDescription),
            PrimaryContactName = Trim(primaryContactName),
            PrimaryContactEmail = Trim(primaryContactEmail),
            PrimaryContactPhone = Trim(primaryContactPhone),
            OwnerUserId = Norm(ownerUserId),
            RiskId = Norm(riskId),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string name, VendorCriticality criticality, VendorStatus status,
        string? legalName, string? serviceDescription,
        string? primaryContactName, string? primaryContactEmail, string? primaryContactPhone,
        Guid? ownerUserId, Guid? riskId, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Criticality = criticality;
        Status = status;
        LegalName = Trim(legalName);
        ServiceDescription = Trim(serviceDescription);
        PrimaryContactName = Trim(primaryContactName);
        PrimaryContactEmail = Trim(primaryContactEmail);
        PrimaryContactPhone = Trim(primaryContactPhone);
        OwnerUserId = Norm(ownerUserId);
        RiskId = Norm(riskId);
        UpdatedAtUtc = utcNow;
    }

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static Guid? Norm(Guid? v) => v is null || v == Guid.Empty ? null : v;
}

public sealed class VendorContact
{
    private VendorContact() { }

    public Guid Id { get; private set; }
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Role { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VendorContact Create(
        Guid vendorId, string name, DateTimeOffset utcNow,
        string? email = null, string? phone = null, string? role = null, bool isPrimary = false)
    {
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new VendorContact
        {
            Id = Guid.CreateVersion7(),
            VendorId = vendorId,
            Name = name.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
            IsPrimary = isPrimary,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class Contract
{
    private Contract() { }

    public Guid Id { get; private set; }
    public string ContractNumber { get; private set; } = null!;
    public Guid VendorId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? ContractType { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateOnly? RenewalDate { get; private set; }
    public bool AutoRenew { get; private set; }
    public ContractStatus Status { get; private set; }
    public string? SlaReference { get; private set; }
    public Guid? ManagedDocumentId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Contract Create(
        string number, Guid vendorId, string title, Guid ownerUserId, DateOnly startDate, DateTimeOffset utcNow,
        string? contractType = null, DateOnly? endDate = null, DateOnly? renewalDate = null,
        bool autoRenew = false, string? slaReference = null, Guid? managedDocumentId = null, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor required.");
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.");
        return new Contract
        {
            Id = Guid.CreateVersion7(),
            ContractNumber = number.Trim(),
            VendorId = vendorId,
            Title = title.Trim(),
            ContractType = string.IsNullOrWhiteSpace(contractType) ? null : contractType.Trim(),
            OwnerUserId = ownerUserId,
            StartDate = startDate,
            EndDate = endDate,
            RenewalDate = renewalDate,
            AutoRenew = autoRenew,
            Status = ContractStatus.Draft,
            SlaReference = string.IsNullOrWhiteSpace(slaReference) ? null : slaReference.Trim(),
            ManagedDocumentId = managedDocumentId is null || managedDocumentId == Guid.Empty ? null : managedDocumentId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title, string? contractType, Guid ownerUserId, DateOnly startDate, DateOnly? endDate,
        DateOnly? renewalDate, bool autoRenew, string? slaReference, Guid? managedDocumentId, string? notes,
        DateTimeOffset utcNow)
    {
        if (Status is ContractStatus.Expired or ContractStatus.Terminated)
            throw new InvalidOperationException("Expired/terminated contracts cannot be edited.");
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        ContractType = string.IsNullOrWhiteSpace(contractType) ? null : contractType.Trim();
        OwnerUserId = ownerUserId;
        StartDate = startDate;
        EndDate = endDate;
        RenewalDate = renewalDate;
        AutoRenew = autoRenew;
        SlaReference = string.IsNullOrWhiteSpace(slaReference) ? null : slaReference.Trim();
        ManagedDocumentId = managedDocumentId is null || managedDocumentId == Guid.Empty ? null : managedDocumentId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void Transition(ContractStatus next, DateTimeOffset utcNow)
    {
        bool ok = (Status, next) switch
        {
            (ContractStatus.Draft, ContractStatus.Active) => true,
            (ContractStatus.Draft, ContractStatus.Terminated) => true,
            (ContractStatus.Active, ContractStatus.Expired) => true,
            (ContractStatus.Active, ContractStatus.Terminated) => true,
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"Cannot transition contract from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
    }

    public int? DaysToExpiry(DateOnly asOf) =>
        EndDate is DateOnly end ? end.DayNumber - asOf.DayNumber : null;

    public bool IsExpired(DateOnly asOf) =>
        Status == ContractStatus.Expired || (EndDate is DateOnly end && end < asOf && Status == ContractStatus.Active);

    public bool IsExpiringSoon(DateOnly asOf, int withinDays = 90) =>
        Status == ContractStatus.Active && DaysToExpiry(asOf) is int d && d >= 0 && d <= withinDays;
}

public sealed class VendorAssessment
{
    private VendorAssessment() { }

    public Guid Id { get; private set; }
    public string AssessmentNumber { get; private set; } = null!;
    public Guid VendorId { get; private set; }
    public VendorAssessmentType AssessmentType { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? ReviewerUserId { get; private set; }
    public DateTimeOffset? ScheduledAtUtc { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public VendorAssessmentStatus Status { get; private set; }
    public VendorAssessmentResult? Result { get; private set; }
    public string? Summary { get; private set; }
    public Guid? RiskId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static VendorAssessment Create(
        string number, Guid vendorId, VendorAssessmentType type, Guid ownerUserId, DateTimeOffset utcNow,
        Guid? reviewerUserId = null, DateTimeOffset? scheduledAtUtc = null, DateTimeOffset? dueAtUtc = null,
        Guid? riskId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor required.");
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.");
        return new VendorAssessment
        {
            Id = Guid.CreateVersion7(),
            AssessmentNumber = number.Trim(),
            VendorId = vendorId,
            AssessmentType = type,
            OwnerUserId = ownerUserId,
            ReviewerUserId = reviewerUserId is null || reviewerUserId == Guid.Empty ? null : reviewerUserId,
            ScheduledAtUtc = scheduledAtUtc ?? utcNow,
            DueAtUtc = dueAtUtc,
            Status = VendorAssessmentStatus.Scheduled,
            RiskId = riskId is null || riskId == Guid.Empty ? null : riskId,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Transition(VendorAssessmentStatus next, DateTimeOffset utcNow, VendorAssessmentResult? result = null, string? summary = null)
    {
        bool ok = (Status, next) switch
        {
            (VendorAssessmentStatus.Scheduled, VendorAssessmentStatus.InProgress) => true,
            (VendorAssessmentStatus.InProgress, VendorAssessmentStatus.Review) => true,
            (VendorAssessmentStatus.Review, VendorAssessmentStatus.Complete) => true,
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"Cannot transition assessment from {Status} to {next}.");
        if (next == VendorAssessmentStatus.Complete && result is null)
            throw new ArgumentException("Result required to complete assessment.");
        Status = next;
        if (next == VendorAssessmentStatus.Complete)
        {
            Result = result;
            Summary = string.IsNullOrWhiteSpace(summary) ? Summary : summary.Trim();
            CompletedAtUtc = utcNow;
        }
        else if (!string.IsNullOrWhiteSpace(summary))
        {
            Summary = summary.Trim();
        }
        UpdatedAtUtc = utcNow;
    }

    public bool IsOverdue(DateTimeOffset asOf) =>
        Status != VendorAssessmentStatus.Complete && DueAtUtc is DateTimeOffset due && due < asOf;
}

public sealed class VendorScopeLink
{
    private VendorScopeLink() { }

    public Guid Id { get; private set; }
    public Guid VendorId { get; private set; }
    public VendorLinkTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VendorScopeLink Create(
        Guid vendorId, VendorLinkTargetType targetType, Guid targetId, Guid createdByUserId, DateTimeOffset utcNow)
    {
        if (vendorId == Guid.Empty || targetId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Vendor, target, and creator are required.");
        return new VendorScopeLink
        {
            Id = Guid.CreateVersion7(),
            VendorId = vendorId,
            TargetType = targetType,
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class VendorNotificationLog
{
    private VendorNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid ResourceId { get; private set; }
    public string EventKey { get; private set; } = null!;
    public DateTimeOffset SentAtUtc { get; private set; }

    public static VendorNotificationLog Create(Guid resourceId, string eventKey, DateTimeOffset utcNow) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ResourceId = resourceId,
            EventKey = eventKey,
            SentAtUtc = utcNow,
        };
}
