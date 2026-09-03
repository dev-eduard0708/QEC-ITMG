using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Platform.Audit;
using Qec.Itmg.Platform.Persistence;

namespace Qec.Itmg.Platform;

/// <summary>
/// Platform module composition: shared platform services and audit persistence.
/// </summary>
public sealed class PlatformModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        SmtpEmailOptions smtpOptions = configuration
            .GetSection(SmtpEmailOptions.SectionName)
            .Get<SmtpEmailOptions>()
            ?? new SmtpEmailOptions();
        services.AddSingleton(Options.Create(smtpOptions));
        services.AddSingleton<IEmailSender, NullEmailSender>();

        string? connectionString = configuration.GetConnectionString(QecEfConventions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{QecEfConventions.ConnectionStringName}' is not configured.");
        }

        services.AddQecSqlServerDbContext<PlatformDbContext>(
            connectionString,
            PlatformDbContext.SchemaName);

        services.AddScoped<IBusinessAuditWriter, EfBusinessAuditWriter>();
        services.AddScoped<ISecurityAuditLogger, EfSecurityAuditLogger>();
    }
}
