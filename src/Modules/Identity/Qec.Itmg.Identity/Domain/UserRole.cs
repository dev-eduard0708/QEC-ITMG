namespace Qec.Itmg.Identity.Domain;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    public Role Role { get; private set; } = null!;

    public static UserRole Create(Guid userId, Guid roleId, DateTimeOffset assignedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role id is required.", nameof(roleId));
        }

        return new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAtUtc = assignedAtUtc,
        };
    }
}
