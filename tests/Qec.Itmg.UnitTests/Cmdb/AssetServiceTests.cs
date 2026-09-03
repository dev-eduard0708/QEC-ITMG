using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Cmdb;

public sealed class AssetServiceTests
{
    [Fact]
    public async Task Create_IssuesAssetNumber()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();
        AssetService service = scope.ServiceProvider.GetRequiredService<AssetService>();

        Asset asset = await service.CreateAsync("Laptop", "Dell XPS");
        Assert.Equal("AST-2026-000001", asset.AssetNumber);
        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public async Task Assign_EnforcesSingleActiveAssignment_AndReturnClosesIt()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider provider = BuildProvider(clock);
        using IServiceScope scope = provider.CreateScope();
        CmdbDbContext db = scope.ServiceProvider.GetRequiredService<CmdbDbContext>();
        AssetService service = scope.ServiceProvider.GetRequiredService<AssetService>();

        Asset asset = await service.CreateAsync("Laptop", "Dell XPS");
        Guid toUser = Guid.CreateVersion7();
        Guid byUser = Guid.CreateVersion7();

        AssetAssignment first = await service.AssignAsync(asset.Id, toUser, byUser);
        Assert.Null(first.ReturnedAtUtc);
        Assert.Equal(AssetStatus.Assigned, (await db.Assets.SingleAsync(a => a.Id == asset.Id)).Status);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(asset.Id, Guid.CreateVersion7(), byUser));

        AssetAssignment closed = await service.ReturnAsync(asset.Id, "returned");
        Assert.NotNull(closed.ReturnedAtUtc);
        Assert.Equal(AssetStatus.InStock, (await db.Assets.SingleAsync(a => a.Id == asset.Id)).Status);
        Assert.Equal(1, await db.AssetAssignments.CountAsync(a => a.AssetId == asset.Id));
    }

    private static ServiceProvider BuildProvider(IClock clock)
    {
        string dbName = Guid.NewGuid().ToString("N");
        ServiceCollection services = new();
        services.AddSingleton(clock);
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase($"plt-asset-{dbName}"));
        services.AddDbContext<CmdbDbContext>(options => options.UseInMemoryDatabase($"cmdb-asset-{dbName}"));
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<AssetService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
