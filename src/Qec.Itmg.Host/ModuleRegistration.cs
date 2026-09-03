using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Identity;
using Qec.Itmg.Organization;
using Qec.Itmg.Platform;

namespace Qec.Itmg.Host;

internal static class ModuleRegistration
{
    public static WebApplicationBuilder AddQecModules(this WebApplicationBuilder builder)
    {
        IModule[] modules =
        [
            new IdentityModule(),
            new OrganizationModule(),
            new PlatformModule(),
        ];

        foreach (IModule module in modules)
        {
            module.Register(builder.Services, builder.Configuration);
        }

        return builder;
    }
}
