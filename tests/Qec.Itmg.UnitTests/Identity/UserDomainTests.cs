using Qec.Itmg.Identity.Domain;
using Xunit;

namespace Qec.Itmg.UnitTests.Identity;

public sealed class UserDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ProducesActiveUser()
    {
        User user = User.Create(
            "alice@qehc.edu.sa",
            "Alice Example",
            UserType.Employee,
            Now,
            directoryObjectId: "dir-1",
            timeZone: "Asia/Riyadh");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("alice@qehc.edu.sa", user.Upn);
        Assert.Equal("Alice Example", user.DisplayName);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(UserType.Employee, user.UserType);
        Assert.Equal("dir-1", user.DirectoryObjectId);
        Assert.Equal("Asia/Riyadh", user.TimeZone);
        Assert.Equal(Now, user.CreatedAtUtc);
        Assert.Equal(Now, user.UpdatedAtUtc);
    }

    [Fact]
    public void Disable_Then_Enable_UpdatesStatus()
    {
        User user = User.Create("bob@qehc.edu.sa", "Bob", UserType.Vendor, Now);
        DateTimeOffset disabledAt = Now.AddMinutes(1);
        DateTimeOffset enabledAt = Now.AddMinutes(2);

        user.Disable(disabledAt);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Equal(disabledAt, user.UpdatedAtUtc);

        user.Enable(enabledAt);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(enabledAt, user.UpdatedAtUtc);
    }

    [Fact]
    public void Create_RejectsBlankUpn()
    {
        Assert.Throws<ArgumentException>(() => User.Create(" ", "Name", UserType.Service, Now));
    }
}
