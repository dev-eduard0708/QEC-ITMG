namespace Qec.Itmg.Cmdb.Domain;

public enum CiRelationshipType
{
    DependsOn = 0,
    HostedOn = 1,
    ConnectsTo = 2,
    Supports = 3,
    Contains = 4,
    /// <summary>Optional hint of redundancy/failover peer. Does not auto-declare SPOF.</summary>
    RedundantWith = 5,
}

public sealed class CiRelationship
{
    private CiRelationship()
    {
    }

    public Guid Id { get; private set; }

    public Guid SourceCiId { get; private set; }

    public Guid TargetCiId { get; private set; }

    public CiRelationshipType RelationshipType { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public static CiRelationship Create(
        Guid sourceCiId,
        Guid targetCiId,
        CiRelationshipType relationshipType,
        DateTimeOffset utcNow,
        string? notes = null)
    {
        if (sourceCiId == Guid.Empty)
        {
            throw new ArgumentException("Source CI is required.", nameof(sourceCiId));
        }

        if (targetCiId == Guid.Empty)
        {
            throw new ArgumentException("Target CI is required.", nameof(targetCiId));
        }

        if (sourceCiId == targetCiId)
        {
            throw new InvalidOperationException("A configuration item cannot link to itself.");
        }

        if (!Enum.IsDefined(relationshipType))
        {
            throw new ArgumentOutOfRangeException(nameof(relationshipType));
        }

        return new CiRelationship
        {
            Id = Guid.CreateVersion7(),
            SourceCiId = sourceCiId,
            TargetCiId = targetCiId,
            RelationshipType = relationshipType,
            CreatedAtUtc = utcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }
}
