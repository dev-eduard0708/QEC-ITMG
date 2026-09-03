using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Audit;
using Qec.Itmg.Platform.Comments;
using Qec.Itmg.Platform.Integrations;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.Platform.Workflow;

namespace Qec.Itmg.Platform;

/// <summary>
/// Platform module composition: shared platform services, audit persistence, and disabled integration adapters.
/// </summary>
public sealed class PlatformModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        AttachmentStorageOptions attachmentOptions = configuration
            .GetSection(AttachmentStorageOptions.SectionName)
            .Get<AttachmentStorageOptions>()
            ?? new AttachmentStorageOptions();
        services.AddSingleton(Options.Create(attachmentOptions));
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();
        services.AddSingleton<IMalwareScanner, DisabledMalwareScanner>();
        services.AddScoped<IAttachmentMalwareScanService, AttachmentMalwareScanService>();

        SmtpEmailOptions smtpOptions = configuration
            .GetSection(SmtpEmailOptions.SectionName)
            .Get<SmtpEmailOptions>()
            ?? new SmtpEmailOptions();
        services.AddSingleton(Options.Create(smtpOptions));
        services.AddSingleton<IEmailSender, NullEmailSender>();

        // Integration adapters — all disabled by default; production connections require QEC authorization.
        IntegrationOptions integrationOptions = configuration
            .GetSection(IntegrationOptions.SectionName)
            .Get<IntegrationOptions>()
            ?? new IntegrationOptions();
        services.AddSingleton(Options.Create(integrationOptions));
        services.AddSingleton<IVeeamClient, DisabledVeeamClient>();
        services.AddSingleton<ISonicWallCaptureClient, DisabledSonicWallClient>();
        services.AddSingleton<ISynologyMonitor, DisabledSynologyMonitor>();

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

        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
    }
}
