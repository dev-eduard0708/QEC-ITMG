using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Comments;

public sealed class CommentService(
    PlatformDbContext db,
    IClock clock) : ICommentService
{
    public async Task<CommentTimelineItem> AddAsync(
        string resourceType,
        Guid resourceId,
        Guid authorUserId,
        string body,
        CommentVisibility visibility,
        CancellationToken cancellationToken = default)
    {
        Comment comment = Comment.Create(
            resourceType,
            resourceId,
            authorUserId,
            body,
            visibility,
            clock.UtcNow);

        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        return CommentTimelineItem.From(comment);
    }

    public async Task<IReadOnlyList<CommentTimelineItem>> ListAsync(
        string resourceType,
        Guid resourceId,
        CommentVisibility? visibilityFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));
        }

        string normalizedType = resourceType.Trim();

        IQueryable<Comment> query = db.Comments
            .AsNoTracking()
            .Where(comment => comment.ResourceType == normalizedType && comment.ResourceId == resourceId);

        if (visibilityFilter is not null)
        {
            query = query.Where(comment => comment.Visibility == visibilityFilter.Value);
        }

        List<Comment> comments = await query
            .OrderBy(comment => comment.CreatedAtUtc)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

        return comments.Select(CommentTimelineItem.From).ToList();
    }

    public async Task<CommentTimelineItem> EditAsync(
        Guid commentId,
        string body,
        CancellationToken cancellationToken = default)
    {
        Comment comment = await db.Comments.SingleAsync(
            candidate => candidate.Id == commentId,
            cancellationToken);

        comment.EditBody(body, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return CommentTimelineItem.From(comment);
    }
}
