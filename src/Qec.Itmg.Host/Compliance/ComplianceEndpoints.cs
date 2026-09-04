using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using System.Security.Claims;

namespace Qec.Itmg.Host.Compliance;

public static class ComplianceEndpoints
{
    public const string ComplianceRead = "compliance.read";
    public const string FrameworkManage = "framework.manage";
    public const string AssessmentPerform = "assessment.perform";

    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapOverview(endpoints);
        MapFrameworks(endpoints);
        MapMappings(endpoints);
        MapCoverage(endpoints);
        MapAssessments(endpoints);
        MapCalendar(endpoints);
        return endpoints;
    }

    private static void MapOverview(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/compliance/overview", async (
            Guid? frameworkVersionId, CoverageService coverage, ComplianceCalendarService calendar,
            FrameworkService frameworks, CancellationToken ct) =>
        {
            FrameworkCoverageDto? cov = null;
            if (frameworkVersionId is Guid vid)
                cov = await coverage.GetCoverageAsync(vid, null, null, ct);
            else
            {
                IReadOnlyList<FrameworkDto> list = await frameworks.ListAsync(ct);
                FrameworkDetailDto? first = list.Count > 0 ? await frameworks.GetAsync(list[0].Id, ct) : null;
                FrameworkVersionDto? current = first?.Versions.FirstOrDefault(v => v.IsCurrent) ?? first?.Versions.FirstOrDefault();
                if (current is not null)
                    cov = await coverage.GetCoverageAsync(current.Id, null, null, ct);
            }

            IReadOnlyList<CalendarItemDto> upcoming = await calendar.ListAsync("upcoming", ct);
            IReadOnlyList<CalendarItemDto> overdue = await calendar.ListAsync("overdue", ct);
            return Results.Ok(new
            {
                coverage = cov,
                upcomingCount = upcoming.Count,
                overdueCount = overdue.Count,
                notes = "Dashboard shows counts and states only. No organization-wide compliance percentage.",
            });
        }).RequirePermission(ComplianceRead);
    }

    private static void MapFrameworks(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/compliance/frameworks").RequirePermission(ComplianceRead);
        read.MapGet(string.Empty, async (FrameworkService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)));
        read.MapGet("/{id:guid}", async (Guid id, FrameworkService svc, CancellationToken ct) =>
        {
            FrameworkDetailDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/versions/{versionId:guid}/requirements", async (Guid versionId, FrameworkService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRequirementsAsync(versionId, ct)));
        read.MapGet("/requirements/{id:guid}", async (Guid id, FrameworkService svc, ControlMappingService maps, CancellationToken ct) =>
        {
            FrameworkRequirementDto? req = await svc.GetRequirementAsync(id, ct);
            if (req is null) return Results.NotFound();
            IReadOnlyList<ControlMappingDto> mapped = await maps.ListAsync(null, id, null, ct);
            return Results.Ok(new { requirement = req, mappedControls = mapped });
        });

        endpoints.MapPost("/api/v1/compliance/frameworks", async (
            CreateFrameworkRequest req, FrameworkService svc, CancellationToken ct) =>
        {
            try
            {
                FrameworkDto created = await svc.CreateAsync(req.Code, req.Name, req.Publisher, req.Description, ct);
                return Results.Created($"/api/v1/compliance/frameworks/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPut("/api/v1/compliance/frameworks/{id:guid}", async (
            Guid id, UpdateFrameworkRequest req, FrameworkService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.UpdateAsync(id, req.Name, req.Publisher, req.Description, req.IsActive, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/frameworks/{id:guid}/versions", async (
            Guid id, CreateVersionRequest req, FrameworkService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.AddVersionAsync(
                    id, req.VersionCode, req.Title, req.PublishedDate, req.EffectiveDate, req.IsCurrent ?? false, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/frameworks/{id:guid}/versions/{versionId:guid}/current", async (
            Guid id, Guid versionId, FrameworkService svc, CancellationToken ct) =>
        {
            try { await svc.SetCurrentVersionAsync(id, versionId, ct); return Results.NoContent(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/frameworks/versions/{versionId:guid}/requirements", async (
            Guid versionId, CreateRequirementRequest req, FrameworkService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.RequirementType, true, out FrameworkRequirementType type))
                return Validation("Valid requirementType required.");
            try
            {
                return Results.Ok(await svc.AddRequirementAsync(
                    versionId, req.Code, req.Title, type, req.ParentRequirementId, req.Text, req.SortOrder, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/frameworks/seed-structure", async (
            FrameworkStructureSeedService seed, CancellationToken ct) =>
        {
            int n = await seed.EnsureStructureAsync(ct);
            return Results.Ok(new { frameworksEnsured = n });
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/frameworks/import", async (
            HttpRequest http, FrameworkImportService import, CancellationToken ct) =>
        {
            using StreamReader reader = new(http.Body);
            string json = await reader.ReadToEndAsync(ct);
            try { return Results.Ok(await import.ImportJsonAsync(json, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);
    }

    private static void MapMappings(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/compliance/mappings", async (
            Guid? internalControlId, Guid? frameworkRequirementId, Guid? frameworkVersionId,
            ControlMappingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(internalControlId, frameworkRequirementId, frameworkVersionId, ct)))
            .RequirePermission(ComplianceRead);

        endpoints.MapPost("/api/v1/compliance/mappings", async (
            CreateMappingRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ControlMappingService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.Relationship ?? "Primary", true, out MappingRelationship rel))
                return Validation("Valid relationship required.");
            try
            {
                return Results.Ok(await svc.CreateAsync(
                    req.InternalControlId, req.FrameworkRequirementId, rel, session.Id, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapDelete("/api/v1/compliance/mappings/{id:guid}", async (
            Guid id, ControlMappingService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(FrameworkManage);
    }

    private static void MapCoverage(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/compliance/coverage/{frameworkVersionId:guid}", async (
            Guid frameworkVersionId, DateOnly? periodStart, DateOnly? periodEnd,
            CoverageService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.GetCoverageAsync(frameworkVersionId, periodStart, periodEnd, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ComplianceRead);
    }

    private static void MapAssessments(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/compliance/assessments").RequirePermission(ComplianceRead);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, Guid? internalControlId, string? status,
            ControlAssessmentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(
                page ?? 1, pageSize ?? 25, internalControlId, ParseEnum<AssessmentStatus>(status), ct)));
        read.MapGet("/{id:guid}", async (Guid id, ControlAssessmentService svc, CancellationToken ct) =>
        {
            ControlAssessmentDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        endpoints.MapPost("/api/v1/compliance/assessments", async (
            CreateAssessmentRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ControlAssessmentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                return Results.Ok(await svc.CreateAsync(
                    req.InternalControlId, req.FrameworkVersionId, req.PeriodStart, req.PeriodEnd,
                    req.AssessorUserId ?? session.Id, req.TestProcedureId, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AssessmentPerform);

        endpoints.MapPost("/api/v1/compliance/assessments/{id:guid}/start", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ControlAssessmentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try { return Results.Ok(await svc.StartAsync(id, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AssessmentPerform);

        endpoints.MapPost("/api/v1/compliance/assessments/{id:guid}/result", async (
            Guid id, RecordResultRequest req, ControlAssessmentService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Result, true, out AssessmentResult result))
                return Validation("Valid result required.");
            try { return Results.Ok(await svc.RecordResultAsync(id, result, req.Notes, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AssessmentPerform);

        endpoints.MapPost("/api/v1/compliance/assessments/{id:guid}/complete", async (
            Guid id, CompleteAssessmentRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            ControlAssessmentService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.Result, true, out AssessmentResult result))
                return Validation("Valid result required.");
            try { return Results.Ok(await svc.CompleteAsync(id, result, session.Id, req.Notes, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AssessmentPerform);
    }

    private static void MapCalendar(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/compliance/calendar", async (
            string? bucket, ComplianceCalendarService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(bucket, ct))).RequirePermission(ComplianceRead);

        endpoints.MapPost("/api/v1/compliance/calendar", async (
            CreateCalendarRequest req, ComplianceCalendarService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.ItemType, true, out CalendarItemType type))
                return Validation("Valid itemType required.");
            try
            {
                return Results.Ok(await svc.CreateAsync(
                    req.Title, type, req.DueAtUtc, req.InternalControlId, req.FrameworkVersionId, req.OwnerUserId, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/calendar/{id:guid}/status", async (
            Guid id, CalendarStatusRequest req, ComplianceCalendarService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out CalendarItemStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.SetStatusAsync(id, status, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);

        endpoints.MapPost("/api/v1/compliance/calendar/schedule-next", async (
            ScheduleNextRequest req, ComplianceCalendarService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.ScheduleNextFromFrequencyAsync(
                    req.InternalControlId, req.Title, req.Frequency, req.OwnerUserId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FrameworkManage);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record CreateFrameworkRequest(string Code, string Name, string Publisher, string? Description);
    private sealed record UpdateFrameworkRequest(string Name, string Publisher, string? Description, bool IsActive);
    private sealed record CreateVersionRequest(
        string VersionCode, string? Title, DateOnly? PublishedDate, DateOnly? EffectiveDate, bool? IsCurrent);
    private sealed record CreateRequirementRequest(
        string Code, string Title, string RequirementType, Guid? ParentRequirementId, string? Text, int? SortOrder);
    private sealed record CreateMappingRequest(
        Guid InternalControlId, Guid FrameworkRequirementId, string? Relationship, string? Notes);
    private sealed record CreateAssessmentRequest(
        Guid InternalControlId, Guid? FrameworkVersionId, DateOnly? PeriodStart, DateOnly? PeriodEnd,
        Guid? AssessorUserId, Guid? TestProcedureId, string? Notes);
    private sealed record RecordResultRequest(string Result, string? Notes);
    private sealed record CompleteAssessmentRequest(string Result, string? Notes);
    private sealed record CreateCalendarRequest(
        string Title, string ItemType, DateTimeOffset DueAtUtc, Guid? InternalControlId,
        Guid? FrameworkVersionId, Guid? OwnerUserId, string? Notes);
    private sealed record CalendarStatusRequest(string Status);
    private sealed record ScheduleNextRequest(Guid InternalControlId, string Title, string Frequency, Guid? OwnerUserId);
}
