namespace Qec.Itmg.Identity.Domain;

public sealed class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Upn { get; private set; } = null!;

    public string? DirectoryObjectId { get; private set; }

    public string DisplayName { get; private set; } = null!;

    public UserStatus Status { get; private set; }

    public UserType UserType { get; private set; }

    public string? TimeZone { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    public static User Create(
        string upn,
        string displayName,
        UserType userType,
        DateTimeOffset utcNow,
        string? directoryObjectId = null,
        string? timeZone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new User
        {
            Id = Guid.CreateVersion7(),
            Upn = upn.Trim(),
            DirectoryObjectId = NormalizeOptional(directoryObjectId),
            DisplayName = displayName.Trim(),
            Status = UserStatus.Active,
            UserType = userType,
            TimeZone = NormalizeOptional(timeZone),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Disable(DateTimeOffset utcNow)
    {
        if (Status == UserStatus.Disabled)
        {
            return;
        }

        Status = UserStatus.Disabled;
        UpdatedAtUtc = utcNow;
    }

    public void Enable(DateTimeOffset utcNow)
    {
        if (Status == UserStatus.Active)
        {
            return;
        }

        Status = UserStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Rename(string displayName, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
