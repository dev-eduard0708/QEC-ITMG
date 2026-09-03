namespace Qec.Itmg.Cmdb.Domain;

public sealed class AssetAssignment
{
    private AssetAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid AssetId { get; private set; }

    public Guid AssignedToUserId { get; private set; }

    public Guid AssignedByUserId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public DateTimeOffset? ReturnedAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive => ReturnedAtUtc is null;

    public static AssetAssignment Create(
        Guid assetId,
        Guid assignedToUserId,
        Guid assignedByUserId,
        DateTimeOffset utcNow,
        string? notes = null)
    {
        if (assetId == Guid.Empty)
        {
            throw new ArgumentException("Asset is required.", nameof(assetId));
        }

        if (assignedToUserId == Guid.Empty)
        {
            throw new ArgumentException("Assigned-to user is required.", nameof(assignedToUserId));
        }

        if (assignedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Assigned-by user is required.", nameof(assignedByUserId));
        }

        return new AssetAssignment
        {
            Id = Guid.CreateVersion7(),
            AssetId = assetId,
            AssignedToUserId = assignedToUserId,
            AssignedByUserId = assignedByUserId,
            AssignedAtUtc = utcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }

    public void Return(DateTimeOffset utcNow, string? notes = null)
    {
        if (ReturnedAtUtc is not null)
        {
            throw new InvalidOperationException("Assignment is already returned.");
        }

        ReturnedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes.Trim() : $"{Notes} | {notes.Trim()}";
        }
    }
}
