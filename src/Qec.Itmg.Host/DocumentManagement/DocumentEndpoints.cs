using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;

namespace Qec.Itmg.Host.DocumentManagement;

public static class DocumentEndpoints
{
    public const string DocRead = "doc.read";
    public const string DocManage = "doc.manage";
    public const string DocApprove = "doc.approve";
    public const string PolicyRead = "policy.read";
    public const string PolicyManage = "policy.manage";
    public const string PolicyApprove = "policy.approve";
    public const string PolicyAcknowledge = "policy.acknowledge";
    public const string ResourceType = "ManagedDocument";

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapDocuments(endpoints);
        MapPolicies(endpoints);
        MapMePolicies(endpoints);
        return endpoints;
    }

    private static void MapDocuments(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/documents").RequirePermission(DocRead);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? type, string? status, bool? reviewOverdueOnly,
            ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool confidential = Can(session, DocManage) || Can(session, DocApprove);
            return Results.Ok(await svc.ListAsync(
                page ?? 1, pageSize ?? 25, search, ParseEnum<DocumentType>(type), ParseEnum<DocumentStatus>(status),
                publishedOnly: false, includeConfidential: confidential, reviewOverdueOnly: reviewOverdueOnly == true, ct));
        });
        read.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool manage = Can(session, DocManage) || Can(session, DocApprove);
            DocumentDto? item = await svc.GetAsync(id, includeConfidential: manage, allowUnpublished: manage, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/{id:guid}/versions", async (Guid id, DocumentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListVersionsAsync(id, ct)));

        endpoints.MapPost("/api/v1/documents", async (
            CreateDocumentRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(req.DocumentType, true, out DocumentType type)) return Validation("Valid documentType required.");
            if (!Enum.TryParse(req.Classification ?? "Internal", true, out DocumentClassification classification))
                return Validation("Valid classification required.");
            try
            {
                DocumentDto created = await svc.CreateAsync(
                    req.Title, type, req.OwnerUserId ?? session.Id, classification, req.DesignatedApproverUserId,
                    req.EffectiveDate, req.ReviewDate, req.RequiresAcknowledgement ?? false, session.Id, req.ChangeSummary, ct);
                return Results.Created($"/api/v1/documents/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DocManage);

        endpoints.MapPut("/api/v1/documents/{id:guid}", async (
            Guid id, UpdateDocumentRequest req, DocumentService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Classification, true, out DocumentClassification classification))
                return Validation("Valid classification required.");
            try
            {
                return Results.Ok(await svc.UpdateMetadataAsync(
                    id, req.Title, req.OwnerUserId, req.DesignatedApproverUserId, classification,
                    req.EffectiveDate, req.ReviewDate, req.RequiresAcknowledgement, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DocManage);

        endpoints.MapPost("/api/v1/documents/{id:guid}/revisions", async (
            Guid id, RevisionRequest? req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try { return Results.Ok(await svc.CreateRevisionAsync(id, session.Id, req?.ChangeSummary, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DocManage);

        endpoints.MapPost("/api/v1/documents/{id:guid}/attachments", async (
            Guid id, HttpRequest httpRequest, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, IAttachmentStorageService attachments, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!httpRequest.HasFormContentType) return Validation("multipart/form-data required.");
            IFormFile? file = httpRequest.Form.Files.GetFile("file") ?? httpRequest.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return Validation("file is required.");
            try
            {
                await using Stream stream = file.OpenReadStream();
                AttachmentMetadata metadata = await attachments.StoreAsync(
                    stream, file.FileName, file.ContentType, session.Id, ResourceType, id, ct);
                await svc.AttachToCurrentVersionAsync(id, metadata.Id, ct);
                return Results.Ok(new { attachmentId = metadata.Id, fileName = metadata.OriginalFileName });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DocManage);

        endpoints.MapGet("/api/v1/documents/{id:guid}/attachments/{attachmentId:guid}/content", async (
            Guid id, Guid attachmentId, IAttachmentStorageService attachments, CancellationToken ct) =>
        {
            AttachmentMetadata? meta = await attachments.GetMetadataAsync(attachmentId, ct);
            if (meta is null || meta.ResourceId != id) return Results.NotFound();
            Stream stream = await attachments.OpenReadAsync(attachmentId, ct);
            return Results.File(stream, meta.ContentType, meta.OriginalFileName);
        }).RequirePermission(DocRead);

        MapWorkflow(endpoints, "/api/v1/documents", DocManage, DocApprove);
    }

    private static void MapPolicies(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/policies").RequirePermission(PolicyRead);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? status, bool? reviewOverdueOnly,
            ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool confidential = Can(session, PolicyManage) || Can(session, PolicyApprove) || Can(session, DocManage);
            return Results.Ok(await svc.ListAsync(
                page ?? 1, pageSize ?? 25, search, DocumentType.Policy, ParseEnum<DocumentStatus>(status),
                publishedOnly: !confidential, includeConfidential: confidential, reviewOverdueOnly: reviewOverdueOnly == true, ct));
        });
        read.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool manage = Can(session, PolicyManage) || Can(session, PolicyApprove) || Can(session, DocManage);
            DocumentDto? item = await svc.GetAsync(id, includeConfidential: manage, allowUnpublished: manage, ct);
            if (item is null || item.DocumentType != nameof(DocumentType.Policy)) return Results.NotFound();
            return Results.Ok(item);
        });
        read.MapGet("/{id:guid}/versions", async (Guid id, DocumentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListVersionsAsync(id, ct)));

        endpoints.MapPost("/api/v1/policies", async (
            CreateDocumentRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(req.Classification ?? "Internal", true, out DocumentClassification classification))
                return Validation("Valid classification required.");
            try
            {
                DocumentDto created = await svc.CreateAsync(
                    req.Title, DocumentType.Policy, req.OwnerUserId ?? session.Id, classification,
                    req.DesignatedApproverUserId, req.EffectiveDate, req.ReviewDate,
                    req.RequiresAcknowledgement ?? true, session.Id, req.ChangeSummary, ct);
                return Results.Created($"/api/v1/policies/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyManage);

        endpoints.MapPost("/api/v1/policies/seed-catalog", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            await svc.EnsureCatalogSeedAsync(session.Id, ct);
            return Results.Ok(new { seeded = true });
        }).RequirePermission(PolicyManage);

        endpoints.MapPost("/api/v1/policies/{id:guid}/revisions", async (
            Guid id, RevisionRequest? req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try { return Results.Ok(await svc.CreateRevisionAsync(id, session.Id, req?.ChangeSummary, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyManage);

        MapWorkflow(endpoints, "/api/v1/policies", PolicyManage, PolicyApprove);

        endpoints.MapPost("/api/v1/policies/{id:guid}/acknowledge", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc,
            DocumentNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                PolicyAcknowledgementDto ack = await svc.AcknowledgeAsync(id, session.Id, ct);
                await notifications.NotifyAcknowledgedAsync(ack, ct);
                return Results.Ok(ack);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyAcknowledge);
    }

    private static void MapMePolicies(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/policies/outstanding", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await svc.ListOutstandingAcknowledgementsAsync(session.Id, ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/policies/summary", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await svc.GetAcknowledgementSummaryAsync(session.Id, ct));
        }).RequireAuthorization();
    }

    private static void MapWorkflow(IEndpointRouteBuilder endpoints, string prefix, string managePerm, string approvePerm)
    {
        endpoints.MapPost($"{prefix}/{{id:guid}}/submit", async (
            Guid id, DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            try
            {
                DocumentDto updated = await svc.SubmitForReviewAsync(id, ct);
                await notifications.NotifyReviewRequestedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(managePerm);

        endpoints.MapPost($"{prefix}/{{id:guid}}/approve", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                DocumentDto updated = await svc.ApproveAsync(id, session.Id, ct);
                await notifications.NotifyApprovedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(approvePerm);

        endpoints.MapPost($"{prefix}/{{id:guid}}/return", async (
            Guid id, ReasonRequest? req, DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            try
            {
                DocumentDto updated = await svc.ReturnToDraftAsync(id, req?.Reason, ct);
                await notifications.NotifyReturnedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(approvePerm);

        endpoints.MapPost($"{prefix}/{{id:guid}}/publish", async (
            Guid id, DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            try
            {
                DocumentDto updated = await svc.PublishAsync(id, ct);
                await notifications.NotifyPublishedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(managePerm);

        endpoints.MapPost($"{prefix}/{{id:guid}}/retire", async (
            Guid id, ReasonRequest req, DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Reason)) return Validation("Retirement reason is required.");
            try
            {
                DocumentDto updated = await svc.RetireAsync(id, req.Reason, ct);
                await notifications.NotifyRetiredAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(managePerm);
    }

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));

    private static IResult SessionUnavailable() =>
        Results.Json(new { error = new { code = "session_unavailable", message = "No active ITMG user session." } }, statusCode: 403);

    private static IResult Validation(string message) =>
        Results.Json(new { error = new { code = "validation_error", message } }, statusCode: 400);

    private static IResult FromEx(Exception ex)
    {
        if (ex is ArgumentException) return Validation(ex.Message);
        if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = new { code = "not_found", message = ex.Message } }, statusCode: 404);
        return Results.Json(new { error = new { code = "invalid_operation", message = ex.Message } }, statusCode: 400);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.TryParse(value, true, out TEnum parsed) ? parsed : null;
}

public sealed class DocumentNotificationService(INotificationService notifications)
{
    public const string ResourceType = "ManagedDocument";

    public async Task NotifyReviewRequestedAsync(DocumentDto doc, CancellationToken ct)
    {
        if (doc.DesignatedApproverUserId is Guid approver)
        {
            await notifications.CreateAsync(
                approver, "document.review_requested", NotificationSeverity.Warning,
                $"Review requested: {doc.DocumentNumber}",
                $"Please review \"{doc.Title}\".",
                ResourceType, doc.Id, ActionUrl(doc), ct);
        }
    }

    public Task NotifyApprovedAsync(DocumentDto doc, CancellationToken ct) =>
        notifications.CreateAsync(
            doc.OwnerUserId, "document.approved", NotificationSeverity.Info,
            $"{doc.DocumentNumber} approved",
            $"\"{doc.Title}\" was approved.",
            ResourceType, doc.Id, ActionUrl(doc), ct);

    public Task NotifyReturnedAsync(DocumentDto doc, CancellationToken ct) =>
        notifications.CreateAsync(
            doc.OwnerUserId, "document.returned", NotificationSeverity.Warning,
            $"{doc.DocumentNumber} returned to draft",
            $"\"{doc.Title}\" was returned for changes.",
            ResourceType, doc.Id, ActionUrl(doc), ct);

    public Task NotifyPublishedAsync(DocumentDto doc, CancellationToken ct) =>
        notifications.CreateAsync(
            doc.OwnerUserId, "document.published", NotificationSeverity.Info,
            $"{doc.DocumentNumber} published",
            $"\"{doc.Title}\" is now published.",
            ResourceType, doc.Id, ActionUrl(doc), ct);

    public Task NotifyRetiredAsync(DocumentDto doc, CancellationToken ct) =>
        notifications.CreateAsync(
            doc.OwnerUserId, "document.retired", NotificationSeverity.Warning,
            $"{doc.DocumentNumber} retired",
            $"\"{doc.Title}\" was retired.",
            ResourceType, doc.Id, ActionUrl(doc), ct);

    public Task NotifyAcknowledgedAsync(PolicyAcknowledgementDto ack, CancellationToken ct) => Task.CompletedTask;

    public Task NotifyReviewDueAsync(DocumentReviewCandidate candidate, CancellationToken ct)
    {
        string title = candidate.ThresholdDays == 0
            ? $"Review overdue: {candidate.DocumentNumber}"
            : $"Review due in {candidate.DaysToReview} day(s): {candidate.DocumentNumber}";
        return notifications.CreateAsync(
            candidate.OwnerUserId,
            candidate.ThresholdDays == 0 ? "document.review_overdue" : "document.review_due",
            candidate.ThresholdDays is 0 or 1 or 7 ? NotificationSeverity.Warning : NotificationSeverity.Info,
            title,
            $"\"{candidate.Title}\" review date {candidate.ReviewDateUtc:u}.",
            ResourceType, candidate.DocumentId, $"/it/documents/{candidate.DocumentId}", ct);
    }

    private static string ActionUrl(DocumentDto doc) =>
        doc.DocumentType == nameof(DocumentType.Policy) ? $"/it/policies/{doc.Id}" : $"/it/documents/{doc.Id}";
}

public sealed class DocumentReviewReminderJob(
    DocumentReviewNotificationService review,
    DocumentNotificationService notifications)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentReviewCandidate> due = await review.FindDueNotificationsAsync(cancellationToken);
        foreach (DocumentReviewCandidate item in due)
        {
            await notifications.NotifyReviewDueAsync(item, cancellationToken);
            await review.MarkNotifiedAsync(item.DocumentId, item.ReviewDateUtc, item.ThresholdDays, cancellationToken);
        }

        return due.Count;
    }
}

public sealed record CreateDocumentRequest(
    string Title, string? DocumentType, string? Classification, Guid? OwnerUserId, Guid? DesignatedApproverUserId,
    DateTimeOffset? EffectiveDate, DateTimeOffset? ReviewDate, bool? RequiresAcknowledgement, string? ChangeSummary);

public sealed record UpdateDocumentRequest(
    string Title, Guid OwnerUserId, Guid? DesignatedApproverUserId, string Classification,
    DateTimeOffset? EffectiveDate, DateTimeOffset? ReviewDate, bool RequiresAcknowledgement);

public sealed record RevisionRequest(string? ChangeSummary);
public sealed record ReasonRequest(string? Reason);
