using Microsoft.EntityFrameworkCore;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Persistence;

namespace Qec.Itmg.DocumentManagement.Services;

internal static class DocumentLocalization
{
    public static bool HasCompleteContent(string? title, string? contentText) =>
        !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(contentText);

    public static async Task UpsertDocumentTitleAsync(
        DocumentManagementDbContext db,
        Guid managedDocumentId,
        string languageCode,
        string title,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        string lang = DocumentLanguageCodes.Normalize(languageCode);
        ManagedDocumentTranslation? existing = await db.ManagedDocumentTranslations
            .FirstOrDefaultAsync(x => x.ManagedDocumentId == managedDocumentId && x.LanguageCode == lang, ct);
        if (existing is null)
            db.ManagedDocumentTranslations.Add(ManagedDocumentTranslation.Create(managedDocumentId, lang, title, utcNow));
        else
            existing.UpdateTitle(title, utcNow);
    }

    public static async Task UpsertVersionContentAsync(
        DocumentManagementDbContext db,
        DocumentVersion version,
        string languageCode,
        string contentText,
        string? changeSummary,
        DateTimeOffset utcNow,
        CancellationToken ct,
        bool allowOverwriteExisting = true)
    {
        if (version.IsImmutable)
            throw new InvalidOperationException("Published or approved versions are immutable.");

        string lang = DocumentLanguageCodes.Normalize(languageCode);
        DocumentVersionTranslation? existing = await db.DocumentVersionTranslations
            .FirstOrDefaultAsync(x => x.DocumentVersionId == version.Id && x.LanguageCode == lang, ct);
        if (existing is null)
        {
            db.DocumentVersionTranslations.Add(
                DocumentVersionTranslation.Create(version.Id, lang, contentText, utcNow, changeSummary));
            return;
        }

        if (!allowOverwriteExisting) return;
        existing.Update(contentText, changeSummary, utcNow);
    }

    public static async Task EnsureEnglishFromLegacyAsync(
        DocumentManagementDbContext db,
        ManagedDocument doc,
        DocumentVersion? version,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(doc.Title))
        {
            bool hasEnTitle = await db.ManagedDocumentTranslations.AnyAsync(
                x => x.ManagedDocumentId == doc.Id && x.LanguageCode == DocumentLanguageCodes.English, ct);
            if (!hasEnTitle)
                db.ManagedDocumentTranslations.Add(
                    ManagedDocumentTranslation.Create(doc.Id, DocumentLanguageCodes.English, doc.Title, utcNow));
        }

        if (version is null || string.IsNullOrWhiteSpace(version.ContentText)) return;

