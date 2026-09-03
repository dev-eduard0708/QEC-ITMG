using Qec.Itmg.Identity.Authentication;
using Xunit;

namespace Qec.Itmg.UnitTests.Authentication;

public sealed class LocalReturnUrlTests
{
    [Theory]
    [InlineData("/it", "/it")]
    [InlineData("/employee", "/employee")]
    [InlineData("/", "/")]
    [InlineData("/governance/controls", "/governance/controls")]
    public void Sanitize_AllowsLocalPaths(string input, string expected)
    {
        Assert.Equal(expected, LocalReturnUrl.Sanitize(input));
        Assert.True(LocalReturnUrl.IsLocalPath(input));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example/it")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("employee")]
    [InlineData("")]
    public void Sanitize_RejectsExternalOrInvalidUrls(string input)
    {
        Assert.Equal("/", LocalReturnUrl.Sanitize(input));
        Assert.False(LocalReturnUrl.IsLocalPath(input));
    }
}
