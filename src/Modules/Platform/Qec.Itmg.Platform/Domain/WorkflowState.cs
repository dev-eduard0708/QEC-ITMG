namespace Qec.Itmg.Platform.Domain;

public sealed class WorkflowState
{
    public Guid Id { get; private set; }

    public Guid WorkflowDefinitionId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsInitial { get; private set; }

    public bool IsTerminal { get; private set; }

    private WorkflowState()
    {
    }

    internal static WorkflowState Create(
        Guid workflowDefinitionId,
        string key,
        string name,
        bool isInitial,
        bool isTerminal)
    {
        return new WorkflowState
        {
            Id = Guid.CreateVersion7(),
            WorkflowDefinitionId = workflowDefinitionId,
            Key = key.Trim(),
            Name = name.Trim(),
            IsInitial = isInitial,
            IsTerminal = isTerminal,
        };
    }
}
