using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Platform;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task ListAndUnreadCount_AreScopedToRecipient()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero));
        await using PlatformDbContext db = CreateDb();
        NotificationService service = new(db, clock);

        Guid userA = Guid.NewGuid();
        Guid userB = Guid.NewGuid();

        await service.CreateAsync(userA, "ticket.assigned", NotificationSeverity.Info, "A1", "for A");
        await service.CreateAsync(userA, "ticket.assigned", NotificationSeverity.Warning, "A2", "for A again");
        await service.CreateAsync(userB, "ticket.assigned", NotificationSeverity.Info, "B1", "for B");

        IReadOnlyList<NotificationDto> forA = await service.ListForUserAsync(userA);
        Assert.Equal(2, forA.Count);
        Assert.All(forA, item => Assert.False(item.IsRead));
        Assert.Equal(2, await service.GetUnreadCountAsync(userA));
        Assert.Equal(1, await service.GetUnreadCountAsync(userB));
    }

    [Fact]
    public async Task MarkRead_OnlyAffectsOwnNotification()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero));
        await using PlatformDbContext db = CreateDb();
        NotificationService service = new(db, clock);

        Guid owner = Guid.NewGuid();
        Guid other = Guid.NewGuid();

        NotificationDto created = await service.CreateAsync(
            owner,
            "change.approved",
            NotificationSeverity.Info,
            "Approved",
            "Your change was approved",
            actionUrl: "/it/changes/1");

        NotificationDto? stolen = await service.MarkReadAsync(other, created.Id);
        Assert.Null(stolen);
        Assert.Equal(1, await service.GetUnreadCountAsync(owner));

        NotificationDto? marked = await service.MarkReadAsync(owner, created.Id);
        Assert.NotNull(marked);
        Assert.True(marked!.IsRead);
        Assert.Equal(0, await service.GetUnreadCountAsync(owner));
    }

    private static PlatformDbContext CreateDb()
    {
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"notifications-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
