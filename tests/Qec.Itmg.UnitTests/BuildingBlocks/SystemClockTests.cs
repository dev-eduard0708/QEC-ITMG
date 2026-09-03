using Qec.Itmg.BuildingBlocks.Time;
using Xunit;

namespace Qec.Itmg.UnitTests.BuildingBlocks;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_UsesUtcOffset()
    {
        IClock clock = new SystemClock();

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }
}
