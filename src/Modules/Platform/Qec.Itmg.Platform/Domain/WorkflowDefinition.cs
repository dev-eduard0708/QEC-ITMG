namespace Qec.Itmg.Platform.Domain;

public sealed class WorkflowDefinition
{
    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<WorkflowState> States { get; private set; } = new List<WorkflowState>();

    public ICollection<WorkflowTransition> Transitions { get; private set; } = new List<WorkflowTransition>();

    private WorkflowDefinition()
    {
    }

    public static WorkflowDefinition Create(string key, string name, int version, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        return new WorkflowDefinition
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim(),
            Name = name.Trim(),
            Version = version,
            IsActive = isActive,
        };
    }

    public WorkflowState AddState(string key, string name, bool isInitial, bool isTerminal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (isInitial && States.Any(state => state.IsInitial))
        {
            throw new InvalidOperationException("Workflow already has an initial state.");
        }

        WorkflowState state = WorkflowState.Create(Id, key, name, isInitial, isTerminal);
        States.Add(state);
        return state;
    }

    public WorkflowTransition AddTransition(
        Guid fromStateId,
        Guid toStateId,
        string? requiredPermission = null,
        bool requiresReason = false)
    {
        if (fromStateId == Guid.Empty || toStateId == Guid.Empty)
        {
            throw new ArgumentException("From/To state ids must not be empty.");
        }

        if (!States.Any(state => state.Id == fromStateId) || !States.Any(state => state.Id == toStateId))
        {
            throw new InvalidOperationException("Transition states must belong to this workflow definition.");
        }

        WorkflowTransition transition = WorkflowTransition.Create(
            Id,
            fromStateId,
            toStateId,
            requiredPermission,
            requiresReason);
        Transitions.Add(transition);
        return transition;
    }
}
