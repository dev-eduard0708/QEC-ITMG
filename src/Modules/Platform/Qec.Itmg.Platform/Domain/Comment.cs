namespace Qec.Itmg.Platform.Domain;

/// <summary>
/// Shared comment foundation. Resource-level authorization stays with calling modules.
/// </summary>
public sealed class Comment
{
    public Guid Id { get; private set; }

    public string ResourceType { get; private set; } = string.Empty;

    public Guid ResourceId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public CommentVisibility Visibility { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    private Comment()
    {
    }

    public static Comment Create(
        string resourceType,
        Guid resourceId,
        Guid authorUserId,
        string body,
        CommentVisibility visibility,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("AuthorUserId must not be empty.", nameof(authorUserId));
        }

        if (!Enum.IsDefined(visibility))
        {
            throw new ArgumentOutOfRangeException(nameof(visibility));
        }

        return new Comment
        {
            Id = Guid.CreateVersion7(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId,
            AuthorUserId = authorUserId,
            Body = body.Trim(),
            Visibility = visibility,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void EditBody(string body, DateTimeOffset editedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        Body = body.Trim();
        EditedAtUtc = editedAtUtc;
    }
}