        bool hasEnBody = await db.DocumentVersionTranslations.AnyAsync(
            x => x.DocumentVersionId == version.Id && x.LanguageCode == DocumentLanguageCodes.English, ct);
        if (!hasEnBody)
        {
            db.DocumentVersionTranslations.Add(DocumentVersionTranslation.Create(
                version.Id, DocumentLanguageCodes.English, version.ContentText, utcNow, version.ChangeSummary));
        }
    }

    public static async Task<(string? Title, string? Content, string? ChangeSummary, bool FallbackUsed)> ResolveEmployeeContentAsync(
        DocumentManagementDbContext db,
        ManagedDocument doc,
        DocumentVersion version,
        string requestedLanguage,
        CancellationToken ct)
    {
        string requested = DocumentLanguageCodes.Normalize(requestedLanguage);
        Dictionary<string, ManagedDocumentTranslation> titles = await db.ManagedDocumentTranslations.AsNoTracking()
            .Where(x => x.ManagedDocumentId == doc.Id)
            .ToDictionaryAsync(x => x.LanguageCode, ct);
        Dictionary<string, DocumentVersionTranslation> bodies = await db.DocumentVersionTranslations.AsNoTracking()
            .Where(x => x.DocumentVersionId == version.Id)
            .ToDictionaryAsync(x => x.LanguageCode, ct);

        string? title = titles.TryGetValue(requested, out ManagedDocumentTranslation? t) ? t.Title : null;
        string? content = bodies.TryGetValue(requested, out DocumentVersionTranslation? b) ? b.ContentText : null;
        string? summary = bodies.TryGetValue(requested, out DocumentVersionTranslation? s) ? s.ChangeSummary : null;

        if (requested == DocumentLanguageCodes.English)
        {
            title ??= doc.Title;
            content ??= version.ContentText;
            summary ??= version.ChangeSummary;
            return (title, content, summary, false);
        }

        bool fallback = string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content);
        if (fallback)
        {
            title = titles.TryGetValue(DocumentLanguageCodes.English, out ManagedDocumentTranslation? et)
                ? et.Title
                : doc.Title;
            content = bodies.TryGetValue(DocumentLanguageCodes.English, out DocumentVersionTranslation? eb)
                ? eb.ContentText
                : version.ContentText;
            summary = bodies.TryGetValue(DocumentLanguageCodes.English, out DocumentVersionTranslation? es)
                ? es.ChangeSummary
                : version.ChangeSummary;
        }

        return (title, content, summary, fallback);
    }

    public static async Task<DocumentLocalizationBundle> LoadBundleAsync(
        DocumentManagementDbContext db,
        Guid managedDocumentId,
        Guid? versionId,
        ManagedDocument doc,
        DocumentVersion? version,
        CancellationToken ct)
    {
        Dictionary<string, ManagedDocumentTranslation> titles = await db.ManagedDocumentTranslations.AsNoTracking()
            .Where(x => x.ManagedDocumentId == managedDocumentId)
            .ToDictionaryAsync(x => x.LanguageCode, ct);
        Dictionary<string, DocumentVersionTranslation> bodies = versionId is Guid vid
            ? await db.DocumentVersionTranslations.AsNoTracking()
                .Where(x => x.DocumentVersionId == vid)
                .ToDictionaryAsync(x => x.LanguageCode, ct)
            : [];

        string? titleEn = titles.TryGetValue(DocumentLanguageCodes.English, out ManagedDocumentTranslation? te)
            ? te.Title
            : doc.Title;
        string? titleAr = titles.TryGetValue(DocumentLanguageCodes.Arabic, out ManagedDocumentTranslation? ta)
            ? ta.Title
            : null;
        string? contentEn = bodies.TryGetValue(DocumentLanguageCodes.English, out DocumentVersionTranslation? ce)
            ? ce.ContentText
            : version?.ContentText;
        string? contentAr = bodies.TryGetValue(DocumentLanguageCodes.Arabic, out DocumentVersionTranslation? ca)
            ? ca.ContentText
            : null;
        string? summaryEn = bodies.TryGetValue(DocumentLanguageCodes.English, out DocumentVersionTranslation? se)
            ? se.ChangeSummary
            : version?.ChangeSummary;
        string? summaryAr = bodies.TryGetValue(DocumentLanguageCodes.Arabic, out DocumentVersionTranslation? sa)
            ? sa.ChangeSummary
            : null;

        return new DocumentLocalizationBundle(
            titleEn, titleAr, contentEn, contentAr, summaryEn, summaryAr,
            HasCompleteContent(titleEn, contentEn),
            HasCompleteContent(titleAr, contentAr));
    }

    public static string DisplayTitle(DocumentLocalizationBundle bundle, string? languageCode)
    {
        string lang = DocumentLanguageCodes.Normalize(languageCode);
        if (lang == DocumentLanguageCodes.Arabic && !string.IsNullOrWhiteSpace(bundle.TitleAr))
            return bundle.TitleAr!;
        return bundle.TitleEn ?? string.Empty;
    }
}

internal sealed record DocumentLocalizationBundle(
    string? TitleEn,
    string? TitleAr,
    string? ContentEn,
    string? ContentAr,
    string? ChangeSummaryEn,
    string? ChangeSummaryAr,
    bool HasEnglishContent,
    bool HasArabicContent);
