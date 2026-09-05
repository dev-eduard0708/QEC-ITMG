using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Email;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Identity.Domain;
using Qec.Itmg.Identity.Persistence;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.Security.Domain;
using Qec.Itmg.Security.Services;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;
using Qec.Itmg.Host.ServiceDesk;

namespace Qec.Itmg.Host.Security;

public static class SecurityEndpoints
{
    public const string SecDashboard = "sec.dashboard";
    public const string SecAwarenessManage = "sec.awareness.manage";
    public const string VulnRead = "vuln.read";
    public const string VulnManage = "vuln.manage";
    public const string RiskManage = "risk.manage";
    public const string ExceptionApprove = "exception.approve";
    public const string TicketReadSecurity = "ticket.read.security";
    public const string IncidentsSecurity = "incidents.security";

    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapDashboard(endpoints);
        MapVulnerabilities(endpoints);
        MapRisks(endpoints);
        MapExceptions(endpoints);
        MapPentests(endpoints);
        MapAwareness(endpoints);
        MapMeSecurity(endpoints);
        return endpoints;
    }

    private static void MapDashboard(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/security/dashboard", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService sec, TicketService tickets, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            int openSecurity = 0;
            if (Can(session, TicketReadSecurity) || Can(session, IncidentsSecurity) || Can(session, SecDashboard))
                openSecurity = await tickets.CountOpenSecurityIncidentsAsync(ct);
            return Results.Ok(await sec.GetDashboardCountsAsync(openSecurity, ct));
        }).RequirePermission(SecDashboard);
    }

    private static void MapVulnerabilities(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/security/vulnerabilities").RequirePermission(VulnRead);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? status, string? severity, bool? overdueOnly,
            SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListVulnerabilitiesAsync(
                page ?? 1, pageSize ?? 25, search, ParseEnum<VulnerabilityStatus>(status),
                ParseEnum<VulnerabilitySeverity>(severity), overdueOnly == true, ct)));
        read.MapGet("/{id:guid}", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            VulnerabilityDto? item = await svc.GetVulnerabilityAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/{id:guid}/links", async (Guid id, SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRemediationLinksAsync(id, ct)));

        endpoints.MapPost("/api/v1/security/vulnerabilities", async (
            CreateVulnRequest req, SecurityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Severity ?? "Medium", true, out VulnerabilitySeverity severity))
                return Validation("Valid severity required.");
            try
            {
                VulnerabilityDto created = await svc.CreateVulnerabilityAsync(
                    req.Title, req.ConfigurationItemId, req.Source ?? "Manual", severity, req.DetectedAtUtc,
                    req.Description, req.ExternalReference, req.DueAtUtc, req.OwnerUserId, ct);
                return Results.Created($"/api/v1/security/vulnerabilities/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);

        endpoints.MapPost("/api/v1/security/vulnerabilities/{id:guid}/transition", async (
            Guid id, VulnTransitionRequest req, SecurityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out VulnerabilityStatus status))
                return Validation("Valid status required.");
            try
            {
                return Results.Ok(await svc.TransitionVulnerabilityAsync(
                    id, status, req.ResolutionSummary, req.AcceptedRiskReason, req.ExceptionId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);

        endpoints.MapPost("/api/v1/security/vulnerabilities/{id:guid}/links", async (
            Guid id, RemediationLinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.LinkType, true, out VulnerabilityRemediationLinkType linkType))
                return Validation("Valid linkType required.");
            try
            {
                return Results.Ok(await svc.AddRemediationLinkAsync(id, linkType, req.TargetId, session.Id, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);

        endpoints.MapPost("/api/v1/security/vulnerabilities/ingest-stub", async (SecurityService svc, CancellationToken ct) =>
        {
            int created = await svc.IngestFromScannerStubAsync(ct);
            return Results.Ok(new { created, note = "Stub adapter only. Real scanners arrive in P19." });
        }).RequirePermission(VulnManage);
    }

    private static void MapRisks(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/security/risks", async (
            int? page, int? pageSize, string? search, string? status, SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRisksAsync(page ?? 1, pageSize ?? 25, search, ParseEnum<RiskStatus>(status), ct)))
            .RequirePermission(SecDashboard);

        endpoints.MapGet("/api/v1/security/risks/{id:guid}", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            RiskDto? item = await svc.GetRiskAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(SecDashboard);

        endpoints.MapPost("/api/v1/security/risks", async (
            CreateRiskRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.Treatment ?? "Mitigate", true, out RiskTreatment treatment))
                return Validation("Valid treatment required.");
            try
            {
                RiskDto created = await svc.CreateRiskAsync(
                    req.Title, req.Description, req.Category, req.OwnerUserId ?? session.Id,
                    req.Likelihood ?? 3, req.Impact ?? 3, treatment, req.ConfigurationItemId,
                    req.BusinessServiceId, req.InternalControlId, req.TreatmentPlan, req.TargetDate, ct);
                return Results.Created($"/api/v1/security/risks/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);

        endpoints.MapPut("/api/v1/security/risks/{id:guid}", async (
            Guid id, UpdateRiskRequest req, SecurityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Treatment, true, out RiskTreatment treatment))
                return Validation("Valid treatment required.");
            try
            {
                return Results.Ok(await svc.UpdateRiskAsync(
                    id, req.Likelihood, req.Impact, req.ResidualLikelihood, req.ResidualImpact,
                    treatment, req.TreatmentPlan, req.TargetDate, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);

        endpoints.MapPost("/api/v1/security/risks/{id:guid}/transition", async (
            Guid id, TransitionRequest req, SecurityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out RiskStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.TransitionRiskAsync(id, status, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);

        endpoints.MapPost("/api/v1/security/risks/{id:guid}/links", async (
            Guid id, RiskLinkRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                await svc.AddRiskLinkAsync(id, req.TargetType, req.TargetId, session.Id, ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);
    }

    private static void MapExceptions(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/security/exceptions", async (
            string? status, SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListExceptionsAsync(ParseEnum<PolicyExceptionStatus>(status), ct)))
            .RequirePermission(SecDashboard);

        endpoints.MapGet("/api/v1/security/exceptions/{id:guid}", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            PolicyExceptionDto? item = await svc.GetExceptionAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(SecDashboard);

        endpoints.MapPost("/api/v1/security/exceptions", async (
            CreateExceptionRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                PolicyExceptionDto created = await svc.CreateExceptionAsync(
                    req.Title, req.Reason, session.Id, req.StartAtUtc, req.ExpiresAtUtc,
                    req.ManagedDocumentId, req.InternalControlId, req.RiskId, req.ConfigurationItemId,
                    req.OwnerUserId, req.CompensatingControls, ct);
                return Results.Created($"/api/v1/security/exceptions/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);

        endpoints.MapPost("/api/v1/security/exceptions/{id:guid}/submit", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.SubmitExceptionAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);

        endpoints.MapPost("/api/v1/security/exceptions/{id:guid}/approve", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try { return Results.Ok(await svc.ApproveExceptionAsync(id, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ExceptionApprove);

        endpoints.MapPost("/api/v1/security/exceptions/{id:guid}/reject", async (
            Guid id, RejectRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try { return Results.Ok(await svc.RejectExceptionAsync(id, session.Id, req.Reason, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ExceptionApprove);

        endpoints.MapPost("/api/v1/security/exceptions/{id:guid}/close", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.CloseExceptionAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(RiskManage);
    }

    private static void MapPentests(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/security/pentests", async (SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPentestsAsync(ct))).RequirePermission(SecDashboard);

        endpoints.MapGet("/api/v1/security/pentests/{id:guid}", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            PenetrationTestDto? item = await svc.GetPentestAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(SecDashboard);

        endpoints.MapGet("/api/v1/security/pentests/{id:guid}/findings", async (Guid id, SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPentestFindingsAsync(id, ct))).RequirePermission(SecDashboard);

        endpoints.MapPost("/api/v1/security/pentests", async (CreatePentestRequest req, SecurityService svc, CancellationToken ct) =>
        {
            try
            {
                PenetrationTestDto created = await svc.CreatePentestAsync(
                    req.Title, req.ScopeSummary, req.Provider, req.StartDate, req.EndDate, ct);
                return Results.Created($"/api/v1/security/pentests/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);

        endpoints.MapPost("/api/v1/security/pentests/{id:guid}/transition", async (
            Guid id, TransitionRequest req, SecurityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out PenetrationTestStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.TransitionPentestAsync(id, status, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);

        endpoints.MapPost("/api/v1/security/pentests/{id:guid}/findings", async (
            Guid id, CreatePentestFindingRequest req, SecurityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Severity ?? "Medium", true, out VulnerabilitySeverity severity))
                return Validation("Valid severity required.");
            try
            {
                return Results.Ok(await svc.AddPentestFindingAsync(
                    id, req.Title, req.Description, severity, req.ConfigurationItemId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);

        endpoints.MapPost("/api/v1/security/pentest-findings/{findingId:guid}/link", async (
            Guid findingId, LinkPentestFindingRequest req, SecurityService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.LinkPentestFindingAsync(
                    findingId, req.VulnerabilityId, req.AuditFindingId, req.EvidenceId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VulnManage);
    }

    private static void MapAwareness(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/security/awareness", async (SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCampaignsAsync(ct))).RequirePermission(SecDashboard);

        endpoints.MapPost("/api/v1/security/awareness/modules/seed", async (
            SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            await workflow.EnsureStarterModulesAsync(ct);
            return Results.Ok(await workflow.ListModulesAsync(includeInactive: true, ct));
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapGet("/api/v1/security/awareness/modules", async (
            bool? includeInactive, SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
            Results.Ok(await workflow.ListModulesAsync(includeInactive == true, ct)))
            .RequirePermission(SecDashboard);

        endpoints.MapPost("/api/v1/security/awareness/modules/{id:guid}/activate", async (
            Guid id, SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                await workflow.ActivateModuleAsync(id, ct);
                return Results.Ok();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapPost("/api/v1/security/awareness/campaigns", async (
            CreateModuleCampaignRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (req.ModuleId == Guid.Empty) return Validation("ModuleId is required.");
            try
            {
                return Results.Ok(await workflow.CreateCampaignForModuleAsync(
                    req.ModuleId, req.Title, session.Id, req.DueAtUtc, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapPost("/api/v1/security/awareness/{id:guid}/assign-open", async (
            Guid id, AssignOpenRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityAwarenessWorkflowService workflow, SecurityAwarenessNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                IReadOnlyList<AwarenessCompletionDto> created = await workflow.OpenAndAssignAsync(
                    id, req.AllEmployees == true, req.UserIds, session.Id, ct);
                await notifications.NotifyAssignmentsAsync(created, ct);
                return Results.Ok(new { assigned = created.Count, items = created });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapPost("/api/v1/security/awareness/{id:guid}/close", async (
            Guid id, SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                await workflow.CloseCampaignAsync(id, ct);
                return Results.Ok();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapGet("/api/v1/security/awareness/{id:guid}/completions/export.csv", async (
            Guid id, SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                string csv = await workflow.ExportCompletionsCsvAsync(id, ct);
                return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv",
                    $"awareness-completion-{id:N}.csv");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        // Legacy thin campaign API (kept for compatibility)
        endpoints.MapPost("/api/v1/security/awareness", async (
            CreateCampaignRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                return Results.Ok(await svc.CreateCampaignAsync(
                    req.Title, req.OwnerUserId ?? session.Id, req.StartsAtUtc ?? DateTimeOffset.UtcNow,
                    req.Description, req.DueAtUtc, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapPost("/api/v1/security/awareness/{id:guid}/open", async (Guid id, SecurityService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.OpenCampaignAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapPost("/api/v1/security/awareness/{id:guid}/assign", async (
            Guid id, AssignRequest req, SecurityService svc, SecurityAwarenessNotificationService notifications,
            CancellationToken ct) =>
        {
            try
            {
                AwarenessCompletionDto item = await svc.AssignCompletionAsync(id, req.UserId, ct);
                await notifications.NotifyAssignmentsAsync([item], ct);
                return Results.Ok(item);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecAwarenessManage);

        endpoints.MapGet("/api/v1/security/awareness/{id:guid}/completions", async (Guid id, SecurityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCompletionsAsync(id, ct))).RequirePermission(SecDashboard);

        endpoints.MapPost("/api/v1/security/awareness/{id:guid}/complete", async (
            Guid id, CompleteAwarenessRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                return Results.Ok(await svc.CompleteAwarenessAsync(
                    id, req.UserId ?? session.Id, req.EvidenceId, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(SecDashboard);
    }

    private static void MapMeSecurity(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/security/awareness/summary", async (
            ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await workflow.GetEmployeeSummaryAsync(session.Id, ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/security/awareness", async (
            string? filter, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await workflow.ListEmployeeAssignmentsAsync(session.Id, filter ?? "outstanding", ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/security/awareness/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            AwarenessModuleDto? content = await workflow.GetAssignmentContentAsync(session.Id, id, ct);
            if (content is null) return Results.NotFound();
            IReadOnlyList<EmployeeAwarenessItemDto> items =
                await workflow.ListEmployeeAssignmentsAsync(session.Id, "all", ct);
            EmployeeAwarenessItemDto? assignment = items.FirstOrDefault(x => x.AssignmentId == id);
            return Results.Ok(new { assignment, module = content });
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/security/awareness/{id:guid}/submit", async (
            Guid id, SubmitQuizRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            SecurityAwarenessWorkflowService workflow, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (req.Answers is null || req.Answers.Count == 0)
                return Validation("Answer all questions before submitting.");
            try
            {
                Dictionary<Guid, Guid> answers = req.Answers.ToDictionary(x => x.QuestionId, x => x.OptionId);
                return Results.Ok(await workflow.SubmitQuizAsync(session.Id, id, answers, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/security/concerns", async (
            ReportSecurityConcernRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            TicketService tickets, TicketNotificationService ticketNotifications, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (string.IsNullOrWhiteSpace(req.CategoryKey) || string.IsNullOrWhiteSpace(req.Description))
                return Validation("Category and description are required.");

            string category = req.CategoryKey.Trim().ToLowerInvariant();
            string title = BuildSecurityConcernTitle(category, req.Title);
            string description = BuildSecurityConcernDescription(category, req);

            try
            {
                var created = await tickets.CreateSecurityConcernAsync(
                    title, description, session.Id, category, req.ConfigurationItemId,
                    TicketPriority.High, ct);
                var dto = await tickets.GetForRequesterAsync(created.Id, session.Id, ct);
                if (dto is not null)
                    await ticketNotifications.NotifyTicketCreatedAsync(dto, ct);
                return Results.Created($"/api/v1/me/tickets/{created.Id}", dto);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequireAuthorization();
    }

    private static string BuildSecurityConcernTitle(string category, string? title)
    {
        if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
        return category switch
        {
            "phishing" => "Suspicious or phishing email",
            "account" => "Suspicious login or account activity",
            "lost_device" => "Lost or stolen device",
            "malware" => "Virus or malware warning",
            "data_disclosure" => "Information sent to the wrong person",
            "suspicious_link" => "Suspicious website or link",
            "unauthorized_access" => "Unauthorized access or activity",
            _ => "Security concern",
        };
    }

    private static string BuildSecurityConcernDescription(string category, ReportSecurityConcernRequest req)
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine(req.Description.Trim());
        if (!string.IsNullOrWhiteSpace(req.NoticedAtUtc))
            sb.AppendLine().Append("Noticed: ").Append(req.NoticedAtUtc.Trim());
        if (!string.IsNullOrWhiteSpace(req.AffectedDeviceOrService))
            sb.AppendLine().Append("Affected: ").Append(req.AffectedDeviceOrService.Trim());
        if (category is "phishing" or "suspicious_link")
        {
            if (!string.IsNullOrWhiteSpace(req.Sender))
                sb.AppendLine().Append("Sender: ").Append(req.Sender.Trim());
            if (!string.IsNullOrWhiteSpace(req.Subject))
                sb.AppendLine().Append("Subject: ").Append(req.Subject.Trim());
            if (!string.IsNullOrWhiteSpace(req.SuspiciousReason))
                sb.AppendLine().Append("Why suspicious: ").Append(req.SuspiciousReason.Trim());
        }

        sb.AppendLine().AppendLine()
            .AppendLine("Note: Employee was advised not to include passwords, OTP codes, or credentials.");
        return sb.ToString();
    }

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static IResult SessionUnavailable() =>
        Results.Json(new { error = new { code = "session_unavailable", message = "No active ITMG user session." } },
            statusCode: StatusCodes.Status403Forbidden);

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record CreateVulnRequest(
        string Title, Guid ConfigurationItemId, string? Source, string? Severity, string? Description,
        string? ExternalReference, DateTimeOffset? DetectedAtUtc, DateTimeOffset? DueAtUtc, Guid? OwnerUserId);
    private sealed record VulnTransitionRequest(
        string Status, string? ResolutionSummary, string? AcceptedRiskReason, Guid? ExceptionId);
    private sealed record RemediationLinkRequest(string LinkType, Guid TargetId, string? Notes);
    private sealed record CreateRiskRequest(
        string Title, string Description, string Category, Guid? OwnerUserId, int? Likelihood, int? Impact,
        string? Treatment, Guid? ConfigurationItemId, Guid? BusinessServiceId, Guid? InternalControlId,
        string? TreatmentPlan, DateOnly? TargetDate);
    private sealed record UpdateRiskRequest(
        int Likelihood, int Impact, int? ResidualLikelihood, int? ResidualImpact, string Treatment,
        string? TreatmentPlan, DateOnly? TargetDate);
    private sealed record TransitionRequest(string Status);
    private sealed record RiskLinkRequest(string TargetType, Guid TargetId);
    private sealed record CreateExceptionRequest(
        string Title, string Reason, DateTimeOffset StartAtUtc, DateTimeOffset ExpiresAtUtc,
        Guid? ManagedDocumentId, Guid? InternalControlId, Guid? RiskId, Guid? ConfigurationItemId,
        Guid? OwnerUserId, string? CompensatingControls);
    private sealed record RejectRequest(string Reason);
    private sealed record CreatePentestRequest(
        string Title, string ScopeSummary, string? Provider, DateOnly? StartDate, DateOnly? EndDate);
    private sealed record CreatePentestFindingRequest(
        string Title, string Description, string? Severity, Guid? ConfigurationItemId);
    private sealed record LinkPentestFindingRequest(Guid? VulnerabilityId, Guid? AuditFindingId, Guid? EvidenceId);
    private sealed record CreateCampaignRequest(
        string Title, string? Description, Guid? OwnerUserId, DateTimeOffset? StartsAtUtc, DateTimeOffset? DueAtUtc);
    private sealed record CreateModuleCampaignRequest(Guid ModuleId, string? Title, DateTimeOffset? DueAtUtc);
    private sealed record AssignOpenRequest(bool? AllEmployees, Guid[]? UserIds);
    private sealed record AssignRequest(Guid UserId);
    private sealed record CompleteAwarenessRequest(Guid? UserId, Guid? EvidenceId, string? Notes);
    private sealed record SubmitQuizRequest(List<QuizAnswer>? Answers);
    private sealed record QuizAnswer(Guid QuestionId, Guid OptionId);
    private sealed record ReportSecurityConcernRequest(
        string CategoryKey,
        string Description,
        string? Title,
        string? NoticedAtUtc,
        string? AffectedDeviceOrService,
        Guid? ConfigurationItemId,
        string? Sender,
        string? Subject,
        string? SuspiciousReason);
}

public sealed class SecurityAwarenessNotificationService(
    INotificationService notifications,
    IEmailQueue emailQueue,
    IdentityDbContext identityDb,
    SecurityService security,
    ILogger<SecurityAwarenessNotificationService> logger)
{
    public const string ResourceType = "AwarenessAssignment";

    public async Task NotifyAssignmentsAsync(IReadOnlyList<AwarenessCompletionDto> assignments, CancellationToken ct)
    {
        if (assignments.Count == 0) return;
        Dictionary<Guid, AwarenessCampaignDto> campaigns = (await security.ListCampaignsAsync(ct))
            .ToDictionary(x => x.Id);
        foreach (AwarenessCompletionDto item in assignments)
        {
            campaigns.TryGetValue(item.CampaignId, out AwarenessCampaignDto? campaign);
            string title = campaign?.Title ?? "Security awareness";
            string due = item.DueAtUtc is DateTimeOffset d
                ? $" Due by {d:u}."
                : campaign?.DueAtUtc is DateTimeOffset cd
                    ? $" Due by {cd:u}."
                    : string.Empty;
            string actionUrl = $"/employee/security/awareness/{item.Id}";
            string subject = $"QEC Security Awareness Required: {title}";
            string body =
                $"Please complete your QEC security awareness assignment.\n\n" +
                $"Module: {title}\n" +
                $"Estimated time: a few minutes.{due}\n\n" +
                "Start security awareness in ITMG.";
            await NotifyEmployeeAsync(
                item.UserId, "awareness.assigned", NotificationSeverity.Warning,
                subject, body, item.Id, actionUrl, ct);
        }
    }

    public async Task NotifyReminderAsync(AwarenessReminderCandidate candidate, CancellationToken ct)
    {
        string actionUrl = $"/employee/security/awareness/{candidate.AssignmentId}";
        string type = candidate.ReminderKind switch
        {
            SecurityAwarenessWorkflowService.ReminderOverdue => "awareness.overdue",
            SecurityAwarenessWorkflowService.ReminderDue1 => "awareness.due_soon",
            _ => "awareness.due_soon",
        };
        string title = candidate.ReminderKind == SecurityAwarenessWorkflowService.ReminderOverdue
            ? $"Security awareness overdue: {candidate.Title}"
            : $"Security awareness due soon: {candidate.Title}";
        string message = candidate.DueAtUtc is DateTimeOffset due
            ? $"\"{candidate.Title}\" is due {due:u}."
            : $"\"{candidate.Title}\" still needs to be completed.";
        await NotifyEmployeeAsync(
            candidate.UserId, type,
            candidate.ReminderKind == SecurityAwarenessWorkflowService.ReminderOverdue
                ? NotificationSeverity.Warning
                : NotificationSeverity.Info,
            title, message, candidate.AssignmentId, actionUrl, ct);
    }

    private async Task NotifyEmployeeAsync(
        Guid recipientUserId,
        string type,
        NotificationSeverity severity,
        string title,
        string message,
        Guid assignmentId,
        string actionUrl,
        CancellationToken ct)
    {
        try
        {
            await notifications.CreateAsync(
                recipientUserId, type, severity, title, message, ResourceType, assignmentId, actionUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed awareness notification {Type} for {UserId}", type, recipientUserId);
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
            logger.LogWarning(ex, "Failed to enqueue awareness email for {UserId}", recipientUserId);
        }
    }
}

public sealed class SecurityAwarenessReminderJob(
    SecurityAwarenessWorkflowService workflow,
    SecurityAwarenessNotificationService notifications)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AwarenessReminderCandidate> due = await workflow.FindReminderCandidatesAsync(cancellationToken);
        foreach (AwarenessReminderCandidate item in due)
        {
            await notifications.NotifyReminderAsync(item, cancellationToken);
            await workflow.MarkReminderSentAsync(item.AssignmentId, item.UserId, item.ReminderKind, cancellationToken);
        }

        return due.Count;
    }
}

public sealed class SecurityExceptionExpiryJob(SecurityService security)
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        security.MarkExpiredExceptionsJobAsync(cancellationToken);
}

public sealed class SecurityExceptionReminderJob(
    SecurityService security,
    INotificationService notifications,
    IClock clock)
{
    private static readonly int[] Thresholds = [30, 14, 7, 1, 0];

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        int sent = 0;
        DateTimeOffset now = clock.UtcNow;
        foreach (var entity in await security.GetExpiringExceptionCandidatesAsync(cancellationToken))
        {
            int days = (int)Math.Floor((entity.ExpiresAtUtc - now).TotalDays);
            foreach (int threshold in Thresholds)
            {
                bool match = threshold == 0 ? days < 0 : days == threshold;
                if (!match) continue;
                string eventKey = threshold == 0 ? "security.exception_overdue" : $"security.exception_due_{threshold}";
                if (await security.HasExceptionNotificationAsync(entity.Id, eventKey, cancellationToken)) continue;
                Guid recipient = entity.OwnerUserId ?? entity.RequestedByUserId;
                await notifications.CreateAsync(
                    recipient,
                    eventKey,
                    threshold is 0 or 1 or 7 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    threshold == 0
                        ? $"Exception expired: {entity.ExceptionNumber}"
                        : $"Exception expires in {days} day(s): {entity.ExceptionNumber}",
                    entity.Title,
                    "PolicyException",
                    entity.Id,
                    $"/it/security/exceptions",
                    cancellationToken);
                await security.RecordExceptionNotificationAsync(entity.Id, eventKey, cancellationToken);
                sent++;
                break;
            }
        }

        return sent;
    }
}
