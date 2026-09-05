namespace Qec.Itmg.Security.Domain;

public enum VulnerabilitySeverity
{
    Informational = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum VulnerabilityStatus
{
    Open = 0,
    InRemediation = 1,
    Resolved = 2,
    AcceptedRisk = 3,
    FalsePositive = 4,
}

public enum VulnerabilityRemediationLinkType
{
    ChangeRequest = 0,
    Ticket = 1,
    Finding = 2,
    CorrectiveAction = 3,
}

public enum RiskStatus
{
    Identified = 0,
    Analyzed = 1,
    Treatment = 2,
    Monitoring = 3,
    Closed = 4,
}

public enum RiskTreatment
{
    Avoid = 0,
    Mitigate = 1,
    Transfer = 2,
    Accept = 3,
}

public enum PolicyExceptionStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
    Closed = 5,
}

public enum PenetrationTestStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum PentestFindingStatus
{
    Open = 0,
    Linked = 1,
    Closed = 2,
}

public enum AwarenessCampaignStatus
{
    Draft = 0,
    Open = 1, // Active for employees
    Closed = 2,
}

public enum AwarenessCompletionStatus
{
    Assigned = 0,
    Completed = 1,
    Exempt = 2,
}

public enum AwarenessModuleStatus
{
    Draft = 0,
    Active = 1,
    Inactive = 2,
}

public sealed class Vulnerability
{
    private Vulnerability() { }

