using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Qec.Itmg.Contracts.Modules;

/// <summary>
/// Explicit module composition contract. Host registers modules directly; no assembly scanning.
/// </summary>
public interface IModule
{
    void Register(IServiceCollection services, IConfiguration configuration);
}
