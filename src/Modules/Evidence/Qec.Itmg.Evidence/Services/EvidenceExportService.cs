using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Evidence.Domain;

namespace Qec.Itmg.Evidence.Services;

public sealed record EvidenceExportResult(byte[] ZipBytes, string FileName, int EvidenceCount);

/// <summary>Privileged ZIP export of selected evidence metadata + attachments. Requires evidence.export.</summary>
public sealed class EvidenceExportService(
    EvidenceService evidence,
    IClock clock,
    IBusinessAuditWriter businessAudit)
{
    public async Task<EvidenceExportResult> ExportAsync(
        IReadOnlyList<Guid> evidenceIds,
        Guid actorUserId,
        Func<Guid, Task<(Stream Stream, string FileName, string ContentType)?>> openAttachment,
        bool includeConfidential,
        CancellationToken ct)
    {
        if (evidenceIds.Count == 0) throw new ArgumentException("At least one evidence id is required.");

        List<EvidenceDto> items = [];
        foreach (Guid id in evidenceIds.Distinct())
        {
            EvidenceDto? item = await evidence.GetAsync(id, includeConfidential, ct);
            if (item is null) continue;
            if (!includeConfidential && item.Classification != EvidenceClassification.Internal.ToString())
                continue;
            items.Add(item);
        }

        if (items.Count == 0) throw new InvalidOperationException("No authorized evidence found to export.");

        using MemoryStream zipStream = new();
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = items.Select(x => new
            {
                x.EvidenceNumber,
                x.Title,
                x.Description,
                x.SourceType,
                x.SourceRecordId,
                x.EvidenceType,
                x.Classification,
                x.ValidFrom,
                x.ValidTo,
                x.Status,
                x.OwnerUserId,
                x.CurrentAttachmentId,
                ExportedAtUtc = clock.UtcNow,
            });
            byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json");
            await using (Stream entryStream = manifestEntry.Open())
                await entryStream.WriteAsync(manifestBytes, ct);

            foreach (EvidenceDto item in items)
            {
                if (item.CurrentAttachmentId is not Guid aid) continue;
                (Stream Stream, string FileName, string ContentType)? file = await openAttachment(aid);
                if (file is null) continue;
                await using Stream content = file.Value.Stream;
                string safeName = $"{item.EvidenceNumber}_{Sanitize(file.Value.FileName)}";
                ZipArchiveEntry entry = archive.CreateEntry($"files/{safeName}");
                await using Stream entryStream = entry.Open();
                await content.CopyToAsync(entryStream, ct);
            }
        }

        string scope = string.Join(",", items.Select(x => x.EvidenceNumber));
        string classifications = string.Join(",", items.Select(x => x.Classification).Distinct());
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Evidence,
            AggregateId = items[0].Id,
            BusinessNumber = scope.Length > 64 ? scope[..64] : scope,
            Action = BusinessAuditAction.Updated,
            FieldName = "Export",
            NewValue = $"count={items.Count};classification={classifications};actor={actorUserId}",
            Source = AuditSource.Api,
        }, ct);

        string fileName = $"evidence-export-{clock.UtcNow:yyyyMMddHHmmss}.zip";
        return new(zipStream.ToArray(), fileName, items.Count);
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
