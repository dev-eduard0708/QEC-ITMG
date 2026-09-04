namespace Qec.Itmg.Governance.Domain;

public sealed class OrganizationProfile
{
    private OrganizationProfile() { }

    public Guid Id { get; private set; }
    public string LegalName { get; private set; } = null!;
    public string Timezone { get; private set; } = null!;
    public string? ClassificationScheme { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static OrganizationProfile Create(
        string legalName,
        string timezone,
        DateTimeOffset utcNow,
        string? classificationScheme = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone);
        return new OrganizationProfile
        {
            Id = Guid.CreateVersion7(),
            LegalName = legalName.Trim(),
            Timezone = timezone.Trim(),
            ClassificationScheme = TrimOrNull(classificationScheme),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string legalName, string timezone, string? classificationScheme, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone);
        LegalName = legalName.Trim();
        Timezone = timezone.Trim();
        ClassificationScheme = TrimOrNull(classificationScheme);
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OrganizationalUnit
{
    private OrganizationalUnit() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public Guid? ParentId { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static OrganizationalUnit Create(
        string name,
        DateTimeOffset utcNow,
        string? code = null,
        Guid? parentId = null,
        Guid? managerUserId = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new OrganizationalUnit
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Code = TrimOrNull(code),
            ParentId = parentId == Guid.Empty ? null : parentId,
            ManagerUserId = managerUserId == Guid.Empty ? null : managerUserId,
            Description = TrimOrNull(description),
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string name,
        string? code,
        Guid? parentId,
        Guid? managerUserId,
        string? description,
        bool isActive,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (parentId == Id) throw new InvalidOperationException("Organizational unit cannot be its own parent.");
        Name = name.Trim();
        Code = TrimOrNull(code);
        ParentId = parentId == Guid.Empty ? null : parentId;
        ManagerUserId = managerUserId == Guid.Empty ? null : managerUserId;
        Description = TrimOrNull(description);
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OrganizationalUnitMembership
{
    private OrganizationalUnitMembership() { }

    public Guid Id { get; private set; }
    public Guid OrganizationalUnitId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static OrganizationalUnitMembership Create(
        Guid organizationalUnitId,
        Guid userId,
        DateTimeOffset utcNow)
    {
        if (organizationalUnitId == Guid.Empty) throw new ArgumentException("Unit is required.", nameof(organizationalUnitId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        return new OrganizationalUnitMembership
        {
            Id = Guid.CreateVersion7(),
            OrganizationalUnitId = organizationalUnitId,
            UserId = userId,
            CreatedAtUtc = utcNow,
        };
    }
}
