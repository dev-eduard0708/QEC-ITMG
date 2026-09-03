using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;

namespace Qec.Itmg.ServiceDesk.Services;

public sealed record KnowledgeArticleDto(
    Guid Id,
    string Title,
    string Slug,
    string? Summary,
    string Body,
    string Status,
    Guid CreatedByUserId,
    Guid UpdatedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

public sealed class KnowledgeArticleService(ServiceDeskDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<KnowledgeArticleDto>> ListPublishedAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<KnowledgeArticle> query = db.KnowledgeArticles.AsNoTracking()
            .Where(item => item.Status == KnowledgeArticleStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Title.Contains(term)
                || (item.Summary != null && item.Summary.Contains(term))
                || item.Body.Contains(term));
        }

        List<KnowledgeArticle> items = await query
            .OrderByDescending(item => item.PublishedAtUtc)
            .ThenBy(item => item.Title)
            .Take(100)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<KnowledgeArticleDto?> GetPublishedBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        string normalized = KnowledgeArticle.NormalizeSlug(slug);
        KnowledgeArticle? article = await db.KnowledgeArticles.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Slug == normalized && item.Status == KnowledgeArticleStatus.Published,
                cancellationToken);
        return article is null ? null : Map(article);
    }

    public async Task<IReadOnlyList<KnowledgeArticleDto>> ListAdminAsync(
        string? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<KnowledgeArticle> query = db.KnowledgeArticles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status, ignoreCase: true, out KnowledgeArticleStatus parsed))
        {
            query = query.Where(item => item.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(item =>
                item.Title.Contains(term)
                || item.Slug.Contains(term)
                || (item.Summary != null && item.Summary.Contains(term)));
        }

        List<KnowledgeArticle> items = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<KnowledgeArticleDto?> GetAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        KnowledgeArticle? article = await db.KnowledgeArticles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return article is null ? null : Map(article);
    }

    public async Task<KnowledgeArticle> CreateAsync(
        string title,
        string slug,
        string body,
        Guid createdByUserId,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSlugAvailableAsync(slug, excludeId: null, cancellationToken);
        KnowledgeArticle article = KnowledgeArticle.Create(title, slug, body, createdByUserId, clock.UtcNow, summary);
        db.KnowledgeArticles.Add(article);
        await db.SaveChangesAsync(cancellationToken);
        return article;
    }

    public async Task<KnowledgeArticle> UpdateAsync(
        Guid id,
        string title,
        string slug,
        string body,
        Guid updatedByUserId,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        KnowledgeArticle article = await db.KnowledgeArticles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Knowledge article was not found.");

        await EnsureSlugAvailableAsync(slug, excludeId: id, cancellationToken);
        article.Update(title, slug, body, updatedByUserId, clock.UtcNow, summary);
        await db.SaveChangesAsync(cancellationToken);
        return article;
    }

    public async Task<KnowledgeArticle> PublishAsync(
        Guid id,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        KnowledgeArticle article = await db.KnowledgeArticles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Knowledge article was not found.");

        article.Publish(updatedByUserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return article;
    }

    public async Task<KnowledgeArticle> ArchiveAsync(
        Guid id,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        KnowledgeArticle article = await db.KnowledgeArticles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Knowledge article was not found.");

        article.Archive(updatedByUserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return article;
    }

    private async Task EnsureSlugAvailableAsync(
        string slug,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        string normalized = KnowledgeArticle.NormalizeSlug(slug);
        bool taken = await db.KnowledgeArticles.AsNoTracking()
            .AnyAsync(
                item => item.Slug == normalized && (excludeId == null || item.Id != excludeId),
                cancellationToken);
        if (taken)
        {
            throw new InvalidOperationException("A knowledge article with this slug already exists.");
        }
    }

    private static KnowledgeArticleDto Map(KnowledgeArticle article) =>
        new(
            article.Id,
            article.Title,
            article.Slug,
            article.Summary,
            article.Body,
            article.Status.ToString(),
            article.CreatedByUserId,
            article.UpdatedByUserId,
            article.CreatedAtUtc,
            article.UpdatedAtUtc,
            article.PublishedAtUtc);
}
