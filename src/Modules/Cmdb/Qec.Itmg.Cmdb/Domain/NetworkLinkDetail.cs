namespace Qec.Itmg.Cmdb.Domain;

public sealed class NetworkLinkDetail
{
    private NetworkLinkDetail()
    {
    }

    public Guid Id { get; private set; }

    public Guid RelationshipId { get; private set; }

    public string? FromPort { get; private set; }

    public string? ToPort { get; private set; }

    public string? MediaType { get; private set; }

    public string? LinkMode { get; private set; }

    public string? Notes { get; private set; }

    public bool IsConfirmed { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static NetworkLinkDetail Create(
        Guid relationshipId,
        DateTimeOffset utcNow,
        string? fromPort = null,
        string? toPort = null,
        string? mediaType = null,
        string? linkMode = null,
        string? notes = null,
        bool isConfirmed = true)
    {
        if (relationshipId == Guid.Empty)
        {
            throw new ArgumentException("Relationship is required.", nameof(relationshipId));
        }

        return new NetworkLinkDetail
        {
            Id = Guid.CreateVersion7(),
            RelationshipId = relationshipId,
            FromPort = NormalizeOptional(fromPort),
            ToPort = NormalizeOptional(toPort),
            MediaType = NormalizeOptional(mediaType),
            LinkMode = NormalizeOptional(linkMode),
            Notes = NormalizeOptional(notes),
            IsConfirmed = isConfirmed,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string? fromPort,
        string? toPort,
        string? mediaType,
        string? linkMode,
        string? notes,
        bool isConfirmed,
        DateTimeOffset utcNow)
    {
        FromPort = NormalizeOptional(fromPort);
        ToPort = NormalizeOptional(toPort);
        MediaType = NormalizeOptional(mediaType);
        LinkMode = NormalizeOptional(linkMode);
        Notes = NormalizeOptional(notes);
        IsConfirmed = isConfirmed;
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
