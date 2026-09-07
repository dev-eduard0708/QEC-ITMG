namespace Qec.Itmg.Organization.Domain;

public sealed class PositionAssignment
{
    private PositionAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid PositionId { get; private set; }

    public Guid UserId { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PositionAssignment Create(
        Guid positionId,
        Guid userId,
        DateTimeOffset utcNow,
        bool isPrimary = false,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null)
    {
        if (positionId == Guid.Empty)
        {
            throw new ArgumentException("Position is required.", nameof(positionId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(userId));
        }

        return new PositionAssignment
        {
            Id = Guid.CreateVersion7(),
            PositionId = positionId,
            UserId = userId,
            IsPrimary = isPrimary,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void SetPrimary(bool isPrimary, DateTimeOffset utcNow)
    {
        IsPrimary = isPrimary;
        UpdatedAtUtc = utcNow;
    }
}
