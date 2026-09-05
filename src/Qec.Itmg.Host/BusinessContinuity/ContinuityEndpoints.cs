using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.BusinessContinuity.Domain;
using Qec.Itmg.BusinessContinuity.Services;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;

namespace Qec.Itmg.Host.BusinessContinuity;

public static class ContinuityEndpoints
{
    public const string BcmRead = "bcm.read";
    public const string BcmManage = "bcm.manage";
    public const string DrTestManage = "dr.test.manage";

    public static IEndpointRouteBuilder MapContinuityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapDashboard(endpoints);
        MapBia(endpoints);
        MapPlans(endpoints);
        MapProcedures(endpoints);
        MapDrTests(endpoints);
        MapSpof(endpoints);
        return endpoints;
    }

    private static void MapDashboard(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/continuity/dashboard", async (
            ContinuityService bcm, BusinessServiceService services, ConfigurationItemService cis, CancellationToken ct) =>
        {
            var svcList = await services.ListAsync(ct);
            var svcTuples = svcList.Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList();
            int spofs = await cis.CountConfirmedSpofsAsync(ct);
            return Results.Ok(await bcm.GetDashboardCountsAsync(svcTuples, spofs, ct));
        }).RequirePermission(BcmRead);

        endpoints.MapGet("/api/v1/continuity/readiness", async (
            ContinuityService bcm, BusinessServiceService services, CancellationToken ct) =>
        {
            var svcList = await services.ListAsync(ct);
            var spofByService = await services.CountSpofsByServiceAsync(ct);
            var rows = await bcm.GetServiceReadinessAsync(
                svcList.Select(s => (s.Id, s.Name, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(),
                spofByService,
                ct);
            return Results.Ok(rows);
        }).RequirePermission(BcmRead);
    }

    private static void MapBia(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/continuity/bia", async (
            Guid? businessServiceId, string? status, ContinuityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListBiaAsync(businessServiceId, ParseEnum<BiaStatus>(status), ct)))
            .RequirePermission(BcmRead);

        endpoints.MapGet("/api/v1/continuity/bia/{id:guid}", async (
            Guid id, ContinuityService svc, BusinessServiceService services, CancellationToken ct) =>
        {
            BiaDto? item = await svc.GetBiaAsync(id, ct);
            if (item is null) return Results.NotFound();
            BusinessServiceDto? service = await services.GetAsync(item.BusinessServiceId, ct);
            IReadOnlyList<ContinuityLinkDto> links = await svc.ListLinksAsync(id, "BiaRecord", ct);
            IReadOnlyList<Guid> cis = await services.ListLinkedConfigurationItemIdsAsync(item.BusinessServiceId, ct);
            return Results.Ok(new
            {
                bia = item,
                businessService = service,
                linkedConfigurationItemIds = cis,
                links,
            });
        }).RequirePermission(BcmRead);

        endpoints.MapPost("/api/v1/continuity/bia", async (
            CreateBiaRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                BiaDto created = await svc.CreateBiaAsync(
                    req.BusinessServiceId, req.OwnerUserId ?? session.Id, req.BusinessImpactSummary,
                    req.Criticality ?? "High", req.BusinessProcessName, req.FinancialImpact,
                    req.OperationalImpact, req.RegulatoryImpact, req.ReputationalImpact,
                    req.MaximumTolerableDowntimeMinutes, ct);
                return Results.Created($"/api/v1/continuity/bia/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);

        endpoints.MapPost("/api/v1/continuity/bia/{id:guid}/transition", async (
            Guid id, TransitionRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.Status, true, out BiaStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.TransitionBiaAsync(id, status, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);

        endpoints.MapPost("/api/v1/continuity/bia/{id:guid}/links", async (
            Guid id, LinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out ContinuityLinkTargetType targetType))
                return Validation("Valid targetType required.");
            try { return Results.Ok(await svc.AddLinkAsync(id, "BiaRecord", targetType, req.TargetId, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);
    }

    private static void MapPlans(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/continuity/plans", async (
            string? type, string? status, ContinuityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPlansAsync(ParseEnum<ContinuityPlanType>(type), ParseEnum<ContinuityPlanStatus>(status), ct)))
            .RequirePermission(BcmRead);

        endpoints.MapGet("/api/v1/continuity/plans/{id:guid}", async (Guid id, ContinuityService svc, CancellationToken ct) =>
        {
            ContinuityPlanDto? item = await svc.GetPlanAsync(id, ct);
            if (item is null) return Results.NotFound();
            return Results.Ok(new
            {
                plan = item,
                links = await svc.ListLinksAsync(id, "ContinuityPlan", ct),
                procedures = await svc.ListProceduresAsync(id, ct),
            });
        }).RequirePermission(BcmRead);

        endpoints.MapPost("/api/v1/continuity/plans", async (
            CreatePlanRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.PlanType ?? "BusinessContinuity", true, out ContinuityPlanType planType))
                return Validation("Valid planType required.");
            try
            {
                ContinuityPlanDto created = await svc.CreatePlanAsync(
                    req.Title, planType, req.OwnerUserId ?? session.Id, req.ManagedDocumentId,
                    req.EffectiveAtUtc, req.ReviewAtUtc, ct);
                return Results.Created($"/api/v1/continuity/plans/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);

        endpoints.MapPost("/api/v1/continuity/plans/{id:guid}/transition", async (
            Guid id, TransitionRequest req, ContinuityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out ContinuityPlanStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.TransitionPlanAsync(id, status, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);

        endpoints.MapPost("/api/v1/continuity/plans/{id:guid}/links", async (
            Guid id, LinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out ContinuityLinkTargetType targetType))
                return Validation("Valid targetType required.");
            try { return Results.Ok(await svc.AddLinkAsync(id, "ContinuityPlan", targetType, req.TargetId, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);
    }

    private static void MapProcedures(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/continuity/procedures", async (Guid? continuityPlanId, ContinuityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListProceduresAsync(continuityPlanId, ct))).RequirePermission(BcmRead);

        endpoints.MapPost("/api/v1/continuity/procedures", async (
            CreateProcedureRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                return Results.Ok(await svc.CreateProcedureAsync(
                    req.ContinuityPlanId, req.Title, req.OwnerUserId ?? session.Id,
                    req.ManagedDocumentId, req.Sequence, req.RecoveryStage, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);

        endpoints.MapPost("/api/v1/continuity/procedures/{id:guid}/links", async (
            Guid id, LinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out ContinuityLinkTargetType targetType))
                return Validation("Valid targetType required.");
            try { return Results.Ok(await svc.AddLinkAsync(id, "RecoveryProcedure", targetType, req.TargetId, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);
    }

    private static void MapDrTests(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/continuity/tests", async (
            Guid? businessServiceId, string? status, ContinuityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListDrTestsAsync(businessServiceId, ParseEnum<DrTestStatus>(status), ct)))
            .RequirePermission(BcmRead);

        endpoints.MapGet("/api/v1/continuity/tests/{id:guid}", async (
            Guid id, ContinuityService svc, BusinessServiceService services, CancellationToken ct) =>
        {
            DrTestDto? stub = await svc.GetDrTestAsync(id, null, null, ct);
            if (stub is null) return Results.NotFound();
            BusinessServiceDto? service = await services.GetAsync(stub.BusinessServiceId, ct);
            DrTestDto? item = await svc.GetDrTestAsync(id, service?.RtoMinutes, service?.RpoMinutes, ct);
            return Results.Ok(new
            {
                test = item,
                businessService = service,
                links = await svc.ListLinksAsync(id, "DrTest", ct),
            });
        }).RequirePermission(BcmRead);

        endpoints.MapPost("/api/v1/continuity/tests", async (
            CreateDrTestRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TestType ?? "Tabletop", true, out DrTestType testType))
                return Validation("Valid testType required.");
            try
            {
                DrTestDto created = await svc.CreateDrTestAsync(
                    req.Title, req.BusinessServiceId, testType, req.PlannedAtUtc,
                    req.OwnerUserId ?? session.Id, req.ContinuityPlanId, ct);
                return Results.Created($"/api/v1/continuity/tests/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DrTestManage);

        endpoints.MapPost("/api/v1/continuity/tests/{id:guid}/start", async (Guid id, ContinuityService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.StartDrTestAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DrTestManage);

        endpoints.MapPost("/api/v1/continuity/tests/{id:guid}/complete", async (
            Guid id, CompleteDrTestRequest req, ContinuityService svc, BusinessServiceService services,
            INotificationService notifications, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Result, true, out DrTestResult result))
                return Validation("Valid result required.");
            try
            {
                DrTestDto? existing = await svc.GetDrTestAsync(id, null, null, ct);
                if (existing is null) return Results.NotFound();
                BusinessServiceDto? service = await services.GetAsync(existing.BusinessServiceId, ct);
                DrTestDto completed = await svc.CompleteDrTestAsync(
                    id, result, req.ObservedRtoMinutes, req.ObservedRpoMinutes, req.Summary, req.Gaps,
                    service?.RtoMinutes, service?.RpoMinutes, ct);
                if (completed.RtoMet == false || completed.RpoMet == false)
                {
                    const string eventKey = "bcm.rto_rpo_missed";
                    if (!await svc.HasNotificationAsync(completed.Id, eventKey, ct))
                    {
                        string detail = string.Join("; ", new[]
                        {
                            completed.RtoMet == false ? "RTO not met" : null,
                            completed.RpoMet == false ? "RPO not met" : null,
                        }.Where(x => x is not null));
                        await notifications.CreateAsync(
                            completed.OwnerUserId, eventKey, NotificationSeverity.Warning,
                            $"RTO/RPO miss on {completed.DrTestNumber}", detail,
                            "DrTest", completed.Id, "/it/continuity", ct);
                        await svc.RecordNotificationAsync(completed.Id, eventKey, ct);
                    }
                }
                return Results.Ok(completed);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DrTestManage);

        endpoints.MapPost("/api/v1/continuity/tests/{id:guid}/cancel", async (Guid id, ContinuityService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.CancelDrTestAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DrTestManage);

        endpoints.MapPost("/api/v1/continuity/tests/{id:guid}/links", async (
            Guid id, LinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ContinuityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out ContinuityLinkTargetType targetType))
                return Validation("Valid targetType required.");
            try { return Results.Ok(await svc.AddLinkAsync(id, "DrTest", targetType, req.TargetId, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(DrTestManage);
    }

    private static void MapSpof(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/continuity/spofs", async (
            ConfigurationItemService cis, BusinessServiceService services, CancellationToken ct) =>
        {
            IReadOnlyList<ConfigurationItemDto> spofs = await cis.ListSpofsAsync(ct);
            var byService = await services.CountSpofsByServiceAsync(ct);
            return Results.Ok(new { items = spofs, byBusinessService = byService });
        }).RequirePermission(BcmRead);

        endpoints.MapPost("/api/v1/continuity/spofs/{ciId:guid}", async (
            Guid ciId, SetSpofBody req, ConfigurationItemService cis, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await cis.SetSpofAsync(
                    ciId, req.IsSinglePointOfFailure, req.Reason, req.MitigationNotes, req.RiskId,
                    req.Confirmed, req.RowVersion ?? string.Empty, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(BcmManage);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record CreateBiaRequest(
        Guid BusinessServiceId, Guid? OwnerUserId, string BusinessImpactSummary, string? Criticality,
        string? BusinessProcessName, string? FinancialImpact, string? OperationalImpact,
        string? RegulatoryImpact, string? ReputationalImpact, int? MaximumTolerableDowntimeMinutes);
    private sealed record CreatePlanRequest(
        string Title, string? PlanType, Guid? OwnerUserId, Guid? ManagedDocumentId,
        DateTimeOffset? EffectiveAtUtc, DateTimeOffset? ReviewAtUtc);
    private sealed record CreateProcedureRequest(
        Guid ContinuityPlanId, string Title, Guid? OwnerUserId, Guid? ManagedDocumentId, int? Sequence, string? RecoveryStage);
    private sealed record CreateDrTestRequest(
        string Title, Guid BusinessServiceId, string? TestType, DateTimeOffset PlannedAtUtc,
        Guid? OwnerUserId, Guid? ContinuityPlanId);
    private sealed record CompleteDrTestRequest(
        string Result, int? ObservedRtoMinutes, int? ObservedRpoMinutes, string? Summary, string? Gaps);
    private sealed record TransitionRequest(string Status);
    private sealed record LinkRequest(string TargetType, Guid TargetId);
    private sealed record SetSpofBody(
        bool IsSinglePointOfFailure, string? Reason, string? MitigationNotes, Guid? RiskId,
        bool Confirmed, string? RowVersion);
}

public sealed class ContinuityReminderJob(
    ContinuityService continuity,
    INotificationService notifications,
    IClock clock)
{
    private static readonly int[] Thresholds = [30, 14, 7, 1, 0];

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        int sent = 0;
        DateTimeOffset now = clock.UtcNow;

        foreach (var bia in await continuity.GetBiasNeedingReviewAsync(cancellationToken))
        {
            string eventKey = bia.Status == BiaStatus.InReview ? "bcm.bia_review_due" : "bcm.bia_annual_review_due";
            if (await continuity.HasNotificationAsync(bia.Id, eventKey, cancellationToken)) continue;
            await notifications.CreateAsync(
                bia.OwnerUserId, eventKey, NotificationSeverity.Warning,
                $"BIA review due: {bia.BiaNumber}",
                bia.BusinessImpactSummary, "BiaRecord", bia.Id, "/it/continuity", cancellationToken);
            await continuity.RecordNotificationAsync(bia.Id, eventKey, cancellationToken);
            sent++;
        }

        foreach (var plan in await continuity.GetPlansNeedingReviewAsync(cancellationToken))
        {
            if (plan.ReviewAtUtc is not DateTimeOffset due) continue;
            int days = (int)Math.Floor((due - now).TotalDays);
            foreach (int threshold in Thresholds)
            {
                bool match = threshold == 0 ? days < 0 : days == threshold;
                if (!match) continue;
                string eventKey = threshold == 0 ? "bcm.plan_review_overdue" : $"bcm.plan_review_due_{threshold}";
                if (await continuity.HasNotificationAsync(plan.Id, eventKey, cancellationToken)) continue;
                await notifications.CreateAsync(
                    plan.OwnerUserId, eventKey,
                    threshold is 0 or 1 or 7 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    threshold == 0 ? $"Plan review overdue: {plan.PlanNumber}" : $"Plan review due in {days}d: {plan.PlanNumber}",
                    plan.Title, "ContinuityPlan", plan.Id, "/it/continuity", cancellationToken);
                await continuity.RecordNotificationAsync(plan.Id, eventKey, cancellationToken);
                sent++;
                break;
            }
        }

        foreach (var test in await continuity.GetDrTestNotificationCandidatesAsync(cancellationToken))
        {
            if (test.Status == DrTestStatus.Planned)
            {
                int days = (int)Math.Floor((test.PlannedAtUtc - now).TotalDays);
                foreach (int threshold in Thresholds)
                {
                    bool match = threshold == 0 ? days < 0 : days == threshold;
                    if (!match) continue;
                    string eventKey = threshold == 0 ? "bcm.dr_test_overdue" : $"bcm.dr_test_due_{threshold}";
                    if (await continuity.HasNotificationAsync(test.Id, eventKey, cancellationToken)) continue;
                    await notifications.CreateAsync(
                        test.OwnerUserId, eventKey,
                        threshold is 0 or 1 or 7 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                        threshold == 0 ? $"DR test overdue: {test.DrTestNumber}" : $"DR test in {days}d: {test.DrTestNumber}",
                        test.Title, "DrTest", test.Id, "/it/continuity", cancellationToken);
                    await continuity.RecordNotificationAsync(test.Id, eventKey, cancellationToken);
                    sent++;
                    break;
                }
            }
            else if (test.Status == DrTestStatus.Completed && test.Result is DrTestResult.Failed or DrTestResult.PassedWithGaps)
            {
                string eventKey = $"bcm.dr_test_result_{test.Result}";
                if (await continuity.HasNotificationAsync(test.Id, eventKey, cancellationToken)) continue;
                await notifications.CreateAsync(
                    test.OwnerUserId, eventKey, NotificationSeverity.Warning,
                    $"DR test {test.Result}: {test.DrTestNumber}",
                    test.Gaps ?? test.Summary ?? test.Title, "DrTest", test.Id, "/it/continuity", cancellationToken);
                await continuity.RecordNotificationAsync(test.Id, eventKey, cancellationToken);
                sent++;
            }
        }

        return sent;
    }
}
