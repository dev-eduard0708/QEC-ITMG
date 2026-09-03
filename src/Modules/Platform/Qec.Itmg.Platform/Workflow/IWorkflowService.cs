namespace Qec.Itmg.Platform.Workflow;

public interface IWorkflowService
{
    Task<WorkflowDefinitionInfo?> GetActiveDefinitionAsync(
        string workflowKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTransitionInfo>> GetAllowedTransitionsAsync(
        string workflowKey,
        string currentStateKey,
        CancellationToken cancellationToken = default);

    Task<WorkflowTransitionInfo> ValidateTransitionAsync(
        string workflowKey,
        string currentStateKey,
        string targetStateKey,
        CancellationToken cancellationToken = default);
}
