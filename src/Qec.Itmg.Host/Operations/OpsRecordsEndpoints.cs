using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Operations.Services;

namespace Qec.Itmg.Host.Operations;

public static class OpsRecordsEndpoints
{
    public const string OpsRead = "ops.read";
    public const string OpsManage = "ops.manage";
    public const string BackupManage = "backup.manage";
    public const string CertManage = "cert.manage";
    public const string PatchManage = "patch.manage";

    public static IEndpointRouteBuilder MapOpsRecordsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapBackup(endpoints);
        MapRestoreTests(endpoints);
        MapCertificates(endpoints);
        MapPatches(endpoints);
        MapJobs(endpoints);
        return endpoints;
    }

    private static void MapBackup(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/ops/backup-jobs").RequirePermission(OpsRead);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? search, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListBackupJobsAsync(page ?? 1, pageSize ?? 25, search, ct)));
        read.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            BackupJobDto? item = await svc.GetBackupJobAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder manage = endpoints.MapGroup("/api/v1/ops/backup-jobs").RequirePermission(BackupManage);
        manage.MapPost(string.Empty, async (UpsertBackupJobRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                BackupJobDto created = await svc.CreateBackupJobAsync(req.Name, req.Provider, req.ExternalJobId, req.ConfigurationItemId, ct);
                return Results.Created($"/api/v1/ops/backup-jobs/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        manage.MapPut("/{id:guid}", async (Guid id, UpsertBackupJobRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateBackupJobAsync(id, req.Name, req.Provider, req.ExternalJobId, req.ConfigurationItemId, req.IsActive ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });

        RouteGroupBuilder runsRead = endpoints.MapGroup("/api/v1/ops/backup-runs").RequirePermission(OpsRead);
        runsRead.MapGet(string.Empty, async (int? page, int? pageSize, Guid? backupJobId, string? status, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListBackupRunsAsync(page ?? 1, pageSize ?? 25, backupJobId, status, ct)));
        runsRead.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            BackupRunDto? item = await svc.GetBackupRunAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder runsManage = endpoints.MapGroup("/api/v1/ops/backup-runs").RequirePermission(BackupManage);
        runsManage.MapPost(string.Empty, async (UpsertBackupRunRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                BackupRunDto created = await svc.CreateBackupRunAsync(
                    req.BackupJobId, req.StartedAtUtc ?? DateTimeOffset.UtcNow, req.Status ?? "Running",
                    req.Summary, req.ExternalReference, req.CompletedAtUtc, ct);
                return Results.Created($"/api/v1/ops/backup-runs/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        runsManage.MapPut("/{id:guid}", async (Guid id, UpsertBackupRunRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateBackupRunAsync(id, req.Status ?? "Running", req.CompletedAtUtc, req.Summary, req.ExternalReference, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
    }

    private static void MapRestoreTests(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/ops/restore-tests").RequirePermission(OpsRead);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? result, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRestoreTestsAsync(page ?? 1, pageSize ?? 25, result, ct)));
        read.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            RestoreTestDto? item = await svc.GetRestoreTestAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder manage = endpoints.MapGroup("/api/v1/ops/restore-tests").RequirePermission(BackupManage);
        manage.MapPost(string.Empty, async (UpsertRestoreTestRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                RestoreTestDto created = await svc.CreateRestoreTestAsync(req.BackupJobId, req.ConfigurationItemId, req.ScheduledAtUtc, req.Notes, ct);
                return Results.Created($"/api/v1/ops/restore-tests/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        manage.MapPut("/{id:guid}", async (Guid id, UpsertRestoreTestRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateRestoreTestAsync(
                    id, req.BackupJobId, req.ConfigurationItemId, req.ScheduledAtUtc, req.PerformedAtUtc,
                    req.Result ?? "Pending", req.PerformedByUserId, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
    }

    private static void MapCertificates(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/ops/certificates").RequirePermission(OpsRead);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? search, bool? activeOnly, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCertificatesAsync(page ?? 1, pageSize ?? 25, search, activeOnly, ct)));
        read.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            CertificateDto? item = await svc.GetCertificateAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder manage = endpoints.MapGroup("/api/v1/ops/certificates").RequirePermission(CertManage);
        manage.MapPost(string.Empty, async (UpsertCertificateRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                CertificateDto created = await svc.CreateCertificateAsync(
                    req.Name, req.ExpiresAtUtc, req.ConfigurationItemId, req.Subject, req.Issuer, req.Thumbprint, req.OwnerUserId, ct);
                return Results.Created($"/api/v1/ops/certificates/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        manage.MapPut("/{id:guid}", async (Guid id, UpsertCertificateRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateCertificateAsync(
                    id, req.Name, req.ExpiresAtUtc, req.ConfigurationItemId, req.Subject, req.Issuer, req.Thumbprint, req.OwnerUserId, req.IsActive ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
    }

    private static void MapPatches(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder baselinesRead = endpoints.MapGroup("/api/v1/ops/patch-baselines").RequirePermission(OpsRead);
        baselinesRead.MapGet(string.Empty, async (int? page, int? pageSize, string? search, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPatchBaselinesAsync(page ?? 1, pageSize ?? 25, search, ct)));
        baselinesRead.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            PatchBaselineDto? item = await svc.GetPatchBaselineAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder baselinesManage = endpoints.MapGroup("/api/v1/ops/patch-baselines").RequirePermission(PatchManage);
        baselinesManage.MapPost(string.Empty, async (UpsertPatchBaselineRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                PatchBaselineDto created = await svc.CreatePatchBaselineAsync(req.Name, req.Description, req.Version, ct);
                return Results.Created($"/api/v1/ops/patch-baselines/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        baselinesManage.MapPut("/{id:guid}", async (Guid id, UpsertPatchBaselineRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdatePatchBaselineAsync(id, req.Name, req.Description, req.Version, req.IsActive ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });

        RouteGroupBuilder depRead = endpoints.MapGroup("/api/v1/ops/patch-deployments").RequirePermission(OpsRead);
        depRead.MapGet(string.Empty, async (int? page, int? pageSize, Guid? configurationItemId, string? status, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPatchDeploymentsAsync(page ?? 1, pageSize ?? 25, configurationItemId, status, ct)));
        depRead.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            PatchDeploymentDto? item = await svc.GetPatchDeploymentAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder depManage = endpoints.MapGroup("/api/v1/ops/patch-deployments").RequirePermission(PatchManage);
        depManage.MapPost(string.Empty, async (UpsertPatchDeploymentRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                PatchDeploymentDto created = await svc.CreatePatchDeploymentAsync(
                    req.ConfigurationItemId, req.PatchBaselineId, req.ExternalReference, req.ScheduledAtUtc, req.Summary, ct);
                return Results.Created($"/api/v1/ops/patch-deployments/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        depManage.MapPut("/{id:guid}", async (Guid id, UpsertPatchDeploymentRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdatePatchDeploymentAsync(
                    id, req.PatchBaselineId, req.ConfigurationItemId, req.ExternalReference, req.Status ?? "Planned",
                    req.ScheduledAtUtc, req.StartedAtUtc, req.CompletedAtUtc, req.Summary, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
    }

    private static void MapJobs(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/ops/jobs").RequirePermission(OpsRead);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? search, OpsRecordsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListScheduledJobsAsync(page ?? 1, pageSize ?? 25, search, ct)));
        read.MapGet("/{id:guid}", async (Guid id, OpsRecordsService svc, CancellationToken ct) =>
        {
            ScheduledJobDto? item = await svc.GetScheduledJobAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        RouteGroupBuilder manage = endpoints.MapGroup("/api/v1/ops/jobs").RequirePermission(OpsManage);
        manage.MapPost(string.Empty, async (UpsertScheduledJobRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                ScheduledJobDto created = await svc.CreateScheduledJobAsync(
                    req.Name, req.Provider, req.ExternalJobId, req.ConfigurationItemId, req.ScheduleDescription, req.NextRunAtUtc, ct);
                return Results.Created($"/api/v1/ops/jobs/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
        manage.MapPut("/{id:guid}", async (Guid id, UpsertScheduledJobRequest req, OpsRecordsService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateScheduledJobAsync(
                    id, req.Name, req.Provider, req.ExternalJobId, req.ConfigurationItemId, req.ScheduleDescription,
                    req.IsActive ?? true, req.LastRunAtUtc, req.LastResult ?? "Unknown", req.NextRunAtUtc, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        });
    }

    private static IResult FromEx(Exception ex)
    {
        if (ex is ArgumentException)
            return Results.Json(new { error = new { code = "validation_error", message = ex.Message } }, statusCode: StatusCodes.Status400BadRequest);
        string message = ex.Message;
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = new { code = "not_found", message } }, statusCode: StatusCodes.Status404NotFound);
        return Results.Json(new { error = new { code = "invalid_operation", message } }, statusCode: StatusCodes.Status400BadRequest);
    }
}

public sealed record UpsertBackupJobRequest(string Name, string Provider, string? ExternalJobId, Guid? ConfigurationItemId, bool? IsActive);
public sealed record UpsertBackupRunRequest(Guid BackupJobId, DateTimeOffset? StartedAtUtc, string? Status, string? Summary, string? ExternalReference, DateTimeOffset? CompletedAtUtc);
public sealed record UpsertRestoreTestRequest(Guid? BackupJobId, Guid? ConfigurationItemId, DateTimeOffset? ScheduledAtUtc, DateTimeOffset? PerformedAtUtc, string? Result, Guid? PerformedByUserId, string? Notes);
public sealed record UpsertCertificateRequest(string Name, DateTimeOffset ExpiresAtUtc, Guid? ConfigurationItemId, string? Subject, string? Issuer, string? Thumbprint, Guid? OwnerUserId, bool? IsActive);
public sealed record UpsertPatchBaselineRequest(string Name, string? Description, string? Version, bool? IsActive);
public sealed record UpsertPatchDeploymentRequest(Guid ConfigurationItemId, Guid? PatchBaselineId, string? ExternalReference, string? Status, DateTimeOffset? ScheduledAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? Summary);
public sealed record UpsertScheduledJobRequest(string Name, string? Provider, string? ExternalJobId, Guid? ConfigurationItemId, string? ScheduleDescription, bool? IsActive, DateTimeOffset? LastRunAtUtc, string? LastResult, DateTimeOffset? NextRunAtUtc);
