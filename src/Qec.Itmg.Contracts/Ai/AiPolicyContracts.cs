namespace Qec.Itmg.Contracts.Ai;

/// <summary>
/// Data classification for AI context inclusion. Restricted defaults to DENY.
/// </summary>
public enum AiDataClassification
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3,
}

public sealed record AiRedactionResult(
    string Text,
    int RedactionCount,
    IReadOnlyList<string> Categories);

public interface IAiRedactionPipeline
{
    AiRedactionResult Redact(string? input, AiDataClassification classification);
    bool MayIncludeInModelContext(AiDataClassification classification, bool confidentialAllowed);
}

/// <summary>
/// Explicit deny categories so future tool registration cannot expose unattended remote / production control.
/// </summary>
public static class AiDeniedToolCategories
{
    public static readonly string[] Names =
    [
        "remote.start",
        "remote.authorize",
        "remote.unattended",
        "remote.meshcentral",
        "device.execute",
        "shell.execute",
        "sql.execute",
        "http.arbitrary",
        "filesystem.write",
        "integration.enable",
        "change.approve",
        "change.implement",
        "access.approve",
        "jml.fulfill",
        "control.assess.complete",
        "evidence.accept",
        "finding.close",
        "capa.verify",
        "vulnerability.remediate",
        "risk.accept",
        "policy.approve",
        "vm.power",
        "backup.execute",
        "restore.execute",
    ];

    public static bool IsDenied(string toolName) =>
        Names.Any(n => string.Equals(n, toolName, StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("remote.", StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("shell.", StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("sql.", StringComparison.OrdinalIgnoreCase));
}
