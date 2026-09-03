using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Platform.Attachments;

public interface IAttachmentStorageService
{
    Task<AttachmentMetadata> StoreAsync(
        Stream content,
        string originalFileName,
        string contentType,
        Guid uploadedByUserId,
        CancellationToken cancellationToken = default);

    Task<AttachmentMetadata?> GetMetadataAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}

