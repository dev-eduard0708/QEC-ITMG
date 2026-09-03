namespace Qec.Itmg.Platform.Attachments;

public sealed record StoredFileInfo(
    long SizeBytes,
    string Sha256);

