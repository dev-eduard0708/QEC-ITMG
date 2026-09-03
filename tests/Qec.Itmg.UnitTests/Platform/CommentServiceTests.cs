using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Platform.Comments;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Persistence;
using Xunit;

namespace Qec.Itmg.UnitTests.Platform;

public sealed class CommentServiceTests
{
    [Fact]
    public async Task AddAndList_SupportsVisibilityFilter()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using PlatformDbContext db = CreateDb();
        CommentService service = new(db, clock);

        Guid resourceId = Guid.NewGuid();
        Guid authorId = Guid.NewGuid();

        CommentTimelineItem internalComment = await service.AddAsync(
            "Ticket",
            resourceId,
            authorId,
            "internal note",
            CommentVisibility.Internal);

        CommentTimelineItem employeeComment = await service.AddAsync(
            "Ticket",
            resourceId,
            authorId,
            "employee visible",
            CommentVisibility.EmployeeVisible);

        IReadOnlyList<CommentTimelineItem> all = await service.ListAsync("Ticket", resourceId);
        IReadOnlyList<CommentTimelineItem> employeeOnly =
            await service.ListAsync("Ticket", resourceId, CommentVisibility.EmployeeVisible);

        Assert.Equal(2, all.Count);
        Assert.Single(employeeOnly);
        Assert.Equal(employeeComment.Id, employeeOnly[0].Id);
        Assert.Equal("Internal", internalComment.Visibility);
        Assert.Equal("EmployeeVisible", employeeComment.Visibility);
    }

    [Fact]
    public async Task Edit_UpdatesBodyAndEditedAt()
    {
        VariableClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using PlatformDbContext db = CreateDb();
        CommentService service = new(db, clock);

        CommentTimelineItem created = await service.AddAsync(
            "Change",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "original",
            CommentVisibility.Internal);

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        CommentTimelineItem edited = await service.EditAsync(created.Id, "updated body");

        Assert.Equal("updated body", edited.Body);
        Assert.Equal(clock.UtcNow, edited.EditedAtUtc);
    }

    [Fact]
    public async Task Add_RejectsEmptyBodyOrResource()
    {
        FixedClock clock = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        await using PlatformDbContext db = CreateDb();
        CommentService service = new(db, clock);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(" ", Guid.NewGuid(), Guid.NewGuid(), "body", CommentVisibility.Internal));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync("Ticket", Guid.Empty, Guid.NewGuid(), "body", CommentVisibility.Internal));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync("Ticket", Guid.NewGuid(), Guid.NewGuid(), " ", CommentVisibility.Internal));
    }

    private static PlatformDbContext CreateDb()
    {
        DbContextOptions<PlatformDbContext> options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"comments-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
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
