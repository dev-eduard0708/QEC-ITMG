namespace Qec.Itmg.Organization.Domain;

public sealed class Position
{
    private Position()
    {
    }

    public Guid Id { get; private set; }

    public Guid DepartmentId { get; private set; }

    public Guid? ParentPositionId { get; private set; }

    public string Key { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string? DescriptionEn { get; private set; }

    public string? DescriptionAr { get; private set; }

    public bool IsManagerial { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Position Create(
        Guid departmentId,
        string key,
        string nameEn,
        string nameAr,
        DateTimeOffset utcNow,
        Guid? parentPositionId = null,
        string? descriptionEn = null,
        string? descriptionAr = null,
        bool isManagerial = false,
        int sortOrder = 0,
        bool isActive = true)
    {
        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("Department is required.", nameof(departmentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);

        if (parentPositionId == Guid.Empty)
        {
            parentPositionId = null;
        }

        return new Position
        {
            Id = Guid.CreateVersion7(),
            DepartmentId = departmentId,
            ParentPositionId = parentPositionId,
            Key = NormalizeKey(key),
            NameEn = nameEn.Trim(),
            NameAr = nameAr.Trim(),
            DescriptionEn = NormalizeOptional(descriptionEn),
            DescriptionAr = NormalizeOptional(descriptionAr),
            IsManagerial = isManagerial,
            IsActive = isActive,
            SortOrder = sortOrder,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string nameEn,
        string nameAr,
        string? descriptionEn,
        string? descriptionAr,
        bool isManagerial,
        int sortOrder,
        bool isActive,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);

        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        DescriptionEn = NormalizeOptional(descriptionEn);
        DescriptionAr = NormalizeOptional(descriptionAr);
        IsManagerial = isManagerial;
        IsActive = isActive;
        SortOrder = sortOrder;
        UpdatedAtUtc = utcNow;
    }

    public void SetParent(Guid? parentPositionId, DateTimeOffset utcNow)
    {
        if (parentPositionId == Guid.Empty)
        {
            parentPositionId = null;
        }

        if (parentPositionId == Id)
        {
            throw new InvalidOperationException("A position cannot be its own parent.");
        }

        ParentPositionId = parentPositionId;
        UpdatedAtUtc = utcNow;
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAtUtc = utcNow;
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAtUtc = utcNow;
    }

    public static string NormalizeKey(string key) => key.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
