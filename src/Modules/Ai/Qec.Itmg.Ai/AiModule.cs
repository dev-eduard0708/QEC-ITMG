using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.Ai.Persistence;
using Qec.Itmg.Ai.Services;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.Contracts.Ai;
using Qec.Itmg.Contracts.Modules;

namespace Qec.Itmg.Ai;

public sealed class AiModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        AiOptions aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        services.AddSingleton(Options.Create(aiOptions));
        services.AddSingleton<AiHealthState>();
        services.AddSingleton<DisabledAiModelClient>();
        services.AddHttpClient("ai-openai");
        services.AddSingleton<OpenAiCompatibleAiModelClient>();
        services.AddSingleton<IAiModelClient, ConfigurableAiModelClient>();
        services.AddSingleton<IAiRedactionPipeline, AiRedactionPipeline>();

        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<AiDbContext>(connectionString, AiDbContext.SchemaName);
        services.AddScoped<AiInteractionService>();
    }
}