    public Guid Id { get; private set; }
    public string VulnerabilityNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid ConfigurationItemId { get; private set; }
    public string Source { get; private set; } = null!;
    public string? ExternalReference { get; private set; }
    public VulnerabilitySeverity Severity { get; private set; }
    public DateTimeOffset DetectedAtUtc { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public VulnerabilityStatus Status { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public string? ResolutionSummary { get; private set; }
    public string? AcceptedRiskReason { get; private set; }
    public Guid? ExceptionId { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsOverdue(DateTimeOffset utcNow) =>
        DueAtUtc is DateTimeOffset due
        && due < utcNow
        && Status is VulnerabilityStatus.Open or VulnerabilityStatus.InRemediation;

    public static Vulnerability Create(
        string number, string title, Guid configurationItemId, string source,
        VulnerabilitySeverity severity, DateTimeOffset detectedAtUtc, DateTimeOffset utcNow,
        string? description = null, string? externalReference = null, DateTimeOffset? dueAtUtc = null,
        Guid? ownerUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (configurationItemId == Guid.Empty) throw new ArgumentException("CI required.", nameof(configurationItemId));
        return new Vulnerability
        {
            Id = Guid.CreateVersion7(),
            VulnerabilityNumber = number.Trim(),
            Title = title.Trim(),
            Description = TrimOrNull(description),
            ConfigurationItemId = configurationItemId,
            Source = source.Trim(),
            ExternalReference = TrimOrNull(externalReference),
            Severity = severity,
            DetectedAtUtc = detectedAtUtc,
            DueAtUtc = dueAtUtc,
            Status = VulnerabilityStatus.Open,
            OwnerUserId = EmptyToNull(ownerUserId),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title, string? description, VulnerabilitySeverity severity, DateTimeOffset? dueAtUtc,
        Guid? ownerUserId, DateTimeOffset utcNow)
    {
        EnsureOpen();
        Title = title.Trim();
        Description = TrimOrNull(description);
        Severity = severity;
        DueAtUtc = dueAtUtc;
        OwnerUserId = EmptyToNull(ownerUserId);
        UpdatedAtUtc = utcNow;
    }

    public void Transition(
        VulnerabilityStatus next, DateTimeOffset utcNow,
        string? resolutionSummary = null, string? acceptedRiskReason = null, Guid? exceptionId = null,
        bool hasRemediationLink = false)
    {
        if (Status == next) return;
        if (!IsAllowed(Status, next))
            throw new InvalidOperationException($"Cannot transition vulnerability from {Status} to {next}.");

        if (next == VulnerabilityStatus.Resolved)
        {
            if (!hasRemediationLink && string.IsNullOrWhiteSpace(resolutionSummary))
                throw new InvalidOperationException("Resolved requires a remediation link or resolution summary.");
            ResolutionSummary = TrimOrNull(resolutionSummary) ?? ResolutionSummary;
        }

        if (next == VulnerabilityStatus.FalsePositive)
        {
            if (string.IsNullOrWhiteSpace(acceptedRiskReason) && string.IsNullOrWhiteSpace(resolutionSummary))
                throw new InvalidOperationException("FalsePositive requires a documented reason.");
            AcceptedRiskReason = TrimOrNull(acceptedRiskReason) ?? TrimOrNull(resolutionSummary);
        }

        if (next == VulnerabilityStatus.AcceptedRisk)
        {
            if (exceptionId is null || exceptionId == Guid.Empty)
                throw new InvalidOperationException("AcceptedRisk requires an approved exception.");
            if (string.IsNullOrWhiteSpace(acceptedRiskReason))
                throw new InvalidOperationException("AcceptedRisk requires a documented reason.");
            ExceptionId = exceptionId;
            AcceptedRiskReason = acceptedRiskReason.Trim();
        }

        Status = next;
        UpdatedAtUtc = utcNow;
        ResolvedAtUtc = next is VulnerabilityStatus.Resolved or VulnerabilityStatus.AcceptedRisk or VulnerabilityStatus.FalsePositive
            ? utcNow : null;
    }

    private void EnsureOpen()
    {
        if (Status is VulnerabilityStatus.Resolved or VulnerabilityStatus.AcceptedRisk or VulnerabilityStatus.FalsePositive)
            throw new InvalidOperationException("Closed vulnerabilities cannot be edited.");
    }

    private static bool IsAllowed(VulnerabilityStatus from, VulnerabilityStatus to) => (from, to) switch
    {
        (VulnerabilityStatus.Open, VulnerabilityStatus.InRemediation) => true,
        (VulnerabilityStatus.Open, VulnerabilityStatus.Resolved) => true,
        (VulnerabilityStatus.Open, VulnerabilityStatus.AcceptedRisk) => true,
        (VulnerabilityStatus.Open, VulnerabilityStatus.FalsePositive) => true,
        (VulnerabilityStatus.InRemediation, VulnerabilityStatus.Resolved) => true,
        (VulnerabilityStatus.InRemediation, VulnerabilityStatus.AcceptedRisk) => true,
        (VulnerabilityStatus.InRemediation, VulnerabilityStatus.Open) => true,
        _ => false,
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? EmptyToNull(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}

public sealed class VulnerabilityRemediationLink
{
    private VulnerabilityRemediationLink() { }

    public Guid Id { get; private set; }
    public Guid VulnerabilityId { get; private set; }
    public VulnerabilityRemediationLinkType LinkType { get; private set; }
    public Guid TargetId { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VulnerabilityRemediationLink Create(
        Guid vulnerabilityId, VulnerabilityRemediationLinkType linkType, Guid targetId,
        Guid createdByUserId, DateTimeOffset utcNow, string? notes = null)
    {
        if (vulnerabilityId == Guid.Empty) throw new ArgumentException("Vulnerability required.");
        if (targetId == Guid.Empty) throw new ArgumentException("Target required.");
        return new VulnerabilityRemediationLink
        {
            Id = Guid.CreateVersion7(),
            VulnerabilityId = vulnerabilityId,
            LinkType = linkType,
            TargetId = targetId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class Risk
{
    private Risk() { }

    public Guid Id { get; private set; }
    public string RiskNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public Guid? BusinessServiceId { get; private set; }
    public Guid? InternalControlId { get; private set; }
    public RiskStatus Status { get; private set; }
    public int Likelihood { get; private set; }
    public int Impact { get; private set; }
    public int InherentScore { get; private set; }
    public int? ResidualLikelihood { get; private set; }
    public int? ResidualImpact { get; private set; }
    public int? ResidualScore { get; private set; }
    public RiskTreatment Treatment { get; private set; }
    public string? TreatmentPlan { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Risk Create(
        string number, string title, string description, string category, Guid ownerUserId,
        int likelihood, int impact, RiskTreatment treatment, DateTimeOffset utcNow,
        Guid? configurationItemId = null, Guid? businessServiceId = null, Guid? internalControlId = null,
        string? treatmentPlan = null, DateOnly? targetDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.");
        ValidateScore(likelihood, nameof(likelihood));
        ValidateScore(impact, nameof(impact));
        return new Risk
        {
            Id = Guid.CreateVersion7(),
            RiskNumber = number.Trim(),
            Title = title.Trim(),
            Description = description.Trim(),
            Category = category.Trim(),
            OwnerUserId = ownerUserId,
            ConfigurationItemId = EmptyToNull(configurationItemId),
            BusinessServiceId = EmptyToNull(businessServiceId),
            InternalControlId = EmptyToNull(internalControlId),
            Status = RiskStatus.Identified,
            Likelihood = likelihood,
            Impact = impact,
            InherentScore = likelihood * impact,
            Treatment = treatment,
            TreatmentPlan = TrimOrNull(treatmentPlan),
            TargetDate = targetDate,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateAnalysis(
        int likelihood, int impact, int? residualLikelihood, int? residualImpact,
        RiskTreatment treatment, string? treatmentPlan, DateOnly? targetDate, DateTimeOffset utcNow)
    {
        if (Status == RiskStatus.Closed) throw new InvalidOperationException("Closed risks cannot be edited.");
        ValidateScore(likelihood, nameof(likelihood));
        ValidateScore(impact, nameof(impact));
        Likelihood = likelihood;
        Impact = impact;
        InherentScore = likelihood * impact;
        if (residualLikelihood is int rl)
        {
            ValidateScore(rl, nameof(residualLikelihood));
            ResidualLikelihood = rl;
        }
        if (residualImpact is int ri)
        {
            ValidateScore(ri, nameof(residualImpact));
            ResidualImpact = ri;
        }
        if (ResidualLikelihood is int rL && ResidualImpact is int rI)
            ResidualScore = rL * rI;
        Treatment = treatment;
        TreatmentPlan = TrimOrNull(treatmentPlan);
        TargetDate = targetDate;
        UpdatedAtUtc = utcNow;
    }

    public void Transition(RiskStatus next, DateTimeOffset utcNow)
    {
        if (Status == next) return;
        if (!IsAllowed(Status, next))
            throw new InvalidOperationException($"Cannot transition risk from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
        ClosedAtUtc = next == RiskStatus.Closed ? utcNow : null;
    }

    private static bool IsAllowed(RiskStatus from, RiskStatus to) => (from, to) switch
    {
        (RiskStatus.Identified, RiskStatus.Analyzed) => true,
        (RiskStatus.Analyzed, RiskStatus.Treatment) => true,
        (RiskStatus.Treatment, RiskStatus.Monitoring) => true,
        (RiskStatus.Monitoring, RiskStatus.Closed) => true,
        (RiskStatus.Treatment, RiskStatus.Closed) => true,
        _ => false,
    };

    private static void ValidateScore(int value, string name)
    {
        if (value is < 1 or > 5) throw new ArgumentOutOfRangeException(name, "Score must be 1–5.");
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? EmptyToNull(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}

public sealed class RiskLink
{
    private RiskLink() { }

    public Guid Id { get; private set; }
    public Guid RiskId { get; private set; }
    public string TargetType { get; private set; } = null!;
    public Guid TargetId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static RiskLink Create(Guid riskId, string targetType, Guid targetId, Guid createdByUserId, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        if (targetId == Guid.Empty) throw new ArgumentException("Target required.");
        return new RiskLink
        {
            Id = Guid.CreateVersion7(),
            RiskId = riskId,
            TargetType = targetType.Trim(),
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class PolicyException
{
    private PolicyException() { }

    public Guid Id { get; private set; }
    public string ExceptionNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public Guid? ManagedDocumentId { get; private set; }
    public Guid? InternalControlId { get; private set; }
    public Guid? RiskId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset StartAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public PolicyExceptionStatus Status { get; private set; }
    public string? CompensatingControls { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsExpired(DateTimeOffset utcNow) =>
        Status == PolicyExceptionStatus.Expired
        || (Status == PolicyExceptionStatus.Approved && ExpiresAtUtc < utcNow);

    public int? DaysToExpiry(DateTimeOffset utcNow) =>
        Status == PolicyExceptionStatus.Approved
            ? (int)Math.Floor((ExpiresAtUtc - utcNow).TotalDays)
            : null;

    public static PolicyException Create(
        string number, string title, string reason, Guid requestedByUserId,
        DateTimeOffset startAtUtc, DateTimeOffset expiresAtUtc, DateTimeOffset utcNow,
        Guid? managedDocumentId = null, Guid? internalControlId = null, Guid? riskId = null,
        Guid? configurationItemId = null, Guid? ownerUserId = null, string? compensatingControls = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("Requester required.");
        if (expiresAtUtc <= startAtUtc) throw new ArgumentException("Expiry must be after start.");
        return new PolicyException
        {
            Id = Guid.CreateVersion7(),
            ExceptionNumber = number.Trim(),
            Title = title.Trim(),
            Reason = reason.Trim(),
            ManagedDocumentId = EmptyToNull(managedDocumentId),
            InternalControlId = EmptyToNull(internalControlId),
            RiskId = EmptyToNull(riskId),
            ConfigurationItemId = EmptyToNull(configurationItemId),
            RequestedByUserId = requestedByUserId,
            OwnerUserId = EmptyToNull(ownerUserId),
            StartAtUtc = startAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            Status = PolicyExceptionStatus.Draft,
            CompensatingControls = TrimOrNull(compensatingControls),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void SubmitForApproval(DateTimeOffset utcNow)
    {
        if (Status != PolicyExceptionStatus.Draft)
            throw new InvalidOperationException("Only draft exceptions can be submitted.");
        Status = PolicyExceptionStatus.PendingApproval;
        UpdatedAtUtc = utcNow;
    }

    public void Approve(Guid approverUserId, DateTimeOffset utcNow)
    {
        if (Status != PolicyExceptionStatus.PendingApproval)
            throw new InvalidOperationException("Only pending exceptions can be approved.");
        if (approverUserId == RequestedByUserId)
            throw new InvalidOperationException("Requester cannot approve their own exception.");
        ApprovedByUserId = approverUserId;
        Status = PolicyExceptionStatus.Approved;
        UpdatedAtUtc = utcNow;
    }

    public void Reject(Guid approverUserId, string reason, DateTimeOffset utcNow)
    {
        if (Status != PolicyExceptionStatus.PendingApproval)
            throw new InvalidOperationException("Only pending exceptions can be rejected.");
        if (approverUserId == RequestedByUserId)
            throw new InvalidOperationException("Requester cannot reject their own exception.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RejectionReason = reason.Trim();
        Status = PolicyExceptionStatus.Rejected;
        UpdatedAtUtc = utcNow;
    }

    public void MarkExpired(DateTimeOffset utcNow)
    {
        if (Status != PolicyExceptionStatus.Approved) return;
        Status = PolicyExceptionStatus.Expired;
        UpdatedAtUtc = utcNow;
    }

    public void Close(DateTimeOffset utcNow)
    {
        if (Status is not (PolicyExceptionStatus.Approved or PolicyExceptionStatus.Expired or PolicyExceptionStatus.Rejected))
            throw new InvalidOperationException("Cannot close exception in current status.");
        Status = PolicyExceptionStatus.Closed;
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? EmptyToNull(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}

public sealed class PenetrationTest
{
    private PenetrationTest() { }

    public Guid Id { get; private set; }
    public string PentestNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Provider { get; private set; }
    public string ScopeSummary { get; private set; } = null!;
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public PenetrationTestStatus Status { get; private set; }
    public Guid? ReportEvidenceId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PenetrationTest Create(
        string number, string title, string scopeSummary, DateTimeOffset utcNow,
        string? provider = null, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeSummary);
        return new PenetrationTest
        {
            Id = Guid.CreateVersion7(),
            PentestNumber = number.Trim(),
            Title = title.Trim(),
            Provider = TrimOrNull(provider),
            ScopeSummary = scopeSummary.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Status = PenetrationTestStatus.Planned,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string title, string scopeSummary, string? provider, DateOnly? startDate, DateOnly? endDate, Guid? reportEvidenceId, DateTimeOffset utcNow)
    {
        Title = title.Trim();
        ScopeSummary = scopeSummary.Trim();
        Provider = TrimOrNull(provider);
        StartDate = startDate;
        EndDate = endDate;
        ReportEvidenceId = EmptyToNull(reportEvidenceId);
        UpdatedAtUtc = utcNow;
    }

    public void Transition(PenetrationTestStatus next, DateTimeOffset utcNow)
    {
        if (Status == next) return;
        bool ok = (Status, next) switch
        {
            (PenetrationTestStatus.Planned, PenetrationTestStatus.InProgress) => true,
            (PenetrationTestStatus.Planned, PenetrationTestStatus.Cancelled) => true,
            (PenetrationTestStatus.InProgress, PenetrationTestStatus.Completed) => true,
            (PenetrationTestStatus.InProgress, PenetrationTestStatus.Cancelled) => true,
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"Cannot transition pentest from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? EmptyToNull(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}

public sealed class PentestFinding
{
    private PentestFinding() { }

    public Guid Id { get; private set; }
    public Guid PenetrationTestId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public VulnerabilitySeverity Severity { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public PentestFindingStatus Status { get; private set; }
    public Guid? VulnerabilityId { get; private set; }
    public Guid? AuditFindingId { get; private set; }
    public Guid? EvidenceId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static PentestFinding Create(
        Guid penetrationTestId, string title, string description, VulnerabilitySeverity severity,
        DateTimeOffset utcNow, Guid? configurationItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new PentestFinding
        {
            Id = Guid.CreateVersion7(),
            PenetrationTestId = penetrationTestId,
            Title = title.Trim(),
            Description = description.Trim(),
            Severity = severity,
            ConfigurationItemId = configurationItemId == Guid.Empty ? null : configurationItemId,
            Status = PentestFindingStatus.Open,
            CreatedAtUtc = utcNow,
        };
    }

    public void Link(Guid? vulnerabilityId, Guid? auditFindingId, Guid? evidenceId)
    {
        VulnerabilityId = vulnerabilityId == Guid.Empty ? null : vulnerabilityId;
        AuditFindingId = auditFindingId == Guid.Empty ? null : auditFindingId;
        EvidenceId = evidenceId == Guid.Empty ? null : evidenceId;
        if (VulnerabilityId is not null || AuditFindingId is not null || EvidenceId is not null)
            Status = PentestFindingStatus.Linked;
    }

    public void Close() => Status = PentestFindingStatus.Closed;
}

public sealed class AwarenessModule
{
    private AwarenessModule() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Summary { get; private set; }
    public string Body { get; private set; } = null!;
    public int Version { get; private set; }
    public AwarenessModuleStatus Status { get; private set; }
    public int EstimatedMinutes { get; private set; }
    public int PassThresholdPercent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static AwarenessModule Create(
        string code,
        string title,
        string body,
        DateTimeOffset utcNow,
        string? summary = null,
        int version = 1,
        int estimatedMinutes = 5,
        int passThresholdPercent = 80,
        AwarenessModuleStatus status = AwarenessModuleStatus.Draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (estimatedMinutes < 1) throw new ArgumentOutOfRangeException(nameof(estimatedMinutes));
        if (passThresholdPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(passThresholdPercent));
        return new AwarenessModule
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Title = title.Trim(),
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            Body = body.Trim(),
            Version = version,
            Status = status,
            EstimatedMinutes = estimatedMinutes,
            PassThresholdPercent = passThresholdPercent,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Activate(DateTimeOffset utcNow)
    {
        Status = AwarenessModuleStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        Status = AwarenessModuleStatus.Inactive;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class AwarenessQuestion
{
    private AwarenessQuestion() { }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public string QuestionText { get; private set; } = null!;
    public int DisplayOrder { get; private set; }

    public static AwarenessQuestion Create(Guid moduleId, string questionText, int displayOrder)
    {
        if (moduleId == Guid.Empty) throw new ArgumentException("Module required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(questionText);
        return new AwarenessQuestion
        {
            Id = Guid.CreateVersion7(),
            ModuleId = moduleId,
            QuestionText = questionText.Trim(),
            DisplayOrder = displayOrder,
        };
    }
}

public sealed class AwarenessAnswerOption
{
    private AwarenessAnswerOption() { }

    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Text { get; private set; } = null!;
    public bool IsCorrect { get; private set; }
    public int DisplayOrder { get; private set; }

    public static AwarenessAnswerOption Create(Guid questionId, string text, bool isCorrect, int displayOrder)
    {
        if (questionId == Guid.Empty) throw new ArgumentException("Question required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new AwarenessAnswerOption
        {
            Id = Guid.CreateVersion7(),
            QuestionId = questionId,
            Text = text.Trim(),
            IsCorrect = isCorrect,
            DisplayOrder = displayOrder,
        };
    }
}

public sealed class AwarenessCampaign
{
    private AwarenessCampaign() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid? ModuleId { get; private set; }
    public int? ModuleVersion { get; private set; }
    public int PassThresholdPercent { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public AwarenessCampaignStatus Status { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AwarenessCampaign Create(
        string title, Guid ownerUserId, DateTimeOffset startsAtUtc, DateTimeOffset utcNow,
        string? description = null, DateTimeOffset? dueAtUtc = null,
        Guid? moduleId = null, int? moduleVersion = null, int passThresholdPercent = 80)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner required.");
        if (passThresholdPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(passThresholdPercent));
        return new AwarenessCampaign
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ModuleId = moduleId is null || moduleId == Guid.Empty ? null : moduleId,
            ModuleVersion = moduleVersion,
            PassThresholdPercent = passThresholdPercent,
            StartsAtUtc = startsAtUtc,
            DueAtUtc = dueAtUtc,
            Status = AwarenessCampaignStatus.Draft,
            OwnerUserId = ownerUserId,
            CreatedAtUtc = utcNow,
        };
    }

    public void Open() => Status = AwarenessCampaignStatus.Open;
    public void Close() => Status = AwarenessCampaignStatus.Closed;
}

public sealed class AwarenessCompletion
{
    private AwarenessCompletion() { }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid UserId { get; private set; }
    public AwarenessCompletionStatus Status { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public int? Score { get; private set; }
    public int AttemptCount { get; private set; }
    public int? ModuleVersion { get; private set; }
    public Guid? EvidenceId { get; private set; }
    public string? Notes { get; private set; }

    public static AwarenessCompletion Assign(
        Guid campaignId, Guid userId, DateTimeOffset utcNow, DateTimeOffset? dueAtUtc = null, int? moduleVersion = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            UserId = userId,
            Status = AwarenessCompletionStatus.Assigned,
            AssignedAtUtc = utcNow,
            DueAtUtc = dueAtUtc,
            AttemptCount = 0,
            ModuleVersion = moduleVersion,
        };

    public void MarkStarted(DateTimeOffset utcNow)
    {
        if (StartedAtUtc is not null) return;
        StartedAtUtc = utcNow;
    }

    public void RecordAttempt(int score, bool passed, DateTimeOffset utcNow)
    {
        AttemptCount += 1;
        Score = score;
        if (passed)
        {
            Status = AwarenessCompletionStatus.Completed;
            CompletedAtUtc = utcNow;
        }
    }

    public void Complete(DateTimeOffset utcNow, Guid? evidenceId = null, string? notes = null)
    {
        Status = AwarenessCompletionStatus.Completed;
        CompletedAtUtc = utcNow;
        EvidenceId = evidenceId == Guid.Empty ? null : evidenceId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void Exempt(string? notes = null)
    {
        Status = AwarenessCompletionStatus.Exempt;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

public sealed class AwarenessAttempt
{
    private AwarenessAttempt() { }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public int AttemptNumber { get; private set; }
    public int Score { get; private set; }
    public bool Passed { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public static AwarenessAttempt Create(
        Guid assignmentId, int attemptNumber, int score, bool passed, DateTimeOffset utcNow)
    {
        if (assignmentId == Guid.Empty) throw new ArgumentException("Assignment required.");
        if (attemptNumber < 1) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        return new AwarenessAttempt
        {
            Id = Guid.CreateVersion7(),
            AssignmentId = assignmentId,
            AttemptNumber = attemptNumber,
            Score = score,
            Passed = passed,
            SubmittedAtUtc = utcNow,
        };
    }
}

public sealed class AwarenessReminderLog
{
    private AwarenessReminderLog() { }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid UserId { get; private set; }
    public string ReminderKind { get; private set; } = null!;
    public DateTimeOffset NotifiedAtUtc { get; private set; }

    public static AwarenessReminderLog Create(
        Guid assignmentId, Guid userId, string reminderKind, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reminderKind);
        return new AwarenessReminderLog
        {
            Id = Guid.CreateVersion7(),
            AssignmentId = assignmentId,
            UserId = userId,
            ReminderKind = reminderKind.Trim(),
            NotifiedAtUtc = utcNow,
        };
    }
}

public sealed class ExceptionExpiryNotificationLog
{
    private ExceptionExpiryNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid ExceptionId { get; private set; }
    public string EventKey { get; private set; } = null!;
    public DateTimeOffset SentAtUtc { get; private set; }

    public static ExceptionExpiryNotificationLog Create(Guid exceptionId, string eventKey, DateTimeOffset utcNow) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ExceptionId = exceptionId,
            EventKey = eventKey,
            SentAtUtc = utcNow,
        };
}
