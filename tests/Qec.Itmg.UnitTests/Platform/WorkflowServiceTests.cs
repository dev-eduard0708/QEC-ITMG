using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.Platform.Workflow;
using Xunit;

namespace Qec.Itmg.UnitTests.Platform;

public sealed class WorkflowServiceTests
{
    [Fact]
    public async Task GetAllowedTransitions_ReturnsFromCurrentStateOnly()
    {
        await using PlatformDbContext db = CreateDb();
        await SeedTicketWorkflowAsync(db);
        WorkflowService service = new(db);

        IReadOnlyList<WorkflowTransitionInfo> allowed =
            await service.GetAllowedTransitionsAsync("ticket", "New");

        Assert.Single(allowed);
        Assert.Equal("InProgress", allowed[0].ToStateKey);
        Assert.Equal("ticket.update", allowed[0].RequiredPermission);
    }

    [Fact]
    public async Task ValidateTransition_AcceptsDefinedEdge_RejectsInvalid()
    {
        await using PlatformDbContext db = CreateDb();
        await SeedTicketWorkflowAsync(db);
        WorkflowService service = new(db);

        WorkflowTransitionInfo ok =
            await service.ValidateTransitionAsync("ticket", "InProgress", "Resolved");
        Assert.True(ok.RequiresReason);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ValidateTransitionAsync("ticket", "New", "Resolved"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ValidateTransitionAsync("ticket", "Resolved", "New"));
    }

    [Fact]
    public async Task GetActiveDefinition_ReturnsHighestActiveVersion()
    {
        await using PlatformDbContext db = CreateDb();
        WorkflowDefinition v1 = WorkflowDefinition.Create("change", "Change v1", 1, isActive: false);
        WorkflowState draft = v1.AddState("Draft", "Draft", isInitial: true, isTerminal: false);
        WorkflowState done = v1.AddState("Done", "Done", isInitial: false, isTerminal: true);
        v1.AddTransition(draft.Id, done.Id);
        db.WorkflowDefinitions.Add(v1);

        WorkflowDefinition v2 = WorkflowDefinition.Create("change", "Change v2", 2, isActive: true);
        WorkflowState open = v2.AddState("Open", "Open", isInitial: true, isTerminal: false);
        WorkflowState closed = v2.AddState("Closed", "Closed", isInitial: false, isTerminal: true);
        v2.AddTransition(open.Id, closed.Id);
        db.WorkflowDefinitions.Add(v2);
        await db.SaveChangesAsync();

        WorkflowService service = new(db);
        WorkflowDefinitionInfo? active = await service.GetActiveDefinitionAsync("change");

        Assert.NotNull(active);
        Assert.Equal(2, active!.Version);
        Assert.Contains(active.States, state => state.Key == "Open");
    }

    private static async Task SeedTicketWorkflowAsync(PlatformDbContext db)
    {
        WorkflowDefinition definition = WorkflowDefinition.Create("ticket", "Ticket", 1, isActive: true);
        WorkflowState neu = definition.AddState("New", "New", isInitial: true, isTerminal: false);
        WorkflowState inProgress = definition.AddState("InProgress", "In Progress", isInitial: false, isTerminal: false);
        WorkflowState resolved = definition.AddState("Resolved", "Resolved", isInitial: false, isTerminal: true);

        definition.AddTransition(neu.Id, inProgress.Id, requiredPermission: "ticket.update");
        definition.AddTransition(inProgress.Id, resolved.Id, requiresReason: true);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }

    private static PlatformDbContext CreateDb()
    {
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"workflow-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
    }
}
