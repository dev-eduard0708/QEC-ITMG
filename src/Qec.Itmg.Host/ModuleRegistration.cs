using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Host.Persistence;
using Qec.Itmg.Identity;
using Qec.Itmg.AccessManagement;
using Qec.Itmg.ChangeManagement;
using Qec.Itmg.Cmdb;
using Qec.Itmg.DocumentManagement;
using Qec.Itmg.Governance;
using Qec.Itmg.Compliance;
using Qec.Itmg.Evidence;
using Qec.Itmg.Audit;
using Qec.Itmg.Security;
using Qec.Itmg.BusinessContinuity;
using Qec.Itmg.ThirdParty;
using Qec.Itmg.Reporting;
using Qec.Itmg.Ai;
using Qec.Itmg.RemoteSupport;
using Qec.Itmg.Operations;
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
            new ChangeManagementModule(),
            new OperationsModule(),
            new AccessManagementModule(),
            new DocumentManagementModule(),
            new GovernanceModule(),
            new ComplianceModule(),
            new EvidenceModule(),
            new AuditModule(),
            new SecurityModule(),
            new ContinuityModule(),
            new ThirdPartyModule(),
            new ReportingModule(),
            new AiModule(),
            new RemoteSupportModule(),
        ];

        foreach (IModule module in modules)
        {
            module.Register(builder.Services, builder.Configuration);
        }

        return builder;
    }
}
