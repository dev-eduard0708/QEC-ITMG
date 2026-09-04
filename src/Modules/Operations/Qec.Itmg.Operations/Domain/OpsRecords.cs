namespace Qec.Itmg.Operations.Domain;

public enum BackupRunStatus
{
    Running = 0,
    Success = 1,
    Warning = 2,
    Failed = 3,
}

public enum RestoreTestResult
{
    Pending = 0,
    Success = 1,
    Failed = 2,
}

public enum PatchDeploymentStatus
{
    Planned = 0,
    InProgress = 1,
    Success = 2,
    Failed = 3,
    RolledBack = 4,
}

public enum JobLastResult
{
    Unknown = 0,
    Success = 1,
    Warning = 2,
    Failed = 3,
}

public sealed class BackupJob
{
    private BackupJob() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string? ExternalJobId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static BackupJob Create(string name, string provider, DateTimeOffset utcNow, string? externalJobId = null, Guid? configurationItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        return new BackupJob
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Provider = provider.Trim(),
            ExternalJobId = Norm(externalJobId),
            ConfigurationItemId = NormGuid(configurationItemId),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string name, string provider, string? externalJobId, Guid? configurationItemId, bool isActive, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        Name = name.Trim();
        Provider = provider.Trim();
        ExternalJobId = Norm(externalJobId);
        ConfigurationItemId = NormGuid(configurationItemId);
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static Guid? NormGuid(Guid? v) => v is null || v == Guid.Empty ? null : v;
}

public sealed class BackupRun
{
    private BackupRun() { }

    public Guid Id { get; private set; }
    public Guid BackupJobId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public BackupRunStatus Status { get; private set; }
    public string? Summary { get; private set; }
    public string? ExternalReference { get; private set; }

    public static BackupRun Create(Guid backupJobId, DateTimeOffset startedAtUtc, BackupRunStatus status = BackupRunStatus.Running, string? summary = null, string? externalReference = null, DateTimeOffset? completedAtUtc = null)
    {
        if (backupJobId == Guid.Empty) throw new ArgumentException("Backup job is required.", nameof(backupJobId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        return new BackupRun
        {
            Id = Guid.CreateVersion7(),
            BackupJobId = backupJobId,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Status = status,
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim(),
        };
    }

    public void Update(BackupRunStatus status, DateTimeOffset? completedAtUtc, string? summary, string? externalReference)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        CompletedAtUtc = completedAtUtc;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
    }
}

public sealed class RestoreTest
{
    private RestoreTest() { }

    public Guid Id { get; private set; }
    public Guid? BackupJobId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public DateTimeOffset? ScheduledAtUtc { get; private set; }
    public DateTimeOffset? PerformedAtUtc { get; private set; }
    public RestoreTestResult Result { get; private set; }
    public Guid? PerformedByUserId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static RestoreTest Create(DateTimeOffset utcNow, Guid? backupJobId = null, Guid? configurationItemId = null, DateTimeOffset? scheduledAtUtc = null, string? notes = null)
    {
        return new RestoreTest
        {
            Id = Guid.CreateVersion7(),
            BackupJobId = backupJobId is null || backupJobId == Guid.Empty ? null : backupJobId,
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            ScheduledAtUtc = scheduledAtUtc,
            Result = RestoreTestResult.Pending,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = utcNow,
        };
    }

