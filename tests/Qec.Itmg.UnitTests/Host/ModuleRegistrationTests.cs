using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity;
using Qec.Itmg.Organization;
using Qec.Itmg.Platform;
using Xunit;

namespace Qec.Itmg.UnitTests.Host;

public sealed class ModuleRegistrationTests
{
    [Fact]
    public void PlatformModule_RegistersSystemClock()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        new PlatformModule().Register(services, configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IClock clock = provider.GetRequiredService<IClock>();

        Assert.IsType<SystemClock>(clock);
        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }

    [Fact]
    public void FoundationalModules_RegisterWithoutThrowing()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        new IdentityModule().Register(services, configuration);
        new OrganizationModule().Register(services, configuration);
        new PlatformModule().Register(services, configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IClock>());
    }
}
