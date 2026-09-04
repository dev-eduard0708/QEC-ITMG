namespace Qec.Itmg.Governance.Domain;

/// <summary>QEC internal control domain codes — lookup data, not framework requirement IDs.</summary>
public static class ControlDomainCodes
{
    public const string AccessManagement = "IAM";
    public const string ChangeManagement = "CHG";
    public const string ItOperations = "OPS";
    public const string ServiceManagement = "SVC";
    public const string Security = "SEC";
    public const string BusinessContinuity = "BCM";
    public const string VendorManagement = "VND";
    public const string Governance = "GOV";
    public const string Other = "OTH";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AccessManagement] = "Access Management",
        [ChangeManagement] = "Change Management",
        [ItOperations] = "IT Operations",
        [ServiceManagement] = "Service Management",
        [Security] = "Security",
        [BusinessContinuity] = "Business Continuity",
        [VendorManagement] = "Vendor Management",
        [Governance] = "Governance",
        [Other] = "Other",
    };

    public static bool IsKnown(string code) => Labels.ContainsKey(code.Trim());

    public static string Normalize(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        string key = code.Trim().ToUpperInvariant();
        if (!Labels.ContainsKey(key))
            throw new ArgumentException($"Unknown control domain code '{code}'.", nameof(code));
        return key;
    }
}

public enum ControlFrequency
{
    Continuous = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    SemiAnnual = 5,
    Annual = 6,
    EventDriven = 7,
    AdHoc = 8,
}

public enum ControlAutomationType
{
    Manual = 0,
    Automated = 1,
    ItmgNative = 2,
}

public enum ControlStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2,
}

public sealed class InternalControl
{
    private InternalControl() { }

