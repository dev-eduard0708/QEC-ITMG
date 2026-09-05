namespace Qec.Itmg.Contracts.Ai;

public enum AiProviderKind
{
    Disabled = 0,
    OpenAICompatible = 1,
}

public sealed record AiToolDefinition(
    string Name,
    string Description,
    string ParametersJsonSchema,
    bool IsReadOnly = true);

public sealed record AiToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

public sealed record AiMessage(
    string Role,
    string Content,
    IReadOnlyList<AiToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? Name = null);

public sealed record AiModelRequest(
    string CorrelationId,
    IReadOnlyList<AiMessage> Messages,
    IReadOnlyList<AiToolDefinition> Tools,
    double Temperature = 0.2,
    int MaxTokens = 1200);

public sealed record AiModelResponse(
    string? Content,
    IReadOnlyList<AiToolCall> ToolCalls,
    string? FinishReason,
    string? ProviderRequestId);

public interface IAiModelClient
{
    AiReadiness GetReadiness();
    Task<AiModelResponse> CompleteAsync(AiModelRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiReadiness(
    bool Enabled,
    bool Configured,
    string ProviderKind,
    string? ModelName,
    string Status,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastErrorSummary);
