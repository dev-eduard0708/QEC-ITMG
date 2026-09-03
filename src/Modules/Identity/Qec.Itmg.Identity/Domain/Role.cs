namespace Qec.Itmg.Identity.Domain;

public sealed class Role
{
    private Role()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsSystem { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    public static Role Create(
        string name,
        DateTimeOffset utcNow,
        string? description = null,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsSystem = isSystem,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateDescription(string? description, DateTimeOffset utcNow)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = utcNow;
    }
}
