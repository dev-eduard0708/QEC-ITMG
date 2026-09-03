using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;

namespace Qec.Itmg.Cmdb.Seed;

public interface ICmdbSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class CmdbSeedRunner(
    CmdbDbContext db,
    IClock clock,
    ILogger<CmdbSeedRunner> logger) : ICmdbSeedRunner
{
    private static readonly (string Key, string Name, string Description)[] DefaultTypes =
    [
        ("laptop", "Laptop", "Employee and staff laptops"),
        ("server", "Server", "Physical or virtual servers"),
        ("application", "Application", "Business applications and software systems"),
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        string[] keys = DefaultTypes.Select(static item => item.Key).ToArray();
        HashSet<string> existing = (await db.CiTypes.AsNoTracking()
            .Where(item => keys.Contains(item.Key))
            .Select(item => item.Key)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach ((string key, string name, string description) in DefaultTypes)
        {
            if (existing.Contains(key))
            {
                continue;
            }

            db.CiTypes.Add(CiType.Create(key, name, clock.UtcNow, description));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "CMDB seed completed. Ensured CI types laptop/server/application (added {AddedCount}).",
            added);
    }
}
