namespace Qec.Itmg.AccessManagement.Domain;

public enum AccessCategoryStage
{
    Requester = 0,
    Approver = 1,
    Fulfiller = 2,
    Verifier = 3,
    Closer = 4,
}

public enum AccessVerificationMethod
{
    Employee = 0,
    Fallback = 1,
}

public enum AccessVerificationOutcome
{
    Verified = 0,
    Problem = 1,
}

public sealed class AccessCategory
{
    private AccessCategory() { }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string NameAr { get; private set; } = null!;
    public string? DescriptionEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public bool IsActive { get; private set; }
    public bool PreferSubjectEmployeeVerification { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static AccessCategory Create(
        string key,
        string nameEn,
        string nameAr,
        DateTimeOffset utcNow,
        string? descriptionEn = null,
        string? descriptionAr = null,
        bool preferSubjectEmployeeVerification = true,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        return new AccessCategory
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim().ToUpperInvariant(),
            NameEn = nameEn.Trim(),
            NameAr = nameAr.Trim(),
            DescriptionEn = Norm(descriptionEn),
            DescriptionAr = Norm(descriptionAr),
            IsActive = isActive,
            PreferSubjectEmployeeVerification = preferSubjectEmployeeVerification,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        bool isActive,
        bool preferSubjectEmployeeVerification,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        DescriptionEn = Norm(descriptionEn);
        DescriptionAr = Norm(descriptionAr);
        IsActive = isActive;
        PreferSubjectEmployeeVerification = preferSubjectEmployeeVerification;
        UpdatedAtUtc = utcNow;
    }

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class AccessCategoryParticipant
{
    private AccessCategoryParticipant() { }

    public Guid Id { get; private set; }
    public Guid AccessCategoryId { get; private set; }
    public AccessCategoryStage Stage { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AccessCategoryParticipant Create(
        Guid accessCategoryId,
        AccessCategoryStage stage,
        Guid userId,
        DateTimeOffset utcNow)
    {
        if (accessCategoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(accessCategoryId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        return new AccessCategoryParticipant
        {
            Id = Guid.CreateVersion7(),
            AccessCategoryId = accessCategoryId,
            Stage = stage,
            UserId = userId,
            CreatedAtUtc = utcNow,
        };
    }
}

public sealed class AccessCaseRouteParticipant
{
    private AccessCaseRouteParticipant() { }

    public Guid Id { get; private set; }
    public Guid AccessCaseId { get; private set; }
    public AccessCategoryStage Stage { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsSubjectEmployeeDerived { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AccessCaseRouteParticipant Create(
        Guid accessCaseId,
        AccessCategoryStage stage,
        Guid userId,
        DateTimeOffset utcNow,
        bool isSubjectEmployeeDerived = false)
    {
        if (accessCaseId == Guid.Empty) throw new ArgumentException("Case is required.", nameof(accessCaseId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        return new AccessCaseRouteParticipant
        {
            Id = Guid.CreateVersion7(),
            AccessCaseId = accessCaseId,
            Stage = stage,
            UserId = userId,
            IsSubjectEmployeeDerived = isSubjectEmployeeDerived,
            CreatedAtUtc = utcNow,
        };
    }
}
