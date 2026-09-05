using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.RemoteSupport.Persistence;
using Qec.Itmg.RemoteSupport.Services;

namespace Qec.Itmg.RemoteSupport;

public sealed class RemoteSupportModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        RemoteSupportOptions remoteOptions =
            configuration.GetSection(RemoteSupportOptions.SectionName).Get<RemoteSupportOptions>()
            ?? new RemoteSupportOptions();
        services.AddSingleton(Options.Create(remoteOptions));
        services.AddSingleton<RemoteEngineHealthState>();
        services.AddSingleton<DisabledRemoteSupportEngine>();
        services.AddHttpClient("remote-meshcentral");
        services.AddSingleton<MeshCentralRemoteSupportEngine>();
        services.AddSingleton<IRemoteSupportEngine, ConfigurableRemoteSupportEngine>();

        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<RemoteSupportDbContext>(
            connectionString,
            RemoteSupportDbContext.SchemaName);
        services.AddScoped<RemoteSessionService>();
    }
}
