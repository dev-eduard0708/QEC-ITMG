using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Comments;

/// <summary>
/// Timeline-ready comment projection for future Ticket/Change/etc modules.
/// </summary>
public sealed record CommentTimelineItem(
    Guid Id,
    string ResourceType,
    Guid ResourceId,
    Guid AuthorUserId,
    string Body,
    string Visibility,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? EditedAtUtc)
{
    public static CommentTimelineItem From(Comment comment) =>
        new(
            comment.Id,
            comment.ResourceType,
            comment.ResourceId,
            comment.AuthorUserId,
            comment.Body,
            comment.Visibility.ToString(),
            comment.CreatedAtUtc,
            comment.EditedAtUtc);
}
