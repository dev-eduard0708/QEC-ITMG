using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Attachments;

public sealed class AttachmentStorageService(
    PlatformDbContext db,
    IClock clock,
    IFileStorage fileStorage) : IAttachmentStorageService
{
    public async Task<AttachmentMetadata> StoreAsync(
        Stream content,
        string originalFileName,
        string contentType,
        Guid uploadedByUserId,
        string? resourceType = null,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("uploadedByUserId must not be empty.", nameof(uploadedByUserId));
        }

        // Generated storage key only: never trust original filename as a disk path.
        string storageKey = $"att-{Guid.NewGuid():N}";

        StoredFileInfo stored = await fileStorage.StoreAsync(
            storageKey,
            content,
            cancellationToken);

        AttachmentMetadata metadata = AttachmentMetadata.CreateUploaded(
            uploadedByUserId: uploadedByUserId,
            originalFileName: originalFileName,
            storageKey: storageKey,
            contentType: contentType,
            sizeBytes: stored.SizeBytes,
            sha256: stored.Sha256,
            uploadedAtUtc: clock.UtcNow,
            resourceType: resourceType,
            resourceId: resourceId);

        db.Attachments.Add(metadata);
        await db.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    public async Task<AttachmentMetadata?> GetMetadataAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        return await db.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == attachmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<AttachmentMetadata>> ListByResourceAsync(
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));
        }

        string normalized = resourceType.Trim();
        return await db.Attachments
            .AsNoTracking()
            .Where(item => item.ResourceType == normalized && item.ResourceId == resourceId)
            .OrderByDescending(item => item.UploadedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        AttachmentMetadata? metadata = await db.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == attachmentId, cancellationToken);

        if (metadata is null)
        {
            throw new FileNotFoundException("Attachment metadata missing.", attachmentId.ToString());
        }

        return await fileStorage.OpenReadAsync(metadata.StorageKey, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        AttachmentMetadata? metadata = await db.Attachments
            .SingleOrDefaultAsync(candidate => candidate.Id == attachmentId, cancellationToken);

        if (metadata is null)
        {
            return;
        }

        db.Attachments.Remove(metadata);
        await db.SaveChangesAsync(cancellationToken);

        await fileStorage.DeleteAsync(metadata.StorageKey, cancellationToken);
    }
}