    public void Update(Guid? backupJobId, Guid? configurationItemId, DateTimeOffset? scheduledAtUtc, DateTimeOffset? performedAtUtc, RestoreTestResult result, Guid? performedByUserId, string? notes)
    {
        if (!Enum.IsDefined(result)) throw new ArgumentOutOfRangeException(nameof(result));
        BackupJobId = backupJobId is null || backupJobId == Guid.Empty ? null : backupJobId;
        ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId;
        ScheduledAtUtc = scheduledAtUtc;
        PerformedAtUtc = performedAtUtc;
        Result = result;
        PerformedByUserId = performedByUserId is null || performedByUserId == Guid.Empty ? null : performedByUserId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

public sealed class CertificateRecord
{
    private CertificateRecord() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid? ConfigurationItemId { get; private set; }
    public string? Subject { get; private set; }
    public string? Issuer { get; private set; }
    public string? Thumbprint { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static CertificateRecord Create(string name, DateTimeOffset expiresAtUtc, DateTimeOffset utcNow, Guid? configurationItemId = null, string? subject = null, string? issuer = null, string? thumbprint = null, Guid? ownerUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CertificateRecord
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
            Issuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer.Trim(),
            Thumbprint = string.IsNullOrWhiteSpace(thumbprint) ? null : thumbprint.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            OwnerUserId = ownerUserId is null || ownerUserId == Guid.Empty ? null : ownerUserId,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string name, DateTimeOffset expiresAtUtc, Guid? configurationItemId, string? subject, string? issuer, string? thumbprint, Guid? ownerUserId, bool isActive, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        ExpiresAtUtc = expiresAtUtc;
        ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId;
        Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        Issuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer.Trim();
        Thumbprint = string.IsNullOrWhiteSpace(thumbprint) ? null : thumbprint.Trim();
        OwnerUserId = ownerUserId is null || ownerUserId == Guid.Empty ? null : ownerUserId;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    public int DaysToExpiry(DateTimeOffset utcNow) =>
        (int)Math.Floor((ExpiresAtUtc - utcNow).TotalDays);

    public bool IsExpired(DateTimeOffset utcNow) => ExpiresAtUtc <= utcNow;
    public bool IsExpiringSoon(DateTimeOffset utcNow, int withinDays = 30) =>
        !IsExpired(utcNow) && DaysToExpiry(utcNow) <= withinDays;
}

public sealed class CertificateExpiryNotificationLog
{
    private CertificateExpiryNotificationLog() { }

    public Guid Id { get; private set; }
    public Guid CertificateId { get; private set; }
    public int ThresholdDays { get; private set; }
    public DateTimeOffset NotifiedAtUtc { get; private set; }

    public static CertificateExpiryNotificationLog Create(Guid certificateId, int thresholdDays, DateTimeOffset utcNow)
    {
        if (certificateId == Guid.Empty) throw new ArgumentException("Certificate is required.", nameof(certificateId));
        return new CertificateExpiryNotificationLog
        {
            Id = Guid.CreateVersion7(),
            CertificateId = certificateId,
            ThresholdDays = thresholdDays,
            NotifiedAtUtc = utcNow,
        };
    }
}

public sealed class PatchBaseline
{
    private PatchBaseline() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Version { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PatchBaseline Create(string name, DateTimeOffset utcNow, string? description = null, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new PatchBaseline
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string name, string? description, string? version, bool isActive, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class PatchDeployment
{
    private PatchDeployment() { }

    public Guid Id { get; private set; }
    public Guid? PatchBaselineId { get; private set; }
    public Guid ConfigurationItemId { get; private set; }
    public string? ExternalReference { get; private set; }
    public PatchDeploymentStatus Status { get; private set; }
    public DateTimeOffset? ScheduledAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? Summary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static PatchDeployment Create(Guid configurationItemId, DateTimeOffset utcNow, Guid? patchBaselineId = null, string? externalReference = null, DateTimeOffset? scheduledAtUtc = null, string? summary = null)
    {
        if (configurationItemId == Guid.Empty) throw new ArgumentException("CI is required.", nameof(configurationItemId));
        return new PatchDeployment
        {
            Id = Guid.CreateVersion7(),
            PatchBaselineId = patchBaselineId is null || patchBaselineId == Guid.Empty ? null : patchBaselineId,
            ConfigurationItemId = configurationItemId,
            ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim(),
            Status = PatchDeploymentStatus.Planned,
            ScheduledAtUtc = scheduledAtUtc,
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            CreatedAtUtc = utcNow,
        };
    }

    public void Update(Guid? patchBaselineId, Guid configurationItemId, string? externalReference, PatchDeploymentStatus status, DateTimeOffset? scheduledAtUtc, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc, string? summary)
    {
        if (configurationItemId == Guid.Empty) throw new ArgumentException("CI is required.", nameof(configurationItemId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        PatchBaselineId = patchBaselineId is null || patchBaselineId == Guid.Empty ? null : patchBaselineId;
        ConfigurationItemId = configurationItemId;
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
        Status = status;
        ScheduledAtUtc = scheduledAtUtc;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }
}

public sealed class ScheduledJob
{
    private ScheduledJob() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Provider { get; private set; }
    public string? ExternalJobId { get; private set; }
    public Guid? ConfigurationItemId { get; private set; }
    public string? ScheduleDescription { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastRunAtUtc { get; private set; }
    public JobLastResult LastResult { get; private set; }
    public DateTimeOffset? NextRunAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ScheduledJob Create(string name, DateTimeOffset utcNow, string? provider = null, string? externalJobId = null, Guid? configurationItemId = null, string? scheduleDescription = null, DateTimeOffset? nextRunAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ScheduledJob
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Provider = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim(),
            ExternalJobId = string.IsNullOrWhiteSpace(externalJobId) ? null : externalJobId.Trim(),
            ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId,
            ScheduleDescription = string.IsNullOrWhiteSpace(scheduleDescription) ? null : scheduleDescription.Trim(),
            IsActive = true,
            LastResult = JobLastResult.Unknown,
            NextRunAtUtc = nextRunAtUtc,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string name, string? provider, string? externalJobId, Guid? configurationItemId, string? scheduleDescription, bool isActive, DateTimeOffset? lastRunAtUtc, JobLastResult lastResult, DateTimeOffset? nextRunAtUtc, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(lastResult)) throw new ArgumentOutOfRangeException(nameof(lastResult));
        Name = name.Trim();
        Provider = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();
        ExternalJobId = string.IsNullOrWhiteSpace(externalJobId) ? null : externalJobId.Trim();
        ConfigurationItemId = configurationItemId is null || configurationItemId == Guid.Empty ? null : configurationItemId;
        ScheduleDescription = string.IsNullOrWhiteSpace(scheduleDescription) ? null : scheduleDescription.Trim();
        IsActive = isActive;
        LastRunAtUtc = lastRunAtUtc;
        LastResult = lastResult;
        NextRunAtUtc = nextRunAtUtc;
        UpdatedAtUtc = utcNow;
    }
}
