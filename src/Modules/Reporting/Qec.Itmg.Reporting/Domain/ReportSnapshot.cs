namespace Qec.Itmg.Reporting.Domain;

public sealed class ReportSnapshot
{
    private ReportSnapshot() { }

    public Guid Id { get; private set; }
    public string SnapshotKey { get; private set; } = null!;
    public DateTimeOffset SnapshotDateUtc { get; private set; }
    public DateTimeOffset? PeriodStartUtc { get; private set; }
    public DateTimeOffset? PeriodEndUtc { get; private set; }
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ReportSnapshot Create(
        string snapshotKey,
        DateTimeOffset snapshotDateUtc,
        string payloadJson,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? periodStartUtc = null,
        DateTimeOffset? periodEndUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        return new ReportSnapshot
        {
            Id = Guid.CreateVersion7(),
            SnapshotKey = snapshotKey.Trim(),
            SnapshotDateUtc = snapshotDateUtc,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            PayloadJson = payloadJson,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void ReplacePayload(
        string payloadJson,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        PayloadJson = payloadJson;
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
    }
}
