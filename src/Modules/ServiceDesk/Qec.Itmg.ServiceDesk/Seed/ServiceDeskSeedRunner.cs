using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Persistence;

namespace Qec.Itmg.ServiceDesk.Seed;

public interface IServiceDeskSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class ServiceDeskSeedRunner(
    ServiceDeskDbContext db,
    IClock clock,
    ILogger<ServiceDeskSeedRunner> logger) : IServiceDeskSeedRunner
{
    public const string DefaultQueueName = "IT Support";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        int queuesAdded = await EnsureDefaultQueueAsync(cancellationToken);
        int policiesAdded = await EnsureDefaultSlaPoliciesAsync(cancellationToken);

        logger.LogInformation(
            "Service desk seed completed. QueuesAdded={QueuesAdded} SlaPoliciesAdded={PoliciesAdded}",
            queuesAdded,
            policiesAdded);
    }

    private async Task<int> EnsureDefaultQueueAsync(CancellationToken cancellationToken)
    {
        bool exists = await db.SupportQueues.AsNoTracking()
            .AnyAsync(item => item.Name == DefaultQueueName, cancellationToken);
        if (exists)
        {
            return 0;
        }

        db.SupportQueues.Add(
            SupportQueue.Create(
                DefaultQueueName,
                clock.UtcNow,
                "Default development support queue"));
        await db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task<int> EnsureDefaultSlaPoliciesAsync(CancellationToken cancellationToken)
    {
        (string Name, TicketPriority Priority, int Response, int Resolution)[] defaults =
        [
            ("DEV Critical", TicketPriority.Critical, 15, 60),
            ("DEV High", TicketPriority.High, 60, 240),
            ("DEV Medium", TicketPriority.Medium, 240, 1440),
            ("DEV Low", TicketPriority.Low, 480, 2880),
        ];

        HashSet<string> existing = (await db.SlaPolicies.AsNoTracking()
            .Select(item => item.Name)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach ((string name, TicketPriority priority, int response, int resolution) in defaults)
        {
            if (existing.Contains(name))
            {
                continue;
            }

            db.SlaPolicies.Add(
                SlaPolicy.Create(name, priority, response, resolution, clock.UtcNow));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }
}
