using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Evidence.Domain;
using Qec.Itmg.Evidence.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Domain;

namespace Qec.Itmg.Host.Evidence;

public static class EvidenceEndpoints
{
    public const string EvidenceRead = "evidence.read";
    public const string EvidenceUpload = "evidence.upload";
    public const string EvidenceAccept = "evidence.accept";
    public const string EvidenceExport = "evidence.export";

    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapCrud(endpoints);
        MapWorkflow(endpoints);
        MapLinks(endpoints);
        MapPromote(endpoints);
        MapExport(endpoints);
        return endpoints;
    }

    private static void MapCrud(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/evidence").RequirePermission(EvidenceRead);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? status, string? type, string? source,
            string? classification, Guid? ownerUserId, bool? expiredOnly, bool? expiringSoonOnly,
            ClaimsPrincipal principal, ICurrentUserService currentUser, EvidenceService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            bool confidential = Can(session, EvidenceAccept) || Can(session, EvidenceExport) || Can(session, EvidenceUpload);
            return Results.Ok(await svc.ListAsync(
                page ?? 1, pageSize ?? 25, search, ParseEnum<EvidenceStatus>(status), ParseEnum<EvidenceType>(type),
                ParseEnum<EvidenceSourceType>(source), ParseEnum<EvidenceClassification>(classification), ownerUserId,
                expiredOnly == true, expiringSoonOnly == true, confidential, ct));
        });
        read.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, EvidenceService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            bool confidential = Can(session, EvidenceAccept) || Can(session, EvidenceExport) || Can(session, EvidenceUpload);
            EvidenceDto? item = await svc.GetAsync(id, confidential, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/{id:guid}/versions", async (Guid id, EvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListVersionsAsync(id, ct)));
        read.MapGet("/{id:guid}/links", async (Guid id, EvidenceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListLinksAsync(id, ct)));
        read.MapGet("/linked/{targetType}/{targetId:guid}", async (
            string targetType, Guid targetId, ClaimsPrincipal principal, ICurrentUserService currentUser,
            EvidenceService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(targetType, true, out EvidenceLinkTargetType parsed))
                return Validation("Valid targetType required.");
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            bool confidential = Can(session, EvidenceAccept) || Can(session, EvidenceExport) || Can(session, EvidenceUpload);
            return Results.Ok(await svc.ListLinkedToAsync(parsed, targetId, confidential, ct));
        });

        endpoints.MapPost("/api/v1/evidence", async (
            CreateEvidenceRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            EvidenceService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.SourceType ?? "Manual", true, out EvidenceSourceType source))
                return Validation("Valid sourceType required.");
            if (!Enum.TryParse(req.EvidenceType, true, out EvidenceType type))
                return Validation("Valid evidenceType required.");
            if (!Enum.TryParse(req.Classification ?? "Internal", true, out EvidenceClassification classification))
                return Validation("Valid classification required.");
            try
            {
                EvidenceDto created = await svc.CreateAsync(
                    req.Title, req.OwnerUserId ?? session.Id, source, type, classification,
                    req.CapturedAtUtc, req.Description, req.SourceRecordId, req.ValidFrom, req.ValidTo,
                    null, session.Id, req.ChangeSummary, ct);
                return Results.Created($"/api/v1/evidence/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceUpload);

        endpoints.MapPut("/api/v1/evidence/{id:guid}", async (
            Guid id, UpdateEvidenceRequest req, EvidenceService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.EvidenceType, true, out EvidenceType type))
                return Validation("Valid evidenceType required.");
            if (!Enum.TryParse(req.Classification, true, out EvidenceClassification classification))
                return Validation("Valid classification required.");
            try
            {
                return Results.Ok(await svc.UpdateAsync(
                    id, req.Title, req.Description, type, classification, req.ValidFrom, req.ValidTo, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceUpload);

        endpoints.MapPost("/api/v1/evidence/{id:guid}/attachments", async (
            Guid id, HttpRequest httpRequest, ClaimsPrincipal principal, ICurrentUserService currentUser,
            EvidenceService svc, IAttachmentStorageService attachments, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!httpRequest.HasFormContentType) return Validation("multipart/form-data required.");
            IFormFile? file = httpRequest.Form.Files.GetFile("file") ?? httpRequest.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return Validation("file is required.");
            bool supersede = string.Equals(httpRequest.Form["supersede"], "true", StringComparison.OrdinalIgnoreCase);
            try
            {
                await using Stream stream = file.OpenReadStream();
                AttachmentMetadata metadata = await attachments.StoreAsync(
                    stream, file.FileName, file.ContentType, session.Id, EvidenceService.AttachmentResourceType, id, ct);
                if (supersede)
                    await svc.AddVersionAsync(id, metadata.Id, session.Id, httpRequest.Form["changeSummary"], true, ct);
                else
                    await svc.AttachCurrentAsync(id, metadata.Id, session.Id, httpRequest.Form["changeSummary"], ct);
                return Results.Ok(new { attachmentId = metadata.Id, fileName = metadata.OriginalFileName });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceUpload);

        endpoints.MapGet("/api/v1/evidence/{id:guid}/attachments/{attachmentId:guid}/content", async (
            Guid id, Guid attachmentId, IAttachmentStorageService attachments, EvidenceService svc, CancellationToken ct) =>
        {
            EvidenceDto? item = await svc.GetAsync(id, includeConfidential: true, ct);
            if (item is null) return Results.NotFound();
            AttachmentMetadata? meta = await attachments.GetMetadataAsync(attachmentId, ct);
            if (meta is null) return Results.NotFound();
            Stream stream = await attachments.OpenReadAsync(attachmentId, ct);
            return Results.File(stream, meta.ContentType, meta.OriginalFileName);
        }).RequirePermission(EvidenceRead);
    }

    private static void MapWorkflow(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/evidence/{id:guid}/submit", async (Guid id, EvidenceService svc, CancellationToken ct) =>
        {
            try { await svc.SubmitAsync(id, ct); return Results.Ok(await svc.GetAsync(id, true, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceUpload);

        endpoints.MapPost("/api/v1/evidence/{id:guid}/return", async (Guid id, EvidenceService svc, CancellationToken ct) =>
        {
            try { await svc.ReturnToDraftAsync(id, ct); return Results.Ok(await svc.GetAsync(id, true, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceAccept);

        endpoints.MapPost("/api/v1/evidence/{id:guid}/accept", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, EvidenceService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try { await svc.AcceptAsync(id, session.Id, ct); return Results.Ok(await svc.GetAsync(id, true, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceAccept);

        endpoints.MapPost("/api/v1/evidence/{id:guid}/withdraw", async (
            Guid id, WithdrawRequest req, EvidenceService svc, CancellationToken ct) =>
        {
            try { await svc.WithdrawAsync(id, req.Reason, ct); return Results.Ok(await svc.GetAsync(id, true, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceAccept);
    }

    private static void MapLinks(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/evidence/{id:guid}/links", async (
            Guid id, LinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            EvidenceService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out EvidenceLinkTargetType type))
                return Validation("Valid targetType required.");
            try { await svc.LinkAsync(id, type, req.TargetId, session.Id, ct); return Results.NoContent(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceUpload);

        endpoints.MapDelete("/api/v1/evidence/{id:guid}/links/{linkId:guid}", async (
            Guid id, Guid linkId, EvidenceService svc, CancellationToken ct) =>
        {
            await svc.UnlinkAsync(id, linkId, ct);
            return Results.NoContent();
        }).RequirePermission(EvidenceUpload);
    }

    private static void MapPromote(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/evidence/promote", async (
            PromoteRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            EvidenceService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.SourceType, true, out EvidenceSourceType source))
                return Validation("Valid sourceType required.");
            if (!Enum.TryParse(req.EvidenceType ?? "Document", true, out EvidenceType type))
                return Validation("Valid evidenceType required.");
            if (!Enum.TryParse(req.Classification ?? "Internal", true, out EvidenceClassification classification))
                return Validation("Valid classification required.");

            EvidenceLinkTargetType? autoLink = source switch
            {
                EvidenceSourceType.Change => EvidenceLinkTargetType.ChangeRequest,
                EvidenceSourceType.BackupRestore or EvidenceSourceType.DrTest => EvidenceLinkTargetType.RestoreTest,
                EvidenceSourceType.AccessReview => EvidenceLinkTargetType.AccessReviewCampaign,
                EvidenceSourceType.Ticket => EvidenceLinkTargetType.Ticket,
                _ => null,
            };

            try
            {
                EvidenceDto created = await svc.PromoteAsync(
                    req.Title, source, req.SourceRecordId, req.OwnerUserId ?? session.Id, type, classification,
                    req.Description, req.ValidFrom, req.ValidTo, req.AttachmentId, session.Id, autoLink, ct);
                return Results.Created($"/api/v1/evidence/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceUpload);
    }

    private static void MapExport(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/evidence/export", async (
            ExportRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            EvidenceExportService export, IAttachmentStorageService attachments, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                EvidenceExportResult result = await export.ExportAsync(
                    req.EvidenceIds,
                    session.Id,
                    async attachmentId =>
                    {
                        AttachmentMetadata? meta = await attachments.GetMetadataAsync(attachmentId, ct);
                        if (meta is null) return null;
                        Stream stream = await attachments.OpenReadAsync(attachmentId, ct);
                        return (stream, meta.OriginalFileName, meta.ContentType);
                    },
                    includeConfidential: true,
                    ct);
                return Results.File(result.ZipBytes, "application/zip", result.FileName);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceExport);
    }

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record CreateEvidenceRequest(
        string Title, string? Description, string? SourceType, string EvidenceType, string? Classification,
        Guid? OwnerUserId, Guid? SourceRecordId, DateTimeOffset? CapturedAtUtc,
        DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, string? ChangeSummary);
    private sealed record UpdateEvidenceRequest(
        string Title, string? Description, string EvidenceType, string Classification,
        DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo);
    private sealed record WithdrawRequest(string Reason);
    private sealed record LinkRequest(string TargetType, Guid TargetId);
    private sealed record PromoteRequest(
        string Title, string SourceType, Guid SourceRecordId, string? EvidenceType, string? Classification,
        Guid? OwnerUserId, string? Description, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, Guid? AttachmentId);
    private sealed record ExportRequest(List<Guid> EvidenceIds);
}

public sealed class EvidenceExpiryJob(EvidenceService evidence)
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        evidence.MarkExpiredJobAsync(cancellationToken);
}
