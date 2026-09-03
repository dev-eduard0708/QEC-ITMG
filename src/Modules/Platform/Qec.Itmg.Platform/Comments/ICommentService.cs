using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Comments;

public interface ICommentService
{
    Task<CommentTimelineItem> AddAsync(
        string resourceType,
        Guid resourceId,
        Guid authorUserId,
        string body,
        CommentVisibility visibility,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommentTimelineItem>> ListAsync(
        string resourceType,
        Guid resourceId,
        CommentVisibility? visibilityFilter = null,
        CancellationToken cancellationToken = default);

    Task<CommentTimelineItem> EditAsync(
        Guid commentId,
        string body,
        CancellationToken cancellationToken = default);
}
