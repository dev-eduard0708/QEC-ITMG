using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Cmdb;

public sealed class ConfigurationItemServiceTests
{
    [Fact]
    public async Task CreateConfigurationItem_IssuesCiNumber()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();

        CmdbDbContext cmdb = scope.ServiceProvider.GetRequiredService<CmdbDbContext>();
        ConfigurationItemService service = scope.ServiceProvider.GetRequiredService<ConfigurationItemService>();

        CiType type = await service.CreateCiTypeAsync("server", "Server");
        ConfigurationItem item = await service.CreateConfigurationItemAsync(type.Id, "App Server 1");

        Assert.Equal("CI-2026-000001", item.CiNumber);
        Assert.Equal(ConfigurationItemStatus.Active, item.Status);
        Assert.Equal(1, await cmdb.ConfigurationItems.CountAsync());
    }

    private static ServiceProvider BuildProvider(IClock clock)
    {
        string dbName = Guid.NewGuid().ToString("N");
        ServiceCollection services = new();
        services.AddSingleton(clock);
        services.AddDbContext<PlatformDbContext>(options =>
            options.UseInMemoryDatabase($"plt-{dbName}"));
        services.AddDbContext<CmdbDbContext>(options =>
            options.UseInMemoryDatabase($"cmdb-{dbName}"));
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ConfigurationItemService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
