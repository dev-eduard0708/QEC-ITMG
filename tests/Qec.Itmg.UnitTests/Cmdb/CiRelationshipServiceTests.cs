using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Cmdb.Services;
using Xunit;

namespace Qec.Itmg.UnitTests.Cmdb;

public sealed class CiRelationshipServiceTests
{
    [Fact]
    public async Task Create_RejectsSelfRelationship()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();
        CmdbDbContext db = scope.ServiceProvider.GetRequiredService<CmdbDbContext>();
        CiRelationshipService service = scope.ServiceProvider.GetRequiredService<CiRelationshipService>();

        CiType type = CiType.Create("server", "Server", clock.UtcNow);
        ConfigurationItem ci = ConfigurationItem.Create("CI-2026-000001", type.Id, "Self", clock.UtcNow);
        db.CiTypes.Add(type);
        db.ConfigurationItems.Add(ci);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ci.Id, ci.Id, CiRelationshipType.DependsOn));
    }

    private static ServiceProvider BuildProvider(IClock clock)
    {
        string dbName = Guid.NewGuid().ToString("N");
        ServiceCollection services = new();
        services.AddSingleton(clock);
        services.AddDbContext<CmdbDbContext>(options => options.UseInMemoryDatabase($"rel-{dbName}"));
        services.AddScoped<CiRelationshipService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
