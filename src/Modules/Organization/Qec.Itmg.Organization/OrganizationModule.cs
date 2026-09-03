using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.Organization;

/// <summary>
/// Organization module composition. Domain types are added in Phase 1.
/// </summary>
public sealed class OrganizationModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Organization domain services are registered in Phase 1.
    }
}
