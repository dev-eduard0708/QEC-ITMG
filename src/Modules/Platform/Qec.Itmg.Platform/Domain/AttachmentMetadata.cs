namespace Qec.Itmg.Platform.Domain;

/// <summary>
/// Reusable attachment metadata entity (disk storage key only, no absolute paths).
/// </summary>
public sealed class AttachmentMetadata
{
    public Guid Id { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    /// <summary>
    /// Generated storage key (never trust original filename as a disk path).
    /// </summary>
    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>
    /// SHA-256 hex string.
    /// </summary>
    public string Sha256 { get; private set; } = string.Empty;

    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    /// <summary>
    /// Optional owning resource type (e.g. Ticket). Null = unbound metadata.
    /// </summary>
    public string? ResourceType { get; private set; }

    /// <summary>
    /// Optional owning resource id. Null = unbound metadata.
    /// </summary>
    public Guid? ResourceId { get; private set; }

    public MalwareScanStatus ScanStatus { get; private set; }

    public string? ScanProvider { get; private set; }

    public string? ScanMessage { get; private set; }

    public DateTimeOffset? ScannedAtUtc { get; private set; }

    /// <summary>
    /// Used for concurrency when scan status is updated.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    private AttachmentMetadata()
    {
    }

    public static AttachmentMetadata CreateUploaded(
        Guid uploadedByUserId,
        string originalFileName,
        string storageKey,
        string contentType,
        long sizeBytes,
        string sha256,
        DateTimeOffset uploadedAtUtc,
        string? resourceType = null,
        Guid? resourceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size must be > 0.");
        }

        AttachmentMetadata metadata = new()
        {
            Id = Guid.CreateVersion7(),
            UploadedByUserId = uploadedByUserId,
            OriginalFileName = originalFileName.Trim(),
            StorageKey = storageKey.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            Sha256 = sha256.Trim(),
            UploadedAtUtc = uploadedAtUtc,
            ScanStatus = MalwareScanStatus.Pending,
        };

        if (!string.IsNullOrWhiteSpace(resourceType) && resourceId is Guid id && id != Guid.Empty)
        {
            metadata.BindToResource(resourceType, id);
        }

        return metadata;
    }

    public void BindToResource(string resourceType, Guid resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));
        }

        if (!string.IsNullOrWhiteSpace(ResourceType) || ResourceId is not null)
        {
            throw new InvalidOperationException("Attachment is already bound to a resource.");
        }

        ResourceType = resourceType.Trim();
        ResourceId = resourceId;
    }

    public void StartScanning(DateTimeOffset utcNow)
    {
        if (ScanStatus != MalwareScanStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Invalid malware scan transition: {ScanStatus} -> Scanning");
        }

        ScanStatus = MalwareScanStatus.Scanning;
        ScanProvider = null;
        ScanMessage = null;
        ScannedAtUtc = null;
    }

    public void MarkNotConfigured(string? provider, string? message, DateTimeOffset utcNow)
    {
        if (ScanStatus != MalwareScanStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Invalid malware scan transition: {ScanStatus} -> NotConfigured");
        }

        ScanStatus = MalwareScanStatus.NotConfigured;
        ScanProvider = provider;
        ScanMessage = message;
        ScannedAtUtc = utcNow;
    }

    public void MarkClean(string? provider, string? message, DateTimeOffset utcNow) =>
        MarkVerdict(MalwareScanStatus.Clean, provider, message, utcNow);

    public void MarkInfected(string? provider, string? message, DateTimeOffset utcNow) =>
        MarkVerdict(MalwareScanStatus.Infected, provider, message, utcNow);

    public void MarkFailed(string? provider, string? message, DateTimeOffset utcNow) =>
        MarkVerdict(MalwareScanStatus.Failed, provider, message, utcNow);

    private void MarkVerdict(
        MalwareScanStatus verdict,
        string? provider,
        string? message,
        DateTimeOffset utcNow)
    {
        if (ScanStatus != MalwareScanStatus.Scanning)
        {
            throw new InvalidOperationException(
                $"Invalid malware scan transition: {ScanStatus} -> {verdict}");
        }

        if (verdict is not (MalwareScanStatus.Clean or MalwareScanStatus.Infected or MalwareScanStatus.Failed))
        {
            throw new InvalidOperationException($"Invalid scan verdict: {verdict}");
        }

        ScanStatus = verdict;
        ScanProvider = provider;
        ScanMessage = message;
        ScannedAtUtc = utcNow;
    }
}

