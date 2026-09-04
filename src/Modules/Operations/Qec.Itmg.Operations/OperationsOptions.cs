namespace Qec.Itmg.Operations;

public sealed class OperationsOptions
{
    public const string SectionName = "Operations";

    /// <summary>Hot retention for closed events (days). Default 90.</summary>
    public int ClosedEventRetentionDays { get; set; } = 90;

    /// <summary>Absolute max age for closed events regardless (days). Default 180.</summary>
    public int EventRetentionDays { get; set; } = 180;
}
