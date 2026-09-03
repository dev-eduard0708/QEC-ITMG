namespace Qec.Itmg.Platform.Workflow;

public sealed record WorkflowStateInfo(
    Guid Id,
    string Key,
    string Name,
    bool IsInitial,
    bool IsTerminal);

public sealed record WorkflowTransitionInfo(
    Guid Id,
    Guid FromStateId,
    string FromStateKey,
    Guid ToStateId,
    string ToStateKey,
    string? RequiredPermission,
    bool RequiresReason);

public sealed record WorkflowDefinitionInfo(
    Guid Id,
    string Key,
    string Name,
    int Version,
    bool IsActive,
    IReadOnlyList<WorkflowStateInfo> States,
    IReadOnlyList<WorkflowTransitionInfo> Transitions);
