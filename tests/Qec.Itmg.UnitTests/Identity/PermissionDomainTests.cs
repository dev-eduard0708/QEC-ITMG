using Qec.Itmg.Identity.Domain;
using Xunit;

namespace Qec.Itmg.UnitTests.Identity;

public sealed class PermissionDomainTests
{
    [Theory]
    [InlineData("ticket.read")]
    [InlineData("change.approve")]
    [InlineData("admin.users.manage")]
    public void Create_AcceptsValidKeys(string key)
    {
        Permission permission = Permission.Create(key, "demo");

        Assert.Equal(key, permission.Key);
        Assert.Equal("demo", permission.Description);
    }

    [Theory]
    [InlineData("ticket")]
    [InlineData("ticket..read")]
    [InlineData("ticket.read.extra.too")]
    [InlineData("")]
    public void Create_RejectsInvalidKeys(string key)
    {
        Assert.Throws<ArgumentException>(() => Permission.Create(key));
    }

    [Fact]
    public void Create_NormalizesKeyToLowercase()
    {
        Permission permission = Permission.Create("Admin.Roles");

        Assert.Equal("admin.roles", permission.Key);
    }
}