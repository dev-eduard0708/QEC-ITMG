using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.ChangeManagement.Domain;
using Qec.Itmg.ChangeManagement.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;

namespace Qec.Itmg.Host.ChangeManagement;

public static class ChangeEndpoints
{
    public const string ChangeCreate = "change.create";
    public const string ChangeRead = "change.read";
    public const string ChangeAssess = "change.assess";
    public const string ChangeApprove = "change.approve";
    public const string ChangeSchedule = "change.schedule";
    public const string ChangeImplement = "change.implement";
    public const string ChangePir = "change.pir";
    public const string ChangeCatalogManage = "change.catalog.manage";

    public static IEndpointRouteBuilder MapChangeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/changes").RequirePermission(ChangeRead);

        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? type, string? status, string? risk, Guid? owner, Guid? ownerUserId,
            ChangeService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(
                page ?? 1, pageSize ?? 25, search,
                ParseEnum<ChangeType>(type), ParseEnum<ChangeStatus>(status), ParseEnum<ChangeRiskRating>(risk),
                owner ?? ownerUserId, ct)));

        read.MapGet("/catalog", async (ChangeService service, ClaimsPrincipal principal, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool manage = session.Permissions.Any(p => string.Equals(p, ChangeCatalogManage, StringComparison.OrdinalIgnoreCase));
            return Results.Ok(await service.ListCatalogAsync(activeOnly: !manage, ct));
        });

        read.MapGet("/catalog/{id:guid}", async (Guid id, ChangeService service, CancellationToken ct) =>
        {
            ChangeCatalogItemDto? item = await service.GetCatalogAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        read.MapGet("/{id:guid}", async (Guid id, ChangeService service, CancellationToken ct) =>
        {
            ChangeDto? item = await service.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        read.MapGet("/{id:guid}/configuration-items", async (Guid id, ChangeService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.ListCisAsync(id, ct)); }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        });

        read.MapGet("/{id:guid}/approvals", async (Guid id, ChangeService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.ListApprovalsAsync(id, ct)); }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        });

        read.MapGet("/{id:guid}/history", async (Guid id, ChangeHistoryService history, CancellationToken ct) =>
        {
            try { return Results.Ok(await history.ListAsync(id, ct)); }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        });

        endpoints.MapPost("/api/v1/changes", async (
            CreateChangeRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ChangeService service,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
                return ValidationProblem("title and description are required.");
            if (!Enum.TryParse(request.Type, true, out ChangeType type))
                return ValidationProblem("A valid type is required.");
            ChangeRiskRating risk = ChangeRiskRating.Medium;
            if (!string.IsNullOrWhiteSpace(request.RiskRating) && !Enum.TryParse(request.RiskRating, true, out risk))
                return ValidationProblem("Invalid riskRating.");
            try
            {
                var created = await service.CreateAsync(
                    request.Title, request.Description, type, session.Id, risk, request.OwnerUserId,
                    request.IsRetrospective ?? false, request.IsPreAuthorizedStandard ?? false,
                    request.RetrospectiveReason, request.ActualImplementationAtUtc, ct);
                return Results.Created($"/api/v1/changes/{created.Id}", await service.GetAsync(created.Id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex is ArgumentException a ? ValidationProblem(a.Message) : FromDomainError((InvalidOperationException)ex);
            }
        }).RequirePermission(ChangeCreate);

        endpoints.MapPost("/api/v1/changes/from-catalog/{catalogItemId:guid}", async (
            Guid catalogItemId,
            CreateFromCatalogRequest? request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ChangeService service,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                var created = await service.CreateFromCatalogAsync(
                    catalogItemId, session.Id, request?.Title, request?.Description, ct);
                return Results.Created($"/api/v1/changes/{created.Id}", await service.GetAsync(created.Id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeCreate);

        endpoints.MapPost("/api/v1/changes/catalog", async (
            UpsertCatalogRequest request, ChangeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
                return ValidationProblem("code and name are required.");
            if (!Enum.TryParse(request.RiskRating, true, out ChangeRiskRating risk))
                return ValidationProblem("A valid riskRating is required.");
            if (string.IsNullOrWhiteSpace(request.ImplementationPlan)
                || string.IsNullOrWhiteSpace(request.TestPlan)
                || string.IsNullOrWhiteSpace(request.RollbackPlan))
                return ValidationProblem("implementation, test, and rollback plans are required.");
            try
            {
                var created = await service.CreateCatalogAsync(
                    request.Code, request.Name, risk, request.ImplementationPlan, request.TestPlan,
                    request.RollbackPlan, request.Description, ct);
                return Results.Created($"/api/v1/changes/catalog/{created.Id}", await service.GetCatalogAsync(created.Id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex is ArgumentException a ? ValidationProblem(a.Message) : FromDomainError((InvalidOperationException)ex);
            }
        }).RequirePermission(ChangeCatalogManage);

        endpoints.MapPut("/api/v1/changes/catalog/{id:guid}", async (
            Guid id, UpsertCatalogRequest request, ChangeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return ValidationProblem("name is required.");
            if (!Enum.TryParse(request.RiskRating, true, out ChangeRiskRating risk))
                return ValidationProblem("A valid riskRating is required.");
            try
            {
                await service.UpdateCatalogAsync(
                    id, request.Name, request.Description, risk, request.ImplementationPlan ?? string.Empty,
                    request.TestPlan ?? string.Empty, request.RollbackPlan ?? string.Empty,
                    request.IsActive ?? true, request.RowVersion ?? string.Empty, ct);
                return Results.Ok(await service.GetCatalogAsync(id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex is ArgumentException a ? ValidationProblem(a.Message) : FromDomainError((InvalidOperationException)ex);
            }
        }).RequirePermission(ChangeCatalogManage);

        endpoints.MapPut("/api/v1/changes/{id:guid}", async (
            Guid id, UpdateChangeRequest request, ChangeService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
                return ValidationProblem("title and description are required.");
            if (!Enum.TryParse(request.Type, true, out ChangeType type))
                return ValidationProblem("A valid type is required.");
            if (!Enum.TryParse(request.RiskRating, true, out ChangeRiskRating risk))
                return ValidationProblem("A valid riskRating is required.");
            try
            {
                await service.UpdateAsync(
                    id, request.Title, request.Description, type, risk, request.OwnerUserId,
                    request.BusinessImpact, request.TechnicalImpact, request.SecurityImpact,
                    request.ImplementationPlan, request.TestPlan, request.RollbackPlan,
                    request.ScheduledStartUtc, request.ScheduledEndUtc, request.IsPreAuthorizedStandard ?? false,
                    request.RowVersion ?? string.Empty, ct);
                return Results.Ok(await service.GetAsync(id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex is ArgumentException a ? ValidationProblem(a.Message) : FromDomainError((InvalidOperationException)ex);
            }
        }).RequirePermission(ChangeAssess);

        endpoints.MapPost("/api/v1/changes/{id:guid}/retrospective", async (
            Guid id,
            MarkRetrospectiveRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ChangeService service,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (string.IsNullOrWhiteSpace(request.Reason)) return ValidationProblem("reason is required.");
            try
            {
                await service.MarkRetrospectiveAsync(
                    id, request.Reason, request.ActualImplementationAtUtc, request.RowVersion ?? string.Empty, session.Id, ct);
                return Results.Ok(await service.GetAsync(id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeAssess);

        endpoints.MapPost("/api/v1/changes/{id:guid}/configuration-items", async (
            Guid id, LinkChangeCiRequest request, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ChangeService service, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (request.ConfigurationItemId == Guid.Empty) return ValidationProblem("configurationItemId is required.");
            try
            {
                await service.LinkCiAsync(id, request.ConfigurationItemId, session.Id, ct);
                return Results.Ok(await service.ListCisAsync(id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeAssess);

        endpoints.MapDelete("/api/v1/changes/{id:guid}/configuration-items/{ciId:guid}", async (
            Guid id, Guid ciId, ChangeService service, CancellationToken ct) =>
        {
            try
            {
                await service.UnlinkCiAsync(id, ciId, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeAssess);

        endpoints.MapPost("/api/v1/changes/{id:guid}/request-approval", async (
            Guid id,
            RequestApprovalBody request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ChangeService service,
            ChangeNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (request.ApproverUserId == Guid.Empty) return ValidationProblem("approverUserId is required.");
            try
            {
                await service.RequestApprovalAsync(id, request.ApproverUserId, session.Id, ct);
                ChangeDto? change = await service.GetAsync(id, ct);
                if (change is not null)
                {
                    await notifications.NotifyApprovalRequestedAsync(change, request.ApproverUserId, ct);
                }

                return Results.Ok(await service.ListApprovalsAsync(id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeAssess);

        endpoints.MapPost("/api/v1/changes/{id:guid}/approve", async (
            Guid id, DecideChangeRequest request, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ChangeService service, ChangeNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            ChangeDto? change = await service.GetAsync(id, ct);
            if (change is null) return Results.NotFound();
            try
            {
                await service.DecideApprovalAsync(
                    id, session.Id, ApprovalDecision.Approved, request.Comment, change.RequesterUserId, ct);
                ChangeDto? updated = await service.GetAsync(id, ct);
                if (updated is not null) await notifications.NotifyDecisionAsync(updated, approved: true, ct);
                return Results.Ok(await service.ListApprovalsAsync(id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeApprove);

        endpoints.MapPost("/api/v1/changes/{id:guid}/reject", async (
            Guid id, DecideChangeRequest request, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ChangeService service, ChangeNotificationService notifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            ChangeDto? change = await service.GetAsync(id, ct);
            if (change is null) return Results.NotFound();
            try
            {
                await service.DecideApprovalAsync(
                    id, session.Id, ApprovalDecision.Rejected, request.Comment, change.RequesterUserId, ct);
                ChangeDto? updated = await service.GetAsync(id, ct);
                if (updated is not null) await notifications.NotifyDecisionAsync(updated, approved: false, ct);
                return Results.Ok(await service.ListApprovalsAsync(id, ct));
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeApprove);

        endpoints.MapPost("/api/v1/changes/{id:guid}/transition", async (
            Guid id,
            TransitionChangeRequest request,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            ChangeService service,
            ChangeNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!Enum.TryParse(request.TargetStatus, true, out ChangeStatus target))
                return ValidationProblem("A valid targetStatus is required.");

            ChangeResult? result = null;
            if (!string.IsNullOrWhiteSpace(request.Result) && Enum.TryParse(request.Result, true, out ChangeResult parsed))
                result = parsed;

            string? required = RequiredPermissionForTransition(target);
            if (required is not null && !session.Permissions.Any(p => string.Equals(p, required, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Json(
                    new { error = new { code = "permission_denied", message = $"{required} is required for this transition." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ChangeDto? before = await service.GetAsync(id, ct);
            try
            {
                await service.TransitionAsync(
                    id, target, session.Id, request.RowVersion ?? string.Empty, request.Comment,
                    request.ValidationNotes, request.PirNotes, result, request.ApproverUserId, ct);
                ChangeDto? after = await service.GetAsync(id, ct);
                if (after is not null)
                {
                    if (target == ChangeStatus.Approval && request.ApproverUserId is Guid approver)
                    {
                        await notifications.NotifyApprovalRequestedAsync(after, approver, ct);
                    }

                    if (before is not null)
                    {
                        await notifications.NotifyStatusAsync(after, before.Status, ct);
                    }
                }

                return Results.Ok(after);
            }
            catch (InvalidOperationException ex) { return FromDomainError(ex); }
        }).RequirePermission(ChangeRead);

        return endpoints;
    }

    private static string? RequiredPermissionForTransition(ChangeStatus target) =>
        target switch
        {
            ChangeStatus.Assessment => ChangeAssess,
            ChangeStatus.Approval => ChangeAssess,
            ChangeStatus.Scheduled => ChangeSchedule,
            ChangeStatus.Implementation or ChangeStatus.Validation or ChangeStatus.Failed or ChangeStatus.RolledBack
                => ChangeImplement,
            ChangeStatus.PostImplementationReview => ChangePir,
            ChangeStatus.Closed or ChangeStatus.RequiresFollowUp => ChangeImplement,
            ChangeStatus.Cancelled => ChangeAssess,
            _ => ChangeAssess,
        };

    private static IResult SessionUnavailable() =>
        Results.Json(new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult ValidationProblem(string message) =>
        Results.Json(new { error = new { code = "validation_error", message } }, statusCode: StatusCodes.Status400BadRequest);

    private static IResult FromDomainError(InvalidOperationException ex)
    {
        string message = ex.Message;
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = new { code = "not_found", message } }, statusCode: StatusCodes.Status404NotFound);
        if (message.Contains("modified by another", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Cannot transition", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = new { code = "conflict", message } }, statusCode: StatusCodes.Status409Conflict);
        return Results.Json(new { error = new { code = "invalid_operation", message } }, statusCode: StatusCodes.Status400BadRequest);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.TryParse(value, true, out TEnum parsed) ? parsed : null;
}

public sealed record CreateChangeRequest(
    string Title, string Description, string Type, string? RiskRating, Guid? OwnerUserId,
    bool? IsRetrospective, bool? IsPreAuthorizedStandard, string? RetrospectiveReason, DateTimeOffset? ActualImplementationAtUtc);

public sealed record CreateFromCatalogRequest(string? Title, string? Description);

public sealed record UpsertCatalogRequest(
    string? Code, string Name, string? Description, string RiskRating,
    string? ImplementationPlan, string? TestPlan, string? RollbackPlan, bool? IsActive, string? RowVersion);

public sealed record UpdateChangeRequest(
    string Title, string Description, string Type, string RiskRating, Guid? OwnerUserId,
    string? BusinessImpact, string? TechnicalImpact, string? SecurityImpact,
    string? ImplementationPlan, string? TestPlan, string? RollbackPlan,
    DateTimeOffset? ScheduledStartUtc, DateTimeOffset? ScheduledEndUtc,
    bool? IsPreAuthorizedStandard, string? RowVersion);

public sealed record LinkChangeCiRequest(Guid ConfigurationItemId);
public sealed record DecideChangeRequest(string? Comment);
public sealed record RequestApprovalBody(Guid ApproverUserId);
public sealed record MarkRetrospectiveRequest(string Reason, DateTimeOffset? ActualImplementationAtUtc, string? RowVersion);
public sealed record TransitionChangeRequest(
    string TargetStatus, string? Comment, string? ValidationNotes, string? PirNotes, string? Result,
    string? RowVersion, Guid? ApproverUserId);
