namespace Qec.Itmg.DocumentManagement;

public sealed class DocumentManagementOptions
{
    public const string SectionName = "DocumentManagement";

    /// <summary>Days before review date to start "due soon" UI flag. Default 30.</summary>
    public int ReviewDueSoonDays { get; set; } = 30;
}
