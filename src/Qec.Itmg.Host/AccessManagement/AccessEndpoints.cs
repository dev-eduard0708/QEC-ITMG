using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;

namespace Qec.Itmg.Host.AccessManagement;

public static class AccessEndpoints
{
    public const string AccessRequest = "access.request";
    public const string AccessApprove = "access.approve";
    public const string AccessFulfill = "access.fulfill";
    public const string AccessReview = "access.review";
    public const string AccessPrivilegedManage = "access.privileged.manage";
    public const string SodManage = "sod.manage";

    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapCases(endpoints);
        MapReviews(endpoints);
        MapAccounts(endpoints);
        MapSod(endpoints);
        return endpoints;
    }

    private static void MapCases(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/access/cases").RequirePermission(AccessRequest);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? search, string? type, string? status, AccessCaseService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(page ?? 1, pageSize ?? 25, search, ParseEnum<AccessCaseType>(type), ParseEnum<AccessCaseStatus>(status), ct)));
        read.MapGet("/{id:guid}", async (Guid id, AccessCaseService svc, CancellationToken ct) =>
        {
            AccessCaseDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/{id:guid}/items", async (Guid id, AccessCaseService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListItemsAsync(id, ct)));
        read.MapGet("/{id:guid}/existing-access", async (Guid id, AccessCaseService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListExistingAccessAsync(id, ct)));
        read.MapGet("/{id:guid}/exceptions", async (Guid id, AccessCaseService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListExceptionsAsync(id, ct)));
        read.MapGet("/{id:guid}/sod-violations", async (Guid id, AccessCaseService svc, CancellationToken ct) =>
            Results.Ok(await svc.DetectSodViolationsAsync(id, ct)));
        read.MapGet("/{id:guid}/evidence", async (Guid id, AccessEvidenceService evidence, CancellationToken ct) =>
        {
            try
            {
                AccessEvidenceProjection? item = await evidence.PrepareCaseEvidenceAsync(id, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            }
            catch (InvalidOperationException ex) { return FromEx(ex); }
        });

        endpoints.MapPost("/api/v1/access/cases", async (
            CreateAccessCaseRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(req.Type, true, out AccessCaseType type))
                return Validation("A valid type is required.");
            try
            {
                AccessCaseDto created = await svc.CreateAsync(
                    type, session.Id, req.Reason, req.SubjectUserId, req.SubjectName, req.SubjectEmail,
                    req.DepartmentId, req.ManagerUserId, req.DesignatedApproverUserId, req.EffectiveAtUtc, ct);
                return Results.Created($"/api/v1/access/cases/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPut("/api/v1/access/cases/{id:guid}", async (
            Guid id, UpdateAccessCaseRequest req, AccessCaseService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateDraftAsync(
                    id, req.Reason, req.SubjectUserId, req.SubjectName, req.SubjectEmail,
                    req.DepartmentId, req.ManagerUserId, req.DesignatedApproverUserId, req.EffectiveAtUtc, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        MapCaseAction("/api/v1/access/cases/{id:guid}/submit", AccessRequest, async (id, _, svc, _, ct) =>
            Results.Ok(await svc.SubmitAsync(id, ct)));
        MapCaseAction("/api/v1/access/cases/{id:guid}/start-approval", AccessApprove, async (id, _, svc, _, ct) =>
            Results.Ok(await svc.StartApprovalAsync(id, ct)));

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/approve", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                AccessCaseDto updated = await svc.ApproveAsync(id, session.Id, ct);
                await notifications.NotifyApprovedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessApprove);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/reject", async (
            Guid id, OverrideReasonRequest? req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                AccessCaseDto updated = await svc.RejectAsync(id, session.Id, req?.Reason, ct);
                await notifications.NotifyRejectedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessApprove);

        MapCaseAction("/api/v1/access/cases/{id:guid}/start-verification", AccessFulfill, async (id, _, svc, notifications, ct) =>
        {
            AccessCaseDto updated = await svc.StartVerificationAsync(id, ct);
            await notifications.NotifyVerificationRequiredAsync(updated, ct);
            return Results.Ok(updated);
        });
        MapCaseAction("/api/v1/access/cases/{id:guid}/close", AccessFulfill, async (id, _, svc, _, ct) =>
            Results.Ok(await svc.CloseAsync(id, ct)));

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/cancel", async (
            Guid id, OverrideReasonRequest? req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool canOverride = session.Permissions.Contains(AccessPrivilegedManage);
            try
            {
                return Results.Ok(await svc.CancelAsync(id, session.Id, req?.Reason, canOverride, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/confirm-existing-access", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, AccessCaseService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                await svc.ConfirmExistingAccessAsync(id, session.Id, ct);
                return Results.Ok(await svc.GetAsync(id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/items", async (
            Guid id, AddAccessItemRequest req, AccessCaseService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Action, true, out AccessItemAction action))
                return Validation("A valid action is required.");
            try
            {
                return Results.Ok(await svc.AddItemAsync(
                    id, req.EntitlementKey, action, req.ConfigurationItemId, req.IsPrivileged, req.IsMandatory, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/items/{itemId:guid}/complete", async (
            Guid id, Guid itemId, OverrideReasonRequest? req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try { return Results.Ok(await svc.CompleteItemAsync(id, itemId, session.Id, req?.Reason, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessFulfill);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/existing-access", async (
            Guid id, AddExistingAccessRequest req, AccessCaseService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.AddExistingAccessAsync(id, req.EntitlementKey, req.ConfigurationItemId, req.AccessSummary, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/exceptions", async (
            Guid id, RecordExceptionRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(req.Type, true, out AccessCaseExceptionType type))
                return Validation("A valid exception type is required.");
            string permission = type switch
            {
                AccessCaseExceptionType.SodException => SodManage,
                _ => AccessPrivilegedManage,
            };
            if (!session.Permissions.Contains(permission))
                return Results.Json(new { error = new { code = "forbidden", message = "Missing permission for exception." } }, statusCode: 403);
            try
            {
                return Results.Ok(await svc.RecordExceptionAsync(id, type, req.Reason, session.Id, req.RelatedSodRuleId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/link-ticket", async (
            Guid id, LinkTicketRequest req, AccessCaseService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.LinkTicketAsync(id, req.TicketId, ct);
                return Results.Ok(await svc.GetAsync(id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        void MapCaseAction(
            string route,
            string permission,
            Func<Guid, CurrentUserDto, AccessCaseService, AccessNotificationService, CancellationToken, Task<IResult>> handler)
        {
            endpoints.MapPost(route, async (
                Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
                AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
            {
                CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
                if (session is null) return SessionUnavailable();
                try { return await handler(id, session, svc, notifications, ct); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
            }).RequirePermission(permission);
        }
    }

    private static void MapReviews(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/access/reviews").RequirePermission(AccessReview);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? status, AccessReviewService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCampaignsAsync(page ?? 1, pageSize ?? 25, status, ct)));
        read.MapGet("/{id:guid}", async (Guid id, AccessReviewService svc, CancellationToken ct) =>
        {
            AccessReviewCampaignDto? item = await svc.GetCampaignAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/{id:guid}/items", async (Guid id, AccessReviewService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListItemsAsync(id, ct)));
        read.MapGet("/{id:guid}/evidence", async (Guid id, AccessEvidenceService evidence, CancellationToken ct) =>
        {
            try
            {
                AccessEvidenceProjection? item = await evidence.PrepareReviewEvidenceAsync(id, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            }
            catch (InvalidOperationException ex) { return FromEx(ex); }
        });

        endpoints.MapPost("/api/v1/access/reviews", async (
            CreateReviewCampaignRequest req, AccessReviewService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Type, true, out AccessReviewType type))
                return Validation("A valid type is required.");
            try
            {
                AccessReviewCampaignDto created = await svc.CreateCampaignAsync(
                    req.Name, type, req.ReviewerUserId, req.StartsAtUtc, req.DueAtUtc, ct);
                await notifications.NotifyReviewAssignedAsync(created, ct);
                return Results.Created($"/api/v1/access/reviews/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessReview);

        endpoints.MapPost("/api/v1/access/reviews/{id:guid}/open", async (Guid id, AccessReviewService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.OpenAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessReview);

        endpoints.MapPost("/api/v1/access/reviews/{id:guid}/complete", async (Guid id, AccessReviewService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.CompleteAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessReview);

        endpoints.MapPost("/api/v1/access/reviews/{id:guid}/items", async (
            Guid id, AddReviewItemRequest req, AccessReviewService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.AddItemAsync(id, req.AccessSummary, req.SubjectUserId, req.AccountRecordId, req.ConfigurationItemId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessReview);

        endpoints.MapPost("/api/v1/access/reviews/{id:guid}/items/{itemId:guid}/decide", async (
            Guid id, Guid itemId, DecideReviewRequest req, AccessReviewService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Decision, true, out AccessReviewDecision decision))
                return Validation("A valid decision is required.");
            try { return Results.Ok(await svc.DecideAsync(id, itemId, decision, req.Comment, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessReview);
    }

    private static void MapAccounts(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/access/accounts").RequirePermission(AccessPrivilegedManage);
        read.MapGet(string.Empty, async (int? page, int? pageSize, string? search, string? type, ManagedAccountService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(page ?? 1, pageSize ?? 25, search, type, ct)));
        read.MapGet("/{id:guid}", async (Guid id, ManagedAccountService svc, CancellationToken ct) =>
        {
            ManagedAccountDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        endpoints.MapPost("/api/v1/access/accounts", async (UpsertManagedAccountRequest req, ManagedAccountService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Type, true, out ManagedAccountType type))
                return Validation("A valid type is required.");
            try
            {
                ManagedAccountDto created = await svc.CreateAsync(req.AccountName, type, req.Purpose, req.ConfigurationItemId, req.OwnerUserId, ct);
                return Results.Created($"/api/v1/access/accounts/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessPrivilegedManage);

        endpoints.MapPut("/api/v1/access/accounts/{id:guid}", async (Guid id, UpsertManagedAccountRequest req, ManagedAccountService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status ?? "Active", true, out ManagedAccountStatus status))
                return Validation("A valid status is required.");
            try
            {
                return Results.Ok(await svc.UpdateAsync(
                    id, req.AccountName, req.Purpose, req.ConfigurationItemId, req.OwnerUserId, status, req.LastReviewedAtUtc, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessPrivilegedManage);
    }

    private static void MapSod(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/access/sod").RequirePermission(SodManage);
        read.MapGet(string.Empty, async (int? page, int? pageSize, bool? activeOnly, SodService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(page ?? 1, pageSize ?? 25, activeOnly, ct)));
        read.MapGet("/{id:guid}", async (Guid id, SodService svc, CancellationToken ct) =>
        {
            SodRuleDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        endpoints.MapPost("/api/v1/access/sod", async (UpsertSodRuleRequest req, SodService svc, CancellationToken ct) =>
        {
            try
            {
                SodRuleDto created = await svc.CreateAsync(
                    req.Name, req.LeftEntitlementKey, req.RightEntitlementKey, req.Severity,
                    req.ApplicationConfigurationItemId, req.Description, ct);
                return Results.Created($"/api/v1/access/sod/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SodManage);

        endpoints.MapPut("/api/v1/access/sod/{id:guid}", async (Guid id, UpsertSodRuleRequest req, SodService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateAsync(
                    id, req.Name, req.LeftEntitlementKey, req.RightEntitlementKey, req.Severity,
                    req.IsActive ?? true, req.ApplicationConfigurationItemId, req.Description, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SodManage);
    }

    private static IResult SessionUnavailable() =>
        Results.Json(new { error = new { code = "session_unavailable", message = "No active ITMG user session." } }, statusCode: 403);

    private static IResult Validation(string message) =>
        Results.Json(new { error = new { code = "validation_error", message } }, statusCode: 400);

    private static IResult FromEx(Exception ex)
    {
        if (ex is ArgumentException)
            return Validation(ex.Message);
        if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = new { code = "not_found", message = ex.Message } }, statusCode: 404);
        return Results.Json(new { error = new { code = "invalid_operation", message = ex.Message } }, statusCode: 400);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.TryParse(value, true, out TEnum parsed) ? parsed : null;
}

public sealed class AccessNotificationService(INotificationService notifications)
{
    public const string ResourceType = "AccessCase";

    public Task NotifyApprovalRequestedAsync(AccessCaseDto accessCase, Guid recipientUserId, CancellationToken ct) =>
        notifications.CreateAsync(
            recipientUserId, "access.approval_requested", NotificationSeverity.Warning,
            $"Approval requested: {accessCase.CaseNumber}",
            $"Please review access case \"{accessCase.CaseNumber}\".",
            ResourceType, accessCase.Id, $"/it/access/{accessCase.Id}", ct);

    public Task NotifyApprovedAsync(AccessCaseDto accessCase, CancellationToken ct) =>
        notifications.CreateAsync(
            accessCase.RequesterUserId, "access.approved", NotificationSeverity.Info,
            $"{accessCase.CaseNumber} approved",
            "Access case approved and ready for fulfillment.",
            ResourceType, accessCase.Id, $"/it/access/{accessCase.Id}", ct);

    public Task NotifyRejectedAsync(AccessCaseDto accessCase, CancellationToken ct) =>
        notifications.CreateAsync(
            accessCase.RequesterUserId, "access.rejected", NotificationSeverity.Warning,
            $"{accessCase.CaseNumber} rejected",
            "Access case was rejected.",
            ResourceType, accessCase.Id, $"/it/access/{accessCase.Id}", ct);

    public Task NotifyFulfillmentReadyAsync(AccessCaseDto accessCase, Guid recipientUserId, CancellationToken ct) =>
        notifications.CreateAsync(
            recipientUserId, "access.fulfillment_ready", NotificationSeverity.Info,
            $"Fulfillment ready: {accessCase.CaseNumber}",
            "Access case is ready for checklist fulfillment.",
            ResourceType, accessCase.Id, $"/it/access/{accessCase.Id}", ct);

    public Task NotifyVerificationRequiredAsync(AccessCaseDto accessCase, CancellationToken ct) =>
        notifications.CreateAsync(
            accessCase.RequesterUserId, "access.verification_required", NotificationSeverity.Info,
            $"Verification: {accessCase.CaseNumber}",
            "Please verify completed access work.",
            ResourceType, accessCase.Id, $"/it/access/{accessCase.Id}", ct);

    public Task NotifyReviewAssignedAsync(AccessReviewCampaignDto campaign, CancellationToken ct) =>
        notifications.CreateAsync(
            campaign.ReviewerUserId, "access.review_assigned", NotificationSeverity.Info,
            $"Review assigned: {campaign.Name}",
            $"Due {campaign.DueAtUtc:u}.",
            "AccessReview", campaign.Id, "/it/access/reviews", ct);
}

public sealed record CreateAccessCaseRequest(
    string Type, string Reason, Guid? SubjectUserId, string? SubjectName, string? SubjectEmail,
    Guid? DepartmentId, Guid? ManagerUserId, Guid? DesignatedApproverUserId, DateTimeOffset? EffectiveAtUtc);

public sealed record UpdateAccessCaseRequest(
    string Reason, Guid? SubjectUserId, string? SubjectName, string? SubjectEmail,
    Guid? DepartmentId, Guid? ManagerUserId, Guid? DesignatedApproverUserId, DateTimeOffset? EffectiveAtUtc);

public sealed record AddAccessItemRequest(
    string EntitlementKey, string Action, Guid? ConfigurationItemId, bool IsPrivileged, bool IsMandatory, string? Notes);

public sealed record AddExistingAccessRequest(string EntitlementKey, Guid? ConfigurationItemId, string? AccessSummary);
public sealed record RecordExceptionRequest(string Type, string Reason, Guid? RelatedSodRuleId);
public sealed record LinkTicketRequest(Guid TicketId);
public sealed record OverrideReasonRequest(string? Reason);
public sealed record CreateReviewCampaignRequest(string Name, string Type, Guid ReviewerUserId, DateTimeOffset StartsAtUtc, DateTimeOffset DueAtUtc);
public sealed record AddReviewItemRequest(string AccessSummary, Guid? SubjectUserId, Guid? AccountRecordId, Guid? ConfigurationItemId);
public sealed record DecideReviewRequest(string Decision, string? Comment);
public sealed record UpsertManagedAccountRequest(
    string AccountName, string Type, string Purpose, Guid? ConfigurationItemId, Guid? OwnerUserId,
    string? Status, DateTimeOffset? LastReviewedAtUtc);
public sealed record UpsertSodRuleRequest(
    string Name, string LeftEntitlementKey, string RightEntitlementKey, string Severity,
    Guid? ApplicationConfigurationItemId, string? Description, bool? IsActive);
