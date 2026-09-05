namespace Qec.Itmg.Ai.Domain;

public enum AiInteractionStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Denied = 4,
    Disabled = 5,
}

public sealed class AiInteraction
{
    private AiInteraction() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string CorrelationId { get; private set; } = null!;
    public string Capability { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string? ModelName { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public AiInteractionStatus Status { get; private set; }
    public int ToolCallCount { get; private set; }
    public int RedactionCount { get; private set; }
    public string? ClassificationContext { get; private set; }
    public string? ErrorSummary { get; private set; }

    public ICollection<AiToolInvocation> ToolInvocations { get; private set; } = new List<AiToolInvocation>();

    public static AiInteraction Start(
        Guid userId,
        string correlationId,
        string capability,
        string provider,
        string? modelName,
        DateTimeOffset startedAtUtc,
        string? classificationContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        return new AiInteraction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CorrelationId = correlationId.Trim(),
            Capability = capability.Trim(),
            Provider = provider.Trim(),
            ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim(),
            StartedAtUtc = startedAtUtc,
            Status = AiInteractionStatus.Running,
            ClassificationContext = classificationContext,
        };
    }

    public void Complete(
        AiInteractionStatus status,
        DateTimeOffset completedAtUtc,
        int toolCallCount,
        int redactionCount,
        string? errorSummary = null)
    {
        Status = status;
        CompletedAtUtc = completedAtUtc;
        ToolCallCount = toolCallCount;
        RedactionCount = redactionCount;
        ErrorSummary = string.IsNullOrWhiteSpace(errorSummary)
            ? null
            : errorSummary[..Math.Min(errorSummary.Length, 1000)];
    }
}

public sealed class AiToolInvocation
{
    private AiToolInvocation() { }

    public Guid Id { get; private set; }
    public Guid InteractionId { get; private set; }
    public string ToolName { get; private set; } = null!;
    public string? RecordType { get; private set; }
    public Guid? RecordId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string Result { get; private set; } = null!;

    public static AiToolInvocation Start(
        Guid interactionId,
        string toolName,
        DateTimeOffset startedAtUtc,
        string? recordType = null,
        Guid? recordId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return new AiToolInvocation
        {
            Id = Guid.CreateVersion7(),
            InteractionId = interactionId,
            ToolName = toolName.Trim(),
            RecordType = recordType,
            RecordId = recordId,
            StartedAtUtc = startedAtUtc,
            Result = "Running",
        };
    }

    public void Complete(string result, DateTimeOffset completedAtUtc)
    {
        Result = string.IsNullOrWhiteSpace(result) ? "Completed" : result[..Math.Min(result.Length, 256)];
        CompletedAtUtc = completedAtUtc;
    }
}
