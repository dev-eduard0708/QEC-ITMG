using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.Contracts.Notifications;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;

namespace Qec.Itmg.Host.AccessManagement;

public static class AccessEndpoints
{
    public const string AccessRequest = "access.request";
    public const string AccessApprove = "access.approve";
    public const string AccessFulfill = "access.fulfill";
    public const string AccessConfigure = "access.configure";
    public const string AccessReview = "access.review";
    public const string AccessPrivilegedManage = "access.privileged.manage";
    public const string SodManage = "sod.manage";

    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapCases(endpoints);
        MapCategories(endpoints);
        MapEntitlements(endpoints);
        MapReviews(endpoints);
        MapAccounts(endpoints);
        MapSod(endpoints);
        return endpoints;
    }

    private static void MapEntitlements(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/access/entitlements", async (
            bool? activeOnly, string? search, AccessEntitlementService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(activeOnly ?? true, search, ct)))
            .RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/entitlements", async (
            UpsertAccessEntitlementRequest req, AccessEntitlementService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.DefaultRevokeAction, true, out AccessRevokeAction revoke))
                return Validation("A valid defaultRevokeAction is required (Remove|Disable).");
            try
            {
                AccessEntitlementDto created = await svc.CreateAsync(
                    req.Key, req.NameEn, req.NameAr, req.DescriptionEn, req.DescriptionAr,
                    revoke, req.IsPrivileged ?? false, req.IsActive ?? true, ct);
                return Results.Created($"/api/v1/access/entitlements/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessConfigure);

        endpoints.MapPut("/api/v1/access/entitlements/{id:guid}", async (
            Guid id, UpsertAccessEntitlementRequest req, AccessEntitlementService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.DefaultRevokeAction, true, out AccessRevokeAction revoke))
                return Validation("A valid defaultRevokeAction is required (Remove|Disable).");
            try
            {
                return Results.Ok(await svc.UpdateAsync(
                    id, req.NameEn, req.NameAr, req.DescriptionEn, req.DescriptionAr,
                    revoke, req.IsPrivileged ?? false, req.IsActive ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessConfigure);

        endpoints.MapGet("/api/v1/access/users/{userId:guid}/current-access", async (
            Guid userId, bool? activeOnly, UserAccessService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.GetCurrentAccessAsync(userId, ct, activeOnly ?? true)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);
    }

    private static void MapCategories(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/access/categories", async (
            bool? activeOnly, AccessCategoryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(activeOnly ?? false, ct)))
            .RequirePermission(AccessRequest);

        endpoints.MapGet("/api/v1/access/categories/{id:guid}", async (
            Guid id, AccessCategoryService svc, CancellationToken ct) =>
        {
            AccessCategoryDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/categories", async (
            UpsertAccessCategoryRequest req, AccessCategoryService svc, CancellationToken ct) =>
        {
            try
            {
                AccessCategoryDto created = await svc.CreateAsync(
                    req.Key, req.NameEn, req.NameAr, req.DescriptionEn, req.DescriptionAr,
                    req.PreferSubjectEmployeeVerification ?? true, req.IsActive ?? true, ct);
                return Results.Created($"/api/v1/access/categories/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessConfigure);

        endpoints.MapPut("/api/v1/access/categories/{id:guid}", async (
            Guid id, UpsertAccessCategoryRequest req, AccessCategoryService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateAsync(
                    id, req.NameEn, req.NameAr, req.DescriptionEn, req.DescriptionAr,
                    req.IsActive ?? true, req.PreferSubjectEmployeeVerification ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessConfigure);

        endpoints.MapPut("/api/v1/access/categories/{id:guid}/routing", async (
            Guid id, ReplaceCategoryRoutingRequest req, AccessCategoryService svc, CancellationToken ct) =>
        {
            try
            {
                Dictionary<AccessCategoryStage, IReadOnlyList<Guid>> routing = new()
                {
                    [AccessCategoryStage.Requester] = req.RequesterUserIds ?? [],
                    [AccessCategoryStage.Approver] = req.ApproverUserIds ?? [],
                    [AccessCategoryStage.Fulfiller] = req.FulfillerUserIds ?? [],
                    [AccessCategoryStage.Verifier] = req.VerifierUserIds ?? [],
                    [AccessCategoryStage.Closer] = req.CloserUserIds ?? [],
                };
                return Results.Ok(await svc.ReplaceRoutingAsync(id, routing, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessConfigure);

        endpoints.MapGet("/api/v1/access/categories/{id:guid}/entitlements", async (
            Guid id, bool? activeOnly, AccessCategoryService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.ListEntitlementsAsync(id, ct, activeOnly ?? false)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPut("/api/v1/access/categories/{id:guid}/entitlements", async (
            Guid id, ReplaceCategoryEntitlementsRequest req, AccessCategoryService svc, CancellationToken ct) =>
        {
            try
            {
                IReadOnlyList<AccessCategoryEntitlementSpec> specs = (req.Items ?? [])
                    .Select(x => new AccessCategoryEntitlementSpec(
                        x.AccessEntitlementId, x.IsDefaultForJoiner, x.SortOrder, x.IsActive))
                    .ToList();
                return Results.Ok(await svc.ReplaceEntitlementsAsync(id, specs, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessConfigure);
    }

    private static void MapCases(IEndpointRouteBuilder endpoints)
    {
        string[] caseReadPermissions =
        [
            AccessRequest, AccessApprove, AccessFulfill, AccessConfigure,
            AccessReview, AccessPrivilegedManage, SodManage,
        ];

        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/access/cases")
            .RequireAnyPermission(caseReadPermissions);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? type, string? status, string? queue,
            ClaimsPrincipal principal, ICurrentUserService currentUser, AccessCaseService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool canSeeAll = session.Permissions.Contains(AccessConfigure)
                || session.Permissions.Contains(AccessPrivilegedManage);
            return Results.Ok(await svc.ListAsync(
                page ?? 1, pageSize ?? 25, search, ParseEnum<AccessCaseType>(type), ParseEnum<AccessCaseStatus>(status), ct,
                scopedUserId: session.Id, workQueue: queue, canSeeAll: canSeeAll));
        });
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
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(req.Type, true, out AccessCaseType type))
                return Validation("A valid type is required.");
            try
            {
                List<AccessCaseItemCreateSpec>? itemSpecs = null;
                if (req.Items is { Count: > 0 })
                {
                    itemSpecs = [];
                    foreach (CreateAccessCaseItemRequest item in req.Items)
                    {
                        if (!Enum.TryParse(item.Action, true, out AccessItemAction action))
                            return Validation("Each item requires a valid action.");
                        itemSpecs.Add(new AccessCaseItemCreateSpec(
                            item.AccessEntitlementId,
                            item.CustomName,
                            action,
                            item.Notes,
                            item.IsSelected ?? true));
                    }
                }

                bool submitForApproval = req.SubmitForApproval == true;
                AccessCaseDto created = await svc.CreateAsync(
                    type, session.Id, req.Reason, req.SubjectUserId, req.SubjectName, req.SubjectEmail,
                    req.DepartmentId, req.ManagerUserId, req.DesignatedApproverUserId, req.EffectiveAtUtc, ct,
                    accessCategoryId: req.AccessCategoryId,
                    hasConfigureOverride: session.Permissions.Contains(AccessConfigure),
                    items: itemSpecs,
                    submitForApproval: submitForApproval);

                if (submitForApproval || string.Equals(created.Status, nameof(AccessCaseStatus.Approval), StringComparison.OrdinalIgnoreCase))
                    await NotifyApproversForCaseAsync(created, svc, notifications, ct);

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
                    req.DepartmentId, req.ManagerUserId, req.DesignatedApproverUserId, req.EffectiveAtUtc, ct,
                    accessCategoryId: req.AccessCategoryId));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/submit", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                AccessCaseDto updated = await svc.SubmitAsync(id, session.Id, ct);
                await NotifyApproversForCaseAsync(updated, svc, notifications, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessRequest);

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
                IReadOnlyList<Guid> approverIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Approver, ct);
                AccessCaseDto updated = await svc.ApproveAsync(id, session.Id, ct);
                string category = AccessNotificationService.CategoryLabel(updated);
                string typeLabel = AccessNotificationService.TypeLabel(updated);
                string subject = AccessNotificationService.SubjectLabel(updated);

                IReadOnlyList<Guid> fulfillerIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Fulfiller, ct);
                await notifications.NotifyFulfillersAsync(
                    updated.Id, updated.CaseNumber, category, typeLabel, subject, fulfillerIds, ct);
                await notifications.NotifyRequesterApprovedAsync(
                    updated.Id, updated.CaseNumber, category, typeLabel, subject, updated.RequesterUserId, ct);
                await notifications.ResolveStageNotificationsAsync(
                    updated.Id, approverIds, [AccessNotificationService.TypeApprovalRequested], ct);
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
                IReadOnlyList<Guid> approverIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Approver, ct);
                AccessCaseDto updated = await svc.RejectAsync(id, session.Id, req?.Reason, ct);
                string category = AccessNotificationService.CategoryLabel(updated);
                string typeLabel = AccessNotificationService.TypeLabel(updated);
                string subject = AccessNotificationService.SubjectLabel(updated);
                await notifications.NotifyRequesterRejectedAsync(
                    updated.Id, updated.CaseNumber, category, typeLabel, subject, updated.RequesterUserId, ct);
                await notifications.ResolveStageNotificationsAsync(
                    updated.Id, approverIds, [AccessNotificationService.TypeApprovalRequested], ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessApprove);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/start-verification", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                IReadOnlyList<Guid> fulfillerIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Fulfiller, ct);
                AccessCaseDto updated = await svc.StartVerificationAsync(id, session.Id, ct);
                string category = AccessNotificationService.CategoryLabel(updated);
                string typeLabel = AccessNotificationService.TypeLabel(updated);
                string subject = AccessNotificationService.SubjectLabel(updated);
                bool isLeaver = string.Equals(updated.Type, nameof(AccessCaseType.Leaver), StringComparison.OrdinalIgnoreCase);
                IReadOnlyList<Guid> verifierIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Verifier, ct);
                IEnumerable<Guid> fallbacks = verifierIds.Where(v => updated.SubjectUserId is null || v != updated.SubjectUserId);
                await notifications.NotifyVerificationToSubjectOrFallbacksAsync(
                    updated.Id, updated.CaseNumber, category, typeLabel, subject,
                    updated.SubjectUserId, fallbacks, isLeaver, ct);
                await notifications.ResolveStageNotificationsAsync(
                    updated.Id, fulfillerIds, [AccessNotificationService.TypeFulfillmentReady], ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessFulfill);

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/verify", async (
            Guid id, VerifyAccessCaseRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                IReadOnlyList<Guid> verifierIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Verifier, ct);
                if (string.Equals(req.Mode, "fallback", StringComparison.OrdinalIgnoreCase))
                {
                    AccessCaseDto updated = await svc.VerifyFallbackAsync(id, session.Id, req.FallbackReason ?? req.Comment ?? "", ct);
                    await NotifyClosersForCaseAsync(updated, svc, notifications, ct);
                    await notifications.ResolveStageNotificationsAsync(
                        updated.Id, verifierIds.Concat(updated.SubjectUserId is Guid s ? [s] : []),
                        [AccessNotificationService.TypeVerificationRequired], ct);
                    return Results.Ok(updated);
                }

                AccessCaseDto verified = await svc.VerifyEmployeeAsync(id, session.Id, req.EverythingWorks != false, req.Comment, ct);
                string category = AccessNotificationService.CategoryLabel(verified);
                string typeLabel = AccessNotificationService.TypeLabel(verified);
                string subject = AccessNotificationService.SubjectLabel(verified);

                if (req.EverythingWorks == false)
                {
                    IReadOnlyList<Guid> fulfillerIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Fulfiller, ct);
                    await notifications.NotifyVerificationProblemAsync(
                        verified.Id, verified.CaseNumber, category, typeLabel, subject,
                        fulfillerIds, req.Comment ?? verified.VerificationComment, ct);
                }
                else
                {
                    await NotifyClosersForCaseAsync(verified, svc, notifications, ct);
                }

                await notifications.ResolveStageNotificationsAsync(
                    verified.Id, verifierIds.Concat(verified.SubjectUserId is Guid sub ? [sub] : []),
                    [AccessNotificationService.TypeVerificationRequired], ct);
                return Results.Ok(verified);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/access/cases/{id:guid}/close", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AccessCaseService svc, AccessNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                IReadOnlyList<Guid> closerIds = await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Closer, ct);
                AccessCaseDto updated = await svc.CloseAsync(id, session.Id, ct);
                string category = AccessNotificationService.CategoryLabel(updated);
                string typeLabel = AccessNotificationService.TypeLabel(updated);
                string subject = AccessNotificationService.SubjectLabel(updated);
                await notifications.NotifyCaseClosedAsync(
                    updated.Id, updated.CaseNumber, category, typeLabel, subject,
                    updated.RequesterUserId, updated.SubjectUserId, ct);
                await notifications.ResolveStageNotificationsAsync(
                    updated.Id, closerIds, [AccessNotificationService.TypeReadyToClose], ct);
                IReadOnlyList<Guid> leftoverRecipients = closerIds
                    .Concat(await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Approver, ct))
                    .Concat(await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Fulfiller, ct))
                    .Concat(await svc.GetRouteUserIdsAsync(id, AccessCategoryStage.Verifier, ct))
                    .Append(updated.RequesterUserId)
                    .Concat(updated.SubjectUserId is Guid s ? [s] : Array.Empty<Guid>())
                    .Distinct()
                    .ToList();
                await notifications.ResolveStageNotificationsAsync(
                    updated.Id,
                    leftoverRecipients,
                    [
                        AccessNotificationService.TypeApprovalRequested,
                        AccessNotificationService.TypeFulfillmentReady,
                        AccessNotificationService.TypeVerificationRequired,
                        AccessNotificationService.TypeVerificationProblem,
                        AccessNotificationService.TypeReadyToClose,
                    ],
                    ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AccessFulfill);

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

    private static async Task NotifyApproversForCaseAsync(
        AccessCaseDto accessCase,
        AccessCaseService svc,
        AccessNotificationService notifications,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> approverIds = await svc.GetRouteUserIdsAsync(
            accessCase.Id, AccessCategoryStage.Approver, ct);
        await notifications.NotifyApproversAsync(
            accessCase.Id,
            accessCase.CaseNumber,
            AccessNotificationService.CategoryLabel(accessCase),
            AccessNotificationService.TypeLabel(accessCase),
            AccessNotificationService.SubjectLabel(accessCase),
            approverIds,
            ct);
    }

    private static async Task NotifyClosersForCaseAsync(
        AccessCaseDto accessCase,
        AccessCaseService svc,
        AccessNotificationService notifications,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> closerIds = await svc.GetRouteUserIdsAsync(
            accessCase.Id, AccessCategoryStage.Closer, ct);
        await notifications.NotifyClosersAsync(
            accessCase.Id,
            accessCase.CaseNumber,
            AccessNotificationService.CategoryLabel(accessCase),
            AccessNotificationService.TypeLabel(accessCase),
            AccessNotificationService.SubjectLabel(accessCase),
            closerIds,
            ct);
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

public sealed class AccessNotificationService(IUserNotificationPublisher notifications)
{
    public const string ResourceType = "AccessCase";

    public const string TypeApprovalRequested = "access.approval_requested";
    public const string TypeApproved = "access.approved";
    public const string TypeRejected = "access.rejected";
    public const string TypeFulfillmentReady = "access.fulfillment_ready";
    public const string TypeVerificationRequired = "access.verification_required";
    public const string TypeVerificationProblem = "access.verification_problem";
    public const string TypeReadyToClose = "access.ready_to_close";
    public const string TypeClosed = "access.closed";
    public const string TypeReviewAssigned = "access.review_assigned";

    public async Task NotifyApproversAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        IEnumerable<Guid> approverIds,
        CancellationToken ct)
    {
        foreach (Guid recipientUserId in Distinct(approverIds))
        {
            await notifications.PublishAsync(
                recipientUserId,
                TypeApprovalRequested,
                "Warning",
                $"Approval requested: {caseNumber}",
                $"Please review {typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel}.",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
        }
    }

    public async Task NotifyFulfillersAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        IEnumerable<Guid> fulfillerIds,
        CancellationToken ct)
    {
        foreach (Guid recipientUserId in Distinct(fulfillerIds))
        {
            await notifications.PublishAsync(
                recipientUserId,
                TypeFulfillmentReady,
                "Info",
                $"Fulfillment ready: {caseNumber}",
                $"{typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel} is ready for checklist fulfillment.",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
        }
    }

    public Task NotifyRequesterApprovedAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        Guid requesterId,
        CancellationToken ct) =>
        notifications.PublishAsync(
            requesterId,
            TypeApproved,
            "Info",
            $"{caseNumber} approved",
            $"Your {typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel} was approved and is ready for fulfillment.",
            ResourceType,
            caseId,
            CaseUrl(caseId),
            ct);

    public Task NotifyRequesterRejectedAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        Guid requesterId,
        CancellationToken ct) =>
        notifications.PublishAsync(
            requesterId,
            TypeRejected,
            "Warning",
            $"{caseNumber} rejected",
            $"Your {typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel} was rejected.",
            ResourceType,
            caseId,
            CaseUrl(caseId),
            ct);

    public async Task NotifyVerificationToSubjectOrFallbacksAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        Guid? subjectUserId,
        IEnumerable<Guid> fallbackVerifierIds,
        bool isLeaver,
        CancellationToken ct)
    {
        if (!isLeaver && subjectUserId is Guid subject && subject != Guid.Empty)
        {
            await notifications.PublishAsync(
                subject,
                TypeVerificationRequired,
                "Info",
                $"Verification: {caseNumber}",
                $"Please verify that your {typeLabel} access changes for {caseNumber} ({categoryName}) are working.",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
            return;
        }

        foreach (Guid recipientUserId in Distinct(fallbackVerifierIds))
        {
            await notifications.PublishAsync(
                recipientUserId,
                TypeVerificationRequired,
                "Info",
                $"Verification: {caseNumber}",
                $"Please complete fallback verification for {typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel}.",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
        }
    }

    public async Task NotifyVerificationProblemAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        IEnumerable<Guid> fulfillerIds,
        string? comment,
        CancellationToken ct)
    {
        string detail = string.IsNullOrWhiteSpace(comment)
            ? "The employee reported a problem during verification."
            : comment.Trim();
        foreach (Guid recipientUserId in Distinct(fulfillerIds))
        {
            await notifications.PublishAsync(
                recipientUserId,
                TypeVerificationProblem,
                "Warning",
                $"Verification problem: {caseNumber}",
                $"{typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel} returned to fulfillment. {detail}",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
        }
    }

    public async Task NotifyClosersAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        IEnumerable<Guid> closerIds,
        CancellationToken ct)
    {
        foreach (Guid recipientUserId in Distinct(closerIds))
        {
            await notifications.PublishAsync(
                recipientUserId,
                TypeReadyToClose,
                "Info",
                $"Ready to close: {caseNumber}",
                $"{typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel} is verified and ready to close.",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
        }
    }

    public async Task NotifyCaseClosedAsync(
        Guid caseId,
        string caseNumber,
        string categoryName,
        string typeLabel,
        string subjectLabel,
        Guid requesterId,
        Guid? subjectUserId,
        CancellationToken ct)
    {
        HashSet<Guid> recipients = [requesterId];
        if (subjectUserId is Guid subject && subject != Guid.Empty)
            recipients.Add(subject);

        foreach (Guid recipientUserId in recipients)
        {
            await notifications.PublishAsync(
                recipientUserId,
                TypeClosed,
                "Info",
                $"{caseNumber} closed",
                $"{typeLabel} access case {caseNumber} ({categoryName}) for {subjectLabel} has been closed.",
                ResourceType,
                caseId,
                CaseUrl(caseId),
                ct);
        }
    }

    public async Task ResolveStageNotificationsAsync(
        Guid caseId,
        IEnumerable<Guid> recipientUserIds,
        IEnumerable<string> types,
        CancellationToken ct)
    {
        List<string> typeList = types.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToList();
        if (typeList.Count == 0) return;

        foreach (Guid recipientUserId in Distinct(recipientUserIds))
        {
            foreach (string type in typeList)
            {
                await notifications.MarkResourceNotificationsReadAsync(
                    recipientUserId, type, ResourceType, caseId, ct);
            }
        }
    }

    public Task NotifyReviewAssignedAsync(AccessReviewCampaignDto campaign, CancellationToken ct) =>
        notifications.PublishAsync(
            campaign.ReviewerUserId,
            TypeReviewAssigned,
            "Info",
            $"Review assigned: {campaign.Name}",
            $"Due {campaign.DueAtUtc:u}.",
            "AccessReview",
            campaign.Id,
            "/it/access/reviews",
            ct);

    public static string CategoryLabel(AccessCaseDto accessCase) =>
        accessCase.AccessCategoryDisplayName
        ?? accessCase.AccessCategoryNameSnapshot
        ?? "access category";

    public static string SubjectLabel(AccessCaseDto accessCase) =>
        !string.IsNullOrWhiteSpace(accessCase.SubjectName) ? accessCase.SubjectName!
        : !string.IsNullOrWhiteSpace(accessCase.SubjectEmail) ? accessCase.SubjectEmail!
        : "employee";

    public static string TypeLabel(AccessCaseDto accessCase) =>
        string.IsNullOrWhiteSpace(accessCase.Type) ? "Access" : accessCase.Type;

    private static string CaseUrl(Guid caseId) => $"/it/access/{caseId}";

    private static IEnumerable<Guid> Distinct(IEnumerable<Guid> ids) =>
        ids.Where(id => id != Guid.Empty).Distinct();
}

public sealed record CreateAccessCaseRequest(
    string Type, string Reason, Guid? SubjectUserId, string? SubjectName, string? SubjectEmail,
    Guid? DepartmentId, Guid? ManagerUserId, Guid? DesignatedApproverUserId, DateTimeOffset? EffectiveAtUtc,
    Guid? AccessCategoryId = null,
    IReadOnlyList<CreateAccessCaseItemRequest>? Items = null,
    bool? SubmitForApproval = null);

public sealed record CreateAccessCaseItemRequest(
    Guid? AccessEntitlementId,
    string? CustomName,
    string Action,
    string? Notes,
    bool? IsSelected);

public sealed record UpdateAccessCaseRequest(
    string Reason, Guid? SubjectUserId, string? SubjectName, string? SubjectEmail,
    Guid? DepartmentId, Guid? ManagerUserId, Guid? DesignatedApproverUserId, DateTimeOffset? EffectiveAtUtc,
    Guid? AccessCategoryId = null);

public sealed record UpsertAccessCategoryRequest(
    string Key,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    bool? IsActive,
    bool? PreferSubjectEmployeeVerification);

public sealed record ReplaceCategoryRoutingRequest(
    Guid[]? RequesterUserIds,
    Guid[]? ApproverUserIds,
    Guid[]? FulfillerUserIds,
    Guid[]? VerifierUserIds,
    Guid[]? CloserUserIds);

public sealed record ReplaceCategoryEntitlementsRequest(
    IReadOnlyList<ReplaceCategoryEntitlementItemRequest>? Items);

public sealed record ReplaceCategoryEntitlementItemRequest(
    Guid AccessEntitlementId,
    bool IsDefaultForJoiner,
    int SortOrder,
    bool IsActive);

public sealed record UpsertAccessEntitlementRequest(
    string Key,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    string DefaultRevokeAction,
    bool? IsPrivileged,
    bool? IsActive);

public sealed record VerifyAccessCaseRequest(
    string? Mode,
    bool? EverythingWorks,
    string? Comment,
    string? FallbackReason);

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
