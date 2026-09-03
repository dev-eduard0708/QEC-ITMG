namespace Qec.Itmg.Platform.Attachments;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "Platform:Attachments";

    /// <summary>
    /// Root folder for on-prem development attachment storage.
    /// Must be a path local to the host running the app.
    /// </summary>
    public string RootPath { get; set; } = "attachments";

    /// <summary>
    /// Hard limit for upload size.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MiB
}

