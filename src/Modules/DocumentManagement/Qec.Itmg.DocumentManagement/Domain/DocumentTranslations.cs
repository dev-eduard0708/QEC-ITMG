namespace Qec.Itmg.DocumentManagement.Domain;

public static class DocumentLanguageCodes
{
    public const string English = "en";
    public const string Arabic = "ar";

    public static string Normalize(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return English;
        string code = languageCode.Trim().ToLowerInvariant();
        if (code.StartsWith("ar", StringComparison.Ordinal)) return Arabic;
        return English;
    }

    public static bool IsSupported(string? languageCode)
    {
        string n = Normalize(languageCode);
        return n is English or Arabic;
    }
}

public sealed class ManagedDocumentTranslation
{
    private ManagedDocumentTranslation() { }

    public Guid Id { get; private set; }
    public Guid ManagedDocumentId { get; private set; }
    public string LanguageCode { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ManagedDocumentTranslation Create(
        Guid managedDocumentId,
        string languageCode,
        string title,
        DateTimeOffset utcNow)
    {
        if (managedDocumentId == Guid.Empty) throw new ArgumentException("Document is required.", nameof(managedDocumentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new ManagedDocumentTranslation
        {
            Id = Guid.CreateVersion7(),
            ManagedDocumentId = managedDocumentId,
            LanguageCode = DocumentLanguageCodes.Normalize(languageCode),
            Title = title.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateTitle(string title, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        UpdatedAtUtc = utcNow;
    }
}

public sealed class DocumentVersionTranslation
{
    private DocumentVersionTranslation() { }

    public Guid Id { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public string LanguageCode { get; private set; } = null!;
    public string ContentText { get; private set; } = null!;
    public string? ChangeSummary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static DocumentVersionTranslation Create(
        Guid documentVersionId,
        string languageCode,
        string contentText,
        DateTimeOffset utcNow,
        string? changeSummary = null)
    {
        if (documentVersionId == Guid.Empty) throw new ArgumentException("Version is required.", nameof(documentVersionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentText);
        return new DocumentVersionTranslation
        {
            Id = Guid.CreateVersion7(),
            DocumentVersionId = documentVersionId,
            LanguageCode = DocumentLanguageCodes.Normalize(languageCode),
            ContentText = contentText.Trim(),
            ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? null : changeSummary.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string contentText, string? changeSummary, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentText);
        ContentText = contentText.Trim();
        ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? null : changeSummary.Trim();
        UpdatedAtUtc = utcNow;
    }
}