    public Guid Id { get; private set; }
    public string ControlNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Objective { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Domain { get; private set; } = null!;
    public ControlFrequency Frequency { get; private set; }
    public ControlAutomationType AutomationType { get; private set; }
    public ControlStatus Status { get; private set; }
    public Guid? PrimaryOwnerUserId { get; private set; }
    public Guid? PrimaryOwnerRoleId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static InternalControl Create(
        string controlNumber,
        string title,
        string objective,
        string description,
        string domain,
        ControlFrequency frequency,
        ControlAutomationType automationType,
        DateTimeOffset utcNow,
        Guid? primaryOwnerUserId = null,
        Guid? primaryOwnerRoleId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new InternalControl
        {
            Id = Guid.CreateVersion7(),
            ControlNumber = controlNumber.Trim(),
            Title = title.Trim(),
            Objective = objective.Trim(),
            Description = description.Trim(),
            Domain = ControlDomainCodes.Normalize(domain),
            Frequency = frequency,
            AutomationType = automationType,
            Status = ControlStatus.Draft,
            PrimaryOwnerUserId = Norm(primaryOwnerUserId),
            PrimaryOwnerRoleId = Norm(primaryOwnerRoleId),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title,
        string objective,
        string description,
        ControlFrequency frequency,
        ControlAutomationType automationType,
        Guid? primaryOwnerUserId,
        Guid? primaryOwnerRoleId,
        DateTimeOffset utcNow)
    {
        if (Status == ControlStatus.Retired)
            throw new InvalidOperationException("Retired controls cannot be updated.");
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Title = title.Trim();
        Objective = objective.Trim();
        Description = description.Trim();
        Frequency = frequency;
        AutomationType = automationType;
        PrimaryOwnerUserId = Norm(primaryOwnerUserId);
        PrimaryOwnerRoleId = Norm(primaryOwnerRoleId);
        UpdatedAtUtc = utcNow;
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (Status == ControlStatus.Retired)
            throw new InvalidOperationException("Retired controls cannot be activated.");
        if (Status == ControlStatus.Active) return;
        Status = ControlStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Retire(DateTimeOffset utcNow)
    {
        if (Status == ControlStatus.Retired) return;
        Status = ControlStatus.Retired;
        RetiredAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    private static Guid? Norm(Guid? id) => id is null || id == Guid.Empty ? null : id;
}

public sealed class ControlSecondaryOwner
{
    private ControlSecondaryOwner() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ControlSecondaryOwner Create(Guid controlId, Guid userId, DateTimeOffset utcNow)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("Control is required.", nameof(controlId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        return new ControlSecondaryOwner
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = controlId,
            UserId = userId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class ControlConfigurationItemLink
{
    private ControlConfigurationItemLink() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public Guid ConfigurationItemId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ControlConfigurationItemLink Create(Guid controlId, Guid configurationItemId, DateTimeOffset utcNow)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("Control is required.", nameof(controlId));
        if (configurationItemId == Guid.Empty) throw new ArgumentException("CI is required.", nameof(configurationItemId));
        return new ControlConfigurationItemLink
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = controlId,
            ConfigurationItemId = configurationItemId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class ControlBusinessServiceLink
{
    private ControlBusinessServiceLink() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public Guid BusinessServiceId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ControlBusinessServiceLink Create(Guid controlId, Guid businessServiceId, DateTimeOffset utcNow)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("Control is required.", nameof(controlId));
        if (businessServiceId == Guid.Empty) throw new ArgumentException("Service is required.", nameof(businessServiceId));
        return new ControlBusinessServiceLink
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = controlId,
            BusinessServiceId = businessServiceId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class ControlManagedDocumentLink
{
    private ControlManagedDocumentLink() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ControlManagedDocumentLink Create(Guid controlId, Guid managedDocumentId, DateTimeOffset utcNow)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("Control is required.", nameof(controlId));
        if (managedDocumentId == Guid.Empty) throw new ArgumentException("Document is required.", nameof(managedDocumentId));
        return new ControlManagedDocumentLink
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = controlId,
            ManagedDocumentId = managedDocumentId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class ControlTestProcedure
{
    private ControlTestProcedure() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Purpose { get; private set; }
    public string ProcedureSteps { get; private set; } = null!;
    public string ExpectedResult { get; private set; } = null!;
    public string? SampleGuidance { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ControlTestProcedure Create(
        Guid controlId,
        string title,
        string procedureSteps,
        string expectedResult,
        DateTimeOffset utcNow,
        string? purpose = null,
        string? sampleGuidance = null)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("Control is required.", nameof(controlId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureSteps);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedResult);
        return new ControlTestProcedure
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = controlId,
            Title = title.Trim(),
            Purpose = TrimOrNull(purpose),
            ProcedureSteps = procedureSteps.Trim(),
            ExpectedResult = expectedResult.Trim(),
            SampleGuidance = TrimOrNull(sampleGuidance),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title,
        string? purpose,
        string procedureSteps,
        string expectedResult,
        string? sampleGuidance,
        bool isActive,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureSteps);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedResult);
        Title = title.Trim();
        Purpose = TrimOrNull(purpose);
        ProcedureSteps = procedureSteps.Trim();
        ExpectedResult = expectedResult.Trim();
        SampleGuidance = TrimOrNull(sampleGuidance);
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class EvidenceRequirement
{
    private EvidenceRequirement() { }

    public Guid Id { get; private set; }
    public Guid InternalControlId { get; private set; }
    public string Description { get; private set; } = null!;
    public ControlFrequency? Frequency { get; private set; }
    public string? RetentionNotes { get; private set; }
    public bool IsRequired { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static EvidenceRequirement Create(
        Guid controlId,
        string description,
        DateTimeOffset utcNow,
        ControlFrequency? frequency = null,
        string? retentionNotes = null,
        bool isRequired = true)
    {
        if (controlId == Guid.Empty) throw new ArgumentException("Control is required.", nameof(controlId));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new EvidenceRequirement
        {
            Id = Guid.CreateVersion7(),
            InternalControlId = controlId,
            Description = description.Trim(),
            Frequency = frequency,
            RetentionNotes = string.IsNullOrWhiteSpace(retentionNotes) ? null : retentionNotes.Trim(),
            IsRequired = isRequired,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string description,
        ControlFrequency? frequency,
        string? retentionNotes,
        bool isRequired,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description.Trim();
        Frequency = frequency;
        RetentionNotes = string.IsNullOrWhiteSpace(retentionNotes) ? null : retentionNotes.Trim();
        IsRequired = isRequired;
        UpdatedAtUtc = utcNow;
    }
}
