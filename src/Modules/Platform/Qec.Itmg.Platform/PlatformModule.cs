using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.BuildingBlocks.Persistence;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Integrations;
using Qec.Itmg.Contracts.Modules;
using Qec.Itmg.Contracts.Numbering;
using Qec.Itmg.Contracts.Secrets;
using Qec.Itmg.Contracts.Security;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Audit;
using Qec.Itmg.Platform.Comments;
using Qec.Itmg.Platform.Integrations;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.Platform.NumberSequence;
using Qec.Itmg.Platform.Persistence;
using Qec.Itmg.Platform.Workflow;

namespace Qec.Itmg.Platform;

/// <summary>
/// Platform module composition: shared platform services, audit persistence, and integration adapters
/// (real adapter code; runtime disabled by default until explicitly configured).
/// </summary>
public sealed class PlatformModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();

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
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<GraphMailSender>();
        services.AddSingleton<IEmailSender, ConfigurableEmailSender>();
        services.AddSingleton<IEmailQueue, InlineEmailQueue>();
        services.AddSingleton<MailIntegrationReadiness>();

        IntegrationOptions integrationOptions = configuration
            .GetSection(IntegrationOptions.SectionName)
            .Get<IntegrationOptions>()
            ?? new IntegrationOptions();
        services.AddSingleton(Options.Create(integrationOptions));
        services.AddSingleton<IntegrationHealthState>();

        services.AddHttpClient("integrations-veeam");
        services.AddHttpClient("integrations-synology");
        services.AddHttpClient("integrations-sonicwall");
        services.AddHttpClient("integrations-directory");
        services.AddHttpClient("integrations-virtualization");
        services.AddHttpClient("integrations-vulnscanner");
        services.AddHttpClient("integrations-mail");
        services.AddHttpClient("integrations-siem");

        services.AddSingleton<IVeeamClient, VeeamHttpClient>();
        services.AddSingleton<ISonicWallCaptureClient, SonicWallHttpClient>();
        services.AddSingleton<ISynologyMonitor, SynologyHttpMonitor>();
        services.AddSingleton<IDirectorySyncClient, DirectoryHttpSyncClient>();
        services.AddSingleton<IVirtualizationEnrichmentClient, VirtualizationHttpClient>();
        services.AddScoped<IVulnerabilityScannerIngestClient, VulnerabilityScannerHttpClient>();
        services.AddScoped<VulnerabilityScannerHttpClient>();
        services.AddScoped<ISiemPublisher, HttpsSiemPublisher>();
        services.AddScoped<IIntegrationWebhookProcessor, IntegrationWebhookProcessor>();
        services.AddScoped<IntegrationRunService>();
        services.AddScoped<IntegrationSyncCoordinator>();

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
        services.AddScoped<INotificationService, NotificationService>();
    }
}
