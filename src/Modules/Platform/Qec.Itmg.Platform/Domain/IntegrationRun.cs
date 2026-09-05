namespace Qec.Itmg.Platform.Domain;

public enum IntegrationRunStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Skipped = 4,
}

public sealed class IntegrationRun
{
    private IntegrationRun() { }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = null!;
    public string Operation { get; private set; } = null!;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public IntegrationRunStatus Status { get; private set; }
    public int ProcessedCount { get; private set; }
    public int SucceededCount { get; private set; }
    public int FailedCount { get; private set; }
    public int UnmatchedCount { get; private set; }
    public string? ErrorSummary { get; private set; }
    public string CorrelationId { get; private set; } = null!;

    public static IntegrationRun Start(string provider, string operation, DateTimeOffset startedAtUtc, string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return new IntegrationRun
        {
            Id = Guid.CreateVersion7(),
            Provider = provider.Trim(),
            Operation = operation.Trim(),
            StartedAtUtc = startedAtUtc,
            Status = IntegrationRunStatus.Running,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.CreateVersion7().ToString("N") : correlationId.Trim(),
        };
    }

    public void Complete(
        IntegrationRunStatus status,
        DateTimeOffset completedAtUtc,
        int processed,
        int succeeded,
        int failed,
        int unmatched,
        string? errorSummary)
    {
        if (Status != IntegrationRunStatus.Running)
            return;
        Status = status;
        CompletedAtUtc = completedAtUtc;
        ProcessedCount = processed;
        SucceededCount = succeeded;
        FailedCount = failed;
        UnmatchedCount = unmatched;
        ErrorSummary = Truncate(errorSummary, 2000);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : (value.Length <= max ? value : value[..max]);
}

public sealed class IntegrationWebhookReceipt
{
    private IntegrationWebhookReceipt() { }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ExternalEventId { get; private set; } = null!;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public string Result { get; private set; } = null!;
    public string PayloadHash { get; private set; } = null!;
    public string? ErrorSummary { get; private set; }

    public static IntegrationWebhookReceipt Create(
        string provider,
        string externalEventId,
        DateTimeOffset receivedAtUtc,
        string payloadHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
        return new IntegrationWebhookReceipt
        {
            Id = Guid.CreateVersion7(),
            Provider = provider.Trim().ToLowerInvariant(),
            ExternalEventId = externalEventId.Trim(),
            ReceivedAtUtc = receivedAtUtc,
            Result = "Received",
            PayloadHash = payloadHash,
        };
    }

    public void MarkProcessed(string result, DateTimeOffset processedAtUtc, string? errorSummary = null)
    {
        Result = result;
        ProcessedAtUtc = processedAtUtc;
        ErrorSummary = string.IsNullOrWhiteSpace(errorSummary) ? null : errorSummary[..Math.Min(errorSummary.Length, 1000)];
    }
}

public sealed class IntegrationCorrelation
{
    private IntegrationCorrelation() { }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ExternalId { get; private set; } = null!;
    public string TargetType { get; private set; } = null!;
    public Guid? TargetId { get; private set; }
    public string? DisplayName { get; private set; }
    public string MatchStatus { get; private set; } = null!;
    public string? MetadataJson { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static IntegrationCorrelation Create(
        string provider,
        string externalId,
        string targetType,
        string matchStatus,
        DateTimeOffset updatedAtUtc,
        Guid? targetId = null,
        string? displayName = null,
        string? metadataJson = null)
    {
        return new IntegrationCorrelation
        {
            Id = Guid.CreateVersion7(),
            Provider = provider.Trim(),
            ExternalId = externalId.Trim(),
            TargetType = targetType.Trim(),
            TargetId = targetId,
            DisplayName = displayName,
            MatchStatus = matchStatus,
            MetadataJson = metadataJson,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    public void UpdateMatch(Guid? targetId, string matchStatus, DateTimeOffset updatedAtUtc, string? metadataJson = null)
    {
        TargetId = targetId;
        MatchStatus = matchStatus;
        UpdatedAtUtc = updatedAtUtc;
        if (metadataJson is not null)
            MetadataJson = metadataJson;
    }
}
