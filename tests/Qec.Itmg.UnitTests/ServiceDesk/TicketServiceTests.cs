using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;
using Qec.Itmg.ServiceDesk.Services;
using Xunit;

namespace Qec.Itmg.UnitTests.ServiceDesk;

public sealed class TicketServiceTests
{
    [Fact]
    public async Task Incident_GetsIncNumber_AndServiceRequest_GetsSrNumber()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();
        TicketService service = scope.ServiceProvider.GetRequiredService<TicketService>();
        Guid requester = Guid.CreateVersion7();

        Ticket incident = await service.CreateAsync(
            TicketType.Incident,
            "Network down",
            "Cannot reach VPN",
            requester,
            TicketPriority.High);
        Ticket sr = await service.CreateAsync(
            TicketType.ServiceRequest,
            "Need laptop",
            "New hire kit",
            requester,
            TicketPriority.Medium);

        Assert.Equal("INC-2026-000001", incident.TicketNumber);
        Assert.Equal("SR-2026-000001", sr.TicketNumber);
    }

    [Fact]
    public async Task Create_AppliesSlaDueDates_FromSeededPolicy()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();
        ServiceDeskDbContext db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        TicketService service = scope.ServiceProvider.GetRequiredService<TicketService>();

        db.SlaPolicies.Add(
            SlaPolicy.Create("DEV Critical", TicketPriority.Critical, 15, 60, clock.UtcNow));
        await db.SaveChangesAsync();

        Ticket ticket = await service.CreateAsync(
            TicketType.Incident,
            "Outage",
            "Critical outage",
            Guid.CreateVersion7(),
            TicketPriority.Critical);

        Assert.NotNull(ticket.SlaPolicyId);
        Assert.Equal(clock.UtcNow.AddMinutes(15), ticket.ResponseDueAtUtc);
        Assert.Equal(clock.UtcNow.AddMinutes(60), ticket.ResolutionDueAtUtc);
    }

    [Fact]
    public async Task Assign_SetsQueueAndAssignee_AndWritesHistory()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();
        ServiceDeskDbContext db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        TicketService service = scope.ServiceProvider.GetRequiredService<TicketService>();

        SupportQueue queue = SupportQueue.Create("IT Support", clock.UtcNow);
        db.SupportQueues.Add(queue);
        await db.SaveChangesAsync();

        Ticket ticket = await service.CreateAsync(
            TicketType.ServiceRequest,
            "Access",
            "Need mailbox",
            Guid.CreateVersion7());
        Guid assignee = Guid.CreateVersion7();
        Guid byUser = Guid.CreateVersion7();

        Ticket assigned = await service.AssignAsync(ticket.Id, byUser, queue.Id, assignee, "desk");
        Assert.Equal(queue.Id, assigned.QueueId);
        Assert.Equal(assignee, assigned.AssignedUserId);
        Assert.Equal(TicketStatus.Open, assigned.Status);
        Assert.Equal(1, await db.TicketAssignmentHistories.CountAsync(item => item.TicketId == ticket.Id));
    }

    private static ServiceProvider BuildProvider(IClock clock)
    {
        string dbName = Guid.NewGuid().ToString("N");
        ServiceCollection services = new();
        services.AddSingleton(clock);
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase($"plt-sd-{dbName}"));
        services.AddDbContext<ServiceDeskDbContext>(options => options.UseInMemoryDatabase($"sd-{dbName}"));
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<TicketService>();
        services.AddScoped<SlaEvaluationService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
