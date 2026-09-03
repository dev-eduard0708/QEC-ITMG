namespace Qec.Itmg.Platform.Domain;

public sealed class WorkflowTransition
{
    public Guid Id { get; private set; }

    public Guid WorkflowDefinitionId { get; private set; }

    public Guid FromStateId { get; private set; }

    public Guid ToStateId { get; private set; }

    public string? RequiredPermission { get; private set; }

    public bool RequiresReason { get; private set; }

    private WorkflowTransition()
    {
    }

    internal static WorkflowTransition Create(
        Guid workflowDefinitionId,
        Guid fromStateId,
        Guid toStateId,
        string? requiredPermission,
        bool requiresReason)
    {
        return new WorkflowTransition
        {
            Id = Guid.CreateVersion7(),
            WorkflowDefinitionId = workflowDefinitionId,
            FromStateId = fromStateId,
            ToStateId = toStateId,
            RequiredPermission = string.IsNullOrWhiteSpace(requiredPermission)
                ? null
                : requiredPermission.Trim(),
            RequiresReason = requiresReason,
        };
    }
}
