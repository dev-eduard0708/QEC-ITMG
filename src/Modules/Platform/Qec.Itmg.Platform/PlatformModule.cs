using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.Platform;

/// <summary>
/// Platform module composition. Additional shared platform services are added in Phase 2.
/// </summary>
public sealed class PlatformModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
    }
}
