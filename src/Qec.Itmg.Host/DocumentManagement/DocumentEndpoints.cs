using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
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
                    req.EffectiveDate, req.ReviewDate, req.RequiresAcknowledgement,
                    req.RequireReAcknowledgement, ct));
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
        read.MapGet("/workspace-summary", async (DocumentService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPolicyWorkspaceSummaryAsync(ct)));
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
                    req.RequiresAcknowledgement ?? true, session.Id, req.ChangeSummary, ct,
                    contentText: req.ContentText,
                    reviewerUserId: req.ReviewerUserId,
                    publisherUserId: req.PublisherUserId);
                return Results.Created($"/api/v1/policies/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyManage);

        endpoints.MapPut("/api/v1/policies/{id:guid}", async (
            Guid id, UpdateDocumentRequest req, DocumentService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Classification, true, out DocumentClassification classification))
                return Validation("Valid classification required.");
            try
            {
                return Results.Ok(await svc.UpdateMetadataAsync(
                    id, req.Title, req.OwnerUserId, req.DesignatedApproverUserId, classification,
                    req.EffectiveDate, req.ReviewDate, req.RequiresAcknowledgement, req.RequireReAcknowledgement, ct,
                    reviewerUserId: req.ReviewerUserId,
                    publisherUserId: req.PublisherUserId,
                    contentText: req.ContentText));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyManage);

        endpoints.MapPost("/api/v1/policies/{id:guid}/responsibilities", async (
            Guid id, AssignPolicyResponsibilitiesRequest req, ClaimsPrincipal principal,
            ICurrentUserService currentUser, DocumentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                return Results.Ok(await svc.AssignWorkflowResponsibilitiesAsync(
                    id,
                    session.Id,
                    req.OwnerUserId,
                    req.ReviewerUserId,
                    req.DesignatedApproverUserId,
                    req.PublisherUserId,
                    req.AssignAllToMe == true,
                    ct));
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
            Guid id, AcknowledgePolicyRequest? body, HttpContext http, ClaimsPrincipal principal,
            ICurrentUserService currentUser, PolicyAcknowledgementService ackSvc,
            DocumentNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                string? ua = http.Request.Headers.UserAgent.ToString();
                PolicyAcknowledgementDto ack = await ackSvc.AcknowledgeAsync(
                    id, session.Id, body?.AcceptedStatement == true, ip, ua, ct);
                await notifications.NotifyAcknowledgedAsync(ack, ct);
                return Results.Ok(ack);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyAcknowledge);

        endpoints.MapPost("/api/v1/policies/{id:guid}/assign", async (
            Guid id, AssignPolicyRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            PolicyAcknowledgementService ackSvc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(req.Scope, true, out PolicyAssignmentScope scope))
                return Validation("Scope must be AllEmployees or SpecificUser.");
            try
            {
                PolicyAssignmentResultDto result = await ackSvc.AssignPublishedVersionAsync(
                    id, scope, session.Id, req.UserIds, req.DueAtUtc, req.IsRequired ?? true, ct);
                await notifications.NotifyPolicyAssignedAsync(id, scope, req.UserIds, req.DueAtUtc, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyManage);

        endpoints.MapGet("/api/v1/policies/{id:guid}/acknowledgements/stats", async (
            Guid id, PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            try { return Results.Ok(await ackSvc.GetVersionStatsAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyRead);

        endpoints.MapGet("/api/v1/policies/{id:guid}/acknowledgements", async (
            Guid id, PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            try { return Results.Ok(await ackSvc.ListEmployeeStatusAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyRead);

        endpoints.MapGet("/api/v1/policies/{id:guid}/acknowledgements/export.csv", async (
            Guid id, PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            try
            {
                string csv = await ackSvc.ExportCsvAsync(id, ct);
                return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"policy-acknowledgements-{id:N}.csv");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(PolicyManage);
    }

    private static void MapMePolicies(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/policies/outstanding", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser, PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await ackSvc.ListEmployeePoliciesAsync(session.Id, "outstanding", ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/policies", async (
            string? filter, ClaimsPrincipal principal, ICurrentUserService currentUser,
            PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await ackSvc.ListEmployeePoliciesAsync(session.Id, filter ?? "outstanding", ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/policies/summary", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser, PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            EmployeePolicySummaryDto summary = await ackSvc.GetEmployeeSummaryAsync(session.Id, ct);
            return Results.Ok(new AcknowledgementSummary(
                summary.Outstanding, summary.Required, summary.Required, summary.Acknowledged, summary.Overdue));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/policies/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            EmployeePolicyItemDto? item = await ackSvc.GetEmployeePolicyAsync(session.Id, id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/policies/{id:guid}/acknowledge", async (
            Guid id, AcknowledgePolicyRequest? body, HttpContext http, ClaimsPrincipal principal,
            ICurrentUserService currentUser, PolicyAcknowledgementService ackSvc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                string? ua = http.Request.Headers.UserAgent.ToString();
                return Results.Ok(await ackSvc.AcknowledgeAsync(
                    id, session.Id, body?.AcceptedStatement == true, ip, ua, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequireAuthorization();
    }

    private static void MapWorkflow(IEndpointRouteBuilder endpoints, string prefix, string managePerm, string approvePerm)
    {
        endpoints.MapPost($"{prefix}/{{id:guid}}/submit", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                DocumentDto updated = await svc.SubmitForReviewAsync(id, session.Id, ct);
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
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            DocumentService svc, DocumentNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                DocumentDto updated = await svc.PublishAsync(id, session.Id, ct);
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

public sealed class DocumentNotificationService(
    INotificationService notifications,
    IEmailQueue emailQueue,
    IdentityDbContext identityDb,
    PolicyAcknowledgementService ackSvc,
    DocumentService documents,
    ILogger<DocumentNotificationService> logger)
{
    public const string ResourceType = "ManagedDocument";

    public async Task NotifyReviewRequestedAsync(DocumentDto doc, CancellationToken ct)
    {
        HashSet<Guid> recipients = [];
        if (doc.ReviewerUserId is Guid reviewer) recipients.Add(reviewer);
        if (doc.DesignatedApproverUserId is Guid approver) recipients.Add(approver);
        foreach (Guid recipient in recipients)
        {
            await notifications.CreateAsync(
                recipient, "document.review_requested", NotificationSeverity.Warning,
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

    public async Task NotifyPolicyAssignedAsync(
        Guid documentId,
        PolicyAssignmentScope scope,
        Guid[]? userIds,
        DateTimeOffset? dueAtUtc,
        CancellationToken ct)
    {
        DocumentDto? doc = await documents.GetAsync(documentId, includeConfidential: true, allowUnpublished: true, ct);
        if (doc is null || doc.CurrentVersionId is null) return;

        IReadOnlyList<Guid> recipients = scope == PolicyAssignmentScope.AllEmployees
            ? await ackSvc.ResolveAssigneeUserIdsAsync(doc.CurrentVersionId.Value, ct)
            : (userIds ?? []).Distinct().ToList();

        string dueText = dueAtUtc is DateTimeOffset d
            ? $" Please complete by {d:u}."
            : string.Empty;
        string actionUrl = $"/employee/policies/{documentId}";
        string subject = $"QEC Policy Acknowledgement Required: {doc.Title}";
        string body =
            $"A published QEC policy requires your acknowledgement.\n\n" +
            $"Policy: {doc.Title}\n" +
            $"Number: {doc.DocumentNumber}\n" +
            $"Version: {doc.CurrentVersionNumber}\n" +
            $"Why: You must read and acknowledge this policy for your role.{dueText}\n\n" +
            "Review and acknowledge the policy in ITMG.";

        foreach (Guid userId in recipients)
        {
            await NotifyEmployeeAsync(
                userId,
                "policy.assigned",
                NotificationSeverity.Warning,
                subject,
                body,
                documentId,
                actionUrl,
                ct);
        }
    }

    public async Task NotifyPolicyReminderAsync(PolicyAckReminderCandidate candidate, CancellationToken ct)
    {
        string actionUrl = $"/employee/policies/{candidate.ManagedDocumentId}";
        string type = candidate.ReminderKind switch
        {
            PolicyAcknowledgementService.ReminderOverdue => "policy.overdue",
            PolicyAcknowledgementService.ReminderDue1 => "policy.due_soon",
            _ => "policy.due_soon",
        };
        string title = candidate.ReminderKind == PolicyAcknowledgementService.ReminderOverdue
            ? $"Policy acknowledgement overdue: {candidate.Title}"
            : $"Policy acknowledgement due soon: {candidate.Title}";
        string message = candidate.DueAtUtc is DateTimeOffset due
            ? $"\"{candidate.Title}\" (v{candidate.VersionNumber}) is due {due:u}."
            : $"\"{candidate.Title}\" (v{candidate.VersionNumber}) still needs acknowledgement.";
        await NotifyEmployeeAsync(
            candidate.UserId, type,
            candidate.ReminderKind == PolicyAcknowledgementService.ReminderOverdue
                ? NotificationSeverity.Warning
                : NotificationSeverity.Info,
            title, message, candidate.ManagedDocumentId, actionUrl, ct);
    }

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

    private async Task NotifyEmployeeAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        Guid documentId,
        string actionUrl,
        CancellationToken ct)
    {
        try
        {
            await notifications.CreateAsync(
                recipientUserId, type, severity, title, message, ResourceType, documentId, actionUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed policy notification {Type} for {UserId}", type, recipientUserId);
            return;
        }

        try
        {
            string? email = await identityDb.Users.AsNoTracking()
                .Where(user => user.Id == recipientUserId && user.Status == UserStatus.Active)
                .Select(user => user.Upn)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
                return;
            emailQueue.Enqueue(new EmailMessage
            {
                To = email,
                Subject = title,
                BodyText = $"{message}\n\nOpen: {actionUrl}",
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enqueue policy email for {UserId}", recipientUserId);
        }
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

public sealed class PolicyAcknowledgementReminderJob(
    PolicyAcknowledgementService ackSvc,
    DocumentNotificationService notifications)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PolicyAckReminderCandidate> due = await ackSvc.FindReminderCandidatesAsync(cancellationToken);
        foreach (PolicyAckReminderCandidate item in due)
        {
            await notifications.NotifyPolicyReminderAsync(item, cancellationToken);
            await ackSvc.MarkReminderSentAsync(
                item.AssignmentId, item.UserId, item.DocumentVersionId, item.ReminderKind, cancellationToken);
        }

        return due.Count;
    }
}

public sealed record CreateDocumentRequest(
    string Title, string? DocumentType, string? Classification, Guid? OwnerUserId, Guid? DesignatedApproverUserId,
    DateTimeOffset? EffectiveDate, DateTimeOffset? ReviewDate, bool? RequiresAcknowledgement, string? ChangeSummary,
    string? ContentText = null, Guid? ReviewerUserId = null, Guid? PublisherUserId = null);

public sealed record UpdateDocumentRequest(
    string Title, Guid OwnerUserId, Guid? DesignatedApproverUserId, string Classification,
    DateTimeOffset? EffectiveDate, DateTimeOffset? ReviewDate, bool RequiresAcknowledgement,
    bool RequireReAcknowledgement = true,
    Guid? ReviewerUserId = null, Guid? PublisherUserId = null, string? ContentText = null);

public sealed record AssignPolicyResponsibilitiesRequest(
    Guid? OwnerUserId,
    Guid? ReviewerUserId,
    Guid? DesignatedApproverUserId,
    Guid? PublisherUserId,
    bool? AssignAllToMe);

public sealed record RevisionRequest(string? ChangeSummary);
public sealed record ReasonRequest(string? Reason);
public sealed record AcknowledgePolicyRequest(bool AcceptedStatement);
public sealed record AssignPolicyRequest(
    string Scope,
    Guid[]? UserIds,
    DateTimeOffset? DueAtUtc,
    bool? IsRequired);
