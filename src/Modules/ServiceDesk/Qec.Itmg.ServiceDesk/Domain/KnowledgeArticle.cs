namespace Qec.Itmg.ServiceDesk.Domain;

public enum KnowledgeArticleStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public sealed class KnowledgeArticle
{
    private KnowledgeArticle()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string? Summary { get; private set; }

    public string Body { get; private set; } = null!;

    public KnowledgeArticleStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public static KnowledgeArticle Create(
        string title,
        string slug,
        string body,
        Guid createdByUserId,
        DateTimeOffset utcNow,
        string? summary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created-by user is required.", nameof(createdByUserId));
        }

        return new KnowledgeArticle
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Slug = NormalizeSlug(slug),
            Summary = NormalizeOptional(summary),
            Body = body.Trim(),
            Status = KnowledgeArticleStatus.Draft,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(
        string title,
        string slug,
        string body,
        Guid updatedByUserId,
        DateTimeOffset utcNow,
        string? summary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updated-by user is required.", nameof(updatedByUserId));
        }

        if (Status == KnowledgeArticleStatus.Archived)
        {
            throw new InvalidOperationException("Archived articles cannot be edited.");
        }

        Title = title.Trim();
        Slug = NormalizeSlug(slug);
        Summary = NormalizeOptional(summary);
        Body = body.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = utcNow;
    }

    public void Publish(Guid updatedByUserId, DateTimeOffset utcNow)
    {
        if (Status == KnowledgeArticleStatus.Archived)
        {
            throw new InvalidOperationException("Archived articles cannot be published.");
        }

        Status = KnowledgeArticleStatus.Published;
        PublishedAtUtc ??= utcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = utcNow;
    }

    public void Archive(Guid updatedByUserId, DateTimeOffset utcNow)
    {
        Status = KnowledgeArticleStatus.Archived;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = utcNow;
    }

    internal static string NormalizeSlug(string slug)
    {
        string trimmed = slug.Trim().ToLowerInvariant();
        if (trimmed.Length is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(slug), "Slug length must be 1..128.");
        }

        foreach (char c in trimmed)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            {
                continue;
            }

            throw new ArgumentException("Slug may only contain letters, digits, '-' and '_'.", nameof(slug));
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
