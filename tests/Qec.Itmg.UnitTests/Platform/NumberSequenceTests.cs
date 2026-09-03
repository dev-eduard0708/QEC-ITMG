using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Platform;

public sealed class NumberSequenceTests
{
    [Fact]
    public async Task NextAsync_SequentialNumbersIncrement()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"seq-{Guid.NewGuid():N}")
            .Options;

        await using PlatformDbContext db = new(options);
        NumberSequenceService service = new(db, clock);

        string n1 = await service.NextAsync(sequenceKey: "tickets", prefix: "INC");
        string n2 = await service.NextAsync(sequenceKey: "tickets", prefix: "INC");

        Assert.Equal("INC-2026-000001", n1);
        Assert.Equal("INC-2026-000002", n2);
    }

    [Fact]
    public async Task NextAsync_DifferentKeys_AreIndependent()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"seq-{Guid.NewGuid():N}")
            .Options;

        await using PlatformDbContext db = new(options);
        NumberSequenceService service = new(db, clock);

        string n1 = await service.NextAsync(sequenceKey: "tickets", prefix: "INC");
        string n2 = await service.NextAsync(sequenceKey: "changes", prefix: "CHG");

        Assert.Equal("INC-2026-000001", n1);
        Assert.Equal("CHG-2026-000001", n2);
    }

    [Fact]
    public async Task NextAsync_ResetsIndependentlyPerYear()
    {
        VariableClock clock = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"seq-{Guid.NewGuid():N}")
            .Options;

        await using PlatformDbContext db = new(options);
        NumberSequenceService service = new(db, clock);

        string n1 = await service.NextAsync(sequenceKey: "tickets", prefix: "INC");
        clock.UtcNow = new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero);
        string n2 = await service.NextAsync(sequenceKey: "tickets", prefix: "INC");

        Assert.Equal("INC-2026-000001", n1);
        Assert.Equal("INC-2027-000001", n2);
    }

    [Fact]
    public async Task NextAsync_RejectsEmptyKeyOrPrefix()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"seq-{Guid.NewGuid():N}")
            .Options;

        await using PlatformDbContext db = new(options);
        NumberSequenceService service = new(db, clock);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.NextAsync(sequenceKey: " ", prefix: "INC"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.NextAsync(sequenceKey: "tickets", prefix: " "));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class VariableClock : IClock
    {
        public VariableClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }
}

