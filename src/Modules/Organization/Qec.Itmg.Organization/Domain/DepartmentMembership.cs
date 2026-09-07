namespace Qec.Itmg.Organization.Domain;

public sealed class DepartmentMembership
{
    private DepartmentMembership()
    {
    }

    public Guid Id { get; private set; }

    public Guid DepartmentId { get; private set; }

    public Guid UserId { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsActive => EffectiveTo is null;

    public static DepartmentMembership Create(
        Guid departmentId,
        Guid userId,
        DateTimeOffset utcNow,
        bool isPrimary = false,
        DateTimeOffset? effectiveFrom = null)
    {
        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("Department is required.", nameof(departmentId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(userId));
        }

        return new DepartmentMembership
        {
            Id = Guid.CreateVersion7(),
            DepartmentId = departmentId,
            UserId = userId,
            IsPrimary = isPrimary,
            EffectiveFrom = effectiveFrom ?? utcNow,
            EffectiveTo = null,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void SetPrimary(bool isPrimary, DateTimeOffset utcNow)
    {
        IsPrimary = isPrimary;
        UpdatedAtUtc = utcNow;
    }

    public void EndMembership(DateTimeOffset utcNow)
    {
        if (EffectiveTo is not null)
        {
            return;
        }

        EffectiveTo = utcNow;
        IsPrimary = false;
        UpdatedAtUtc = utcNow;
    }
}
