namespace Qec.Itmg.Organization.Domain;

public sealed class Department
{
    private Department()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>English display name (also used by legacy lookups).</summary>
    public string Name { get; private set; } = null!;

    public string? NameAr { get; private set; }

    public string Code { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? DescriptionAr { get; private set; }

    public Guid? ParentDepartmentId { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Department Create(
        string name,
        DateTimeOffset utcNow,
        string? description = null,
        string? code = null,
        string? nameAr = null,
        string? descriptionAr = null,
        Guid? parentDepartmentId = null,
        int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmedName = name.Trim();
        string resolvedCode = string.IsNullOrWhiteSpace(code)
            ? NormalizeCode(trimmedName)
            : NormalizeCode(code);

        if (parentDepartmentId == Guid.Empty)
        {
            parentDepartmentId = null;
        }

        return new Department
        {
            Id = Guid.CreateVersion7(),
            Name = trimmedName,
            NameAr = NormalizeOptional(nameAr),
            Code = resolvedCode,
            Description = NormalizeOptional(description),
            DescriptionAr = NormalizeOptional(descriptionAr),
            ParentDepartmentId = parentDepartmentId,
            IsActive = true,
            SortOrder = sortOrder,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Rename(string name, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void UpdateDescription(string? description, DateTimeOffset utcNow)
    {
        Description = NormalizeOptional(description);
        UpdatedAtUtc = utcNow;
    }

    public void UpdateDetails(
        string nameEn,
        string? nameAr,
        string code,
        string? descriptionEn,
        string? descriptionAr,
        Guid? parentDepartmentId,
        int sortOrder,
        bool isActive,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (parentDepartmentId == Guid.Empty)
        {
            parentDepartmentId = null;
        }

        if (parentDepartmentId == Id)
        {
            throw new InvalidOperationException("A department cannot be its own parent.");
        }

        Name = nameEn.Trim();
        NameAr = NormalizeOptional(nameAr);
        Code = NormalizeCode(code);
        Description = NormalizeOptional(descriptionEn);
        DescriptionAr = NormalizeOptional(descriptionAr);
        ParentDepartmentId = parentDepartmentId;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    public void SetParent(Guid? parentDepartmentId, DateTimeOffset utcNow)
    {
        if (parentDepartmentId == Guid.Empty)
        {
            parentDepartmentId = null;
        }

        if (parentDepartmentId == Id)
        {
            throw new InvalidOperationException("A department cannot be its own parent.");
        }

        ParentDepartmentId = parentDepartmentId;
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

    public static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        char[] chars = code.Trim().ToUpperInvariant().Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        string normalized = new string(chars);
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized.Trim('_');
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
