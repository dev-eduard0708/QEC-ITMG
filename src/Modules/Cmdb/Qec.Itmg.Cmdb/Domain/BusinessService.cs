namespace Qec.Itmg.Cmdb.Domain;

public sealed class BusinessService
{
    private BusinessService()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid? OwnerUserId { get; private set; }

    public CiCriticality Criticality { get; private set; }

    public int? RtoMinutes { get; private set; }

    public int? RpoMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static BusinessService Create(
        string name,
        CiCriticality criticality,
        DateTimeOffset utcNow,
        string? description = null,
        Guid? ownerUserId = null,
        int? rtoMinutes = null,
        int? rpoMinutes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(criticality))
        {
            throw new ArgumentOutOfRangeException(nameof(criticality));
        }

        return new BusinessService
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            OwnerUserId = ownerUserId is null || ownerUserId == Guid.Empty ? null : ownerUserId,
            Criticality = criticality,
            RtoMinutes = rtoMinutes,
            RpoMinutes = rpoMinutes,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }
}
