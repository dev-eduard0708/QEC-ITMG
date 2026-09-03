using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.Identity;

/// <summary>
/// Identity module composition. Domain types are added in Phase 1.
/// </summary>
public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Identity domain services are registered in Phase 1.
    }
}
