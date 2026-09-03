using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform.Workflow;

public sealed class WorkflowService(PlatformDbContext db) : IWorkflowService
{
    public async Task<WorkflowDefinitionInfo?> GetActiveDefinitionAsync(
        string workflowKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowKey);
        WorkflowDefinition? definition = await LoadActiveAsync(workflowKey.Trim(), cancellationToken);
        return definition is null ? null : Map(definition);
    }

    public async Task<IReadOnlyList<WorkflowTransitionInfo>> GetAllowedTransitionsAsync(
        string workflowKey,
        string currentStateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentStateKey);

        WorkflowDefinition definition = await RequireActiveAsync(workflowKey, cancellationToken);
        WorkflowState current = RequireState(definition, currentStateKey);

        return definition.Transitions
            .Where(transition => transition.FromStateId == current.Id)
            .Select(transition => MapTransition(definition, transition))
            .OrderBy(transition => transition.ToStateKey, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<WorkflowTransitionInfo> ValidateTransitionAsync(
        string workflowKey,
        string currentStateKey,
        string targetStateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStateKey);

        WorkflowDefinition definition = await RequireActiveAsync(workflowKey, cancellationToken);
        WorkflowState from = RequireState(definition, currentStateKey);
        WorkflowState to = RequireState(definition, targetStateKey);

        if (from.IsTerminal)
        {
            throw new InvalidOperationException($"State '{from.Key}' is terminal and cannot transition.");
        }

        WorkflowTransition? transition = definition.Transitions.SingleOrDefault(candidate =>
            candidate.FromStateId == from.Id && candidate.ToStateId == to.Id);

        if (transition is null)
        {
            throw new InvalidOperationException(
                $"Transition '{from.Key}' → '{to.Key}' is not allowed for workflow '{definition.Key}'.");
        }

        return MapTransition(definition, transition);
    }

    private async Task<WorkflowDefinition> RequireActiveAsync(
        string workflowKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowKey);
        WorkflowDefinition? definition = await LoadActiveAsync(workflowKey.Trim(), cancellationToken);
        if (definition is null)
        {
            throw new InvalidOperationException($"No active workflow definition found for key '{workflowKey}'.");
        }

        return definition;
    }

    private async Task<WorkflowDefinition?> LoadActiveAsync(
        string workflowKey,
        CancellationToken cancellationToken)
    {
        return await db.WorkflowDefinitions
            .AsNoTracking()
            .Include(definition => definition.States)
            .Include(definition => definition.Transitions)
            .Where(definition => definition.Key == workflowKey && definition.IsActive)
            .OrderByDescending(definition => definition.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static WorkflowState RequireState(WorkflowDefinition definition, string stateKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateKey);
        string normalized = stateKey.Trim();
        WorkflowState? state = definition.States.SingleOrDefault(candidate =>
            string.Equals(candidate.Key, normalized, StringComparison.OrdinalIgnoreCase));

        if (state is null)
        {
            throw new InvalidOperationException(
                $"State '{normalized}' was not found on workflow '{definition.Key}'.");
        }

        return state;
    }

    private static WorkflowDefinitionInfo Map(WorkflowDefinition definition) =>
        new(
            definition.Id,
            definition.Key,
            definition.Name,
            definition.Version,
            definition.IsActive,
            definition.States
                .OrderBy(state => state.Key, StringComparer.Ordinal)
                .Select(state => new WorkflowStateInfo(
                    state.Id,
                    state.Key,
                    state.Name,
                    state.IsInitial,
                    state.IsTerminal))
                .ToList(),
            definition.Transitions
                .Select(transition => MapTransition(definition, transition))
                .OrderBy(transition => transition.FromStateKey, StringComparer.Ordinal)
                .ThenBy(transition => transition.ToStateKey, StringComparer.Ordinal)
                .ToList());

    private static WorkflowTransitionInfo MapTransition(
        WorkflowDefinition definition,
        WorkflowTransition transition)
    {
        WorkflowState from = definition.States.Single(state => state.Id == transition.FromStateId);
        WorkflowState to = definition.States.Single(state => state.Id == transition.ToStateId);
        return new WorkflowTransitionInfo(
            transition.Id,
            transition.FromStateId,
            from.Key,
            transition.ToStateId,
            to.Key,
            transition.RequiredPermission,
            transition.RequiresReason);
    }
}
