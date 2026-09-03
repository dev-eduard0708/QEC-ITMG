namespace Qec.Itmg.Platform.Attachments;

/// <summary>
/// Minimal file storage abstraction for Platform attachments.
/// Stores files by generated storage key (no absolute physical paths).
/// </summary>
public interface IFileStorage
{
    Task<StoredFileInfo> StoreAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}

