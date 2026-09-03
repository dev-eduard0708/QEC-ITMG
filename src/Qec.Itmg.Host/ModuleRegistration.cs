using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Identity;
using Qec.Itmg.Cmdb;
using Qec.Itmg.Organization;
using Qec.Itmg.Platform;
using Qec.Itmg.ServiceDesk;

namespace Qec.Itmg.Host;

internal static class ModuleRegistration
{
    public static WebApplicationBuilder AddQecModules(this WebApplicationBuilder builder)
    {
        // Shared scoped SQL connection so module DbContexts can enlist in one transaction.
        builder.Services.AddScoped<ISharedDbConnectionAccessor, SharedAppSqlConnection>();

        IModule[] modules =
        [
            new IdentityModule(),
            new OrganizationModule(),
            new PlatformModule(),
            new CmdbModule(),
            new ServiceDeskModule(),
        ];

        foreach (IModule module in modules)
        {
            module.Register(builder.Services, builder.Configuration);
        }

        return builder;
    }
}
