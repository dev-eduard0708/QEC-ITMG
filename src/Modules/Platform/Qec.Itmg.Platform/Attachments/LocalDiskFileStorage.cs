using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Qec.Itmg.Platform.Attachments;

/// <summary>
/// Local on-prem disk storage provider for development.
/// Prevents path traversal by validating storageKey and verifying full paths stay within RootPath.
/// </summary>
public sealed class LocalDiskFileStorage(IOptions<AttachmentStorageOptions> options) : IFileStorage
{
    private readonly string _rootPathFull = Path.GetFullPath(options.Value.RootPath);
    private readonly long _maxFileSizeBytes = options.Value.MaxFileSizeBytes;

    public async Task<StoredFileInfo> StoreAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey, nameof(storageKey));
        ArgumentNullException.ThrowIfNull(content);

        ValidateStorageKey(storageKey);

        Directory.CreateDirectory(_rootPathFull);

        string targetPath = GetValidatedTargetPath(storageKey);

        FileStream? file = null;

        try
        {
            file = new FileStream(
                path: targetPath,
                mode: FileMode.CreateNew,
                access: FileAccess.Write,
                share: FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous);

            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            byte[] buffer = new byte[81920];
            long total = 0;

            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                total += read;
                if (total <= 0 || total > _maxFileSizeBytes)
                {
                    throw new InvalidOperationException(
                        $"File size {total} exceeds MaxFileSizeBytes {_maxFileSizeBytes}.");
                }

                hasher.AppendData(buffer.AsSpan(0, read));
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (total == 0)
            {
                throw new InvalidOperationException("File is empty.");
            }

            byte[] hashBytes = hasher.GetHashAndReset();
            string sha256 = Convert.ToHexString(hashBytes);

            return new StoredFileInfo(total, sha256);
        }
        catch
        {
            // Best-effort cleanup of partial file.
            try
            {
                if (file is not null)
                {
                    await file.DisposeAsync();
                }
            }
            catch
            {
                // ignore cleanup failures
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }

                    break;
                }
                catch
                {
                    // Best-effort retry after async stream disposal.
                    await Task.Delay(25, cancellationToken);
                }
            }

            throw;
        }
        finally
        {
            if (file is not null)
            {
                await file.DisposeAsync();
            }
        }
    }

    public async Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ValidateStorageKey(storageKey);

        string targetPath = GetValidatedTargetPath(storageKey);
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("Attachment file missing on disk.", storageKey);
        }

        // FileStream implements IDisposable; callers must dispose.
        return new FileStream(
            path: targetPath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: 81920,
            options: FileOptions.Asynchronous);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ValidateStorageKey(storageKey);

        string targetPath = GetValidatedTargetPath(storageKey);
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        return Task.CompletedTask;
    }

    private string GetValidatedTargetPath(string storageKey)
    {
        string candidate = Path.GetFullPath(Path.Combine(_rootPathFull, storageKey));
        if (!candidate.StartsWith(_rootPathFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid attachment storage key path traversal detected.");
        }

        return candidate;
    }

    private static void ValidateStorageKey(string storageKey)
    {
        // Must not be treated as a physical path component.
        if (storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Storage key contains invalid filename characters.");
        }

        // Disallow directory separators and traversal tokens.
        if (storageKey.Contains('/') || storageKey.Contains('\\') || storageKey.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage key must not contain path separators.");
        }
    }
}

