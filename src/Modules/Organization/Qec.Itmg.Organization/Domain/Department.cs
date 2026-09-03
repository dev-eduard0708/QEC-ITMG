namespace Qec.Itmg.Organization.Domain;

public sealed class Department
{
    private Department()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static Department Create(string name, DateTimeOffset utcNow, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Department
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = NormalizeOptional(description),
            IsActive = true,
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
