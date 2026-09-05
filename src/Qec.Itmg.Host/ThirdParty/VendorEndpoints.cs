using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.AccessManagement.Domain;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Identity.Admin;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;
using Qec.Itmg.ThirdParty.Domain;
using Qec.Itmg.ThirdParty.Services;

namespace Qec.Itmg.Host.ThirdParty;

public static class VendorEndpoints
{
    public const string VendorRead = "vendor.read";
    public const string VendorManage = "vendor.manage";
    public const string ContractManage = "contract.manage";
    public const string VendorAssess = "vendor.assess";

    public static IEndpointRouteBuilder MapVendorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapDashboard(endpoints);
        MapVendors(endpoints);
        MapContracts(endpoints);
        MapAssessments(endpoints);
        MapAccess(endpoints);
        return endpoints;
    }

    private static void MapDashboard(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/vendors/dashboard", async (
            VendorService vendors, ManagedAccountService accounts, CancellationToken ct) =>
        {
            int privileged = await accounts.CountActiveWithVendorAsync(ct);
            return Results.Ok(await vendors.GetDashboardAsync(privileged, ct));
        }).RequirePermission(VendorRead);
    }

    private static void MapVendors(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/vendors", async (string? search, string? status, VendorService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListVendorsAsync(search, ParseEnum<VendorStatus>(status), ct)))
            .RequirePermission(VendorRead);

        endpoints.MapGet("/api/v1/vendors/{id:guid}", async (Guid id, VendorService svc, CancellationToken ct) =>
        {
            VendorDto? item = await svc.GetVendorAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors", async (CreateVendorBody req, VendorService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Criticality ?? "Medium", true, out VendorCriticality criticality))
                return Validation("Valid criticality required.");
            try
            {
                VendorDto created = await svc.CreateVendorAsync(
                    req.Name, criticality, req.LegalName, req.ServiceDescription,
                    req.PrimaryContactName, req.PrimaryContactEmail, req.PrimaryContactPhone,
                    req.OwnerUserId, req.RiskId, ct);
                return Results.Created($"/api/v1/vendors/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);

        endpoints.MapPut("/api/v1/vendors/{id:guid}", async (Guid id, UpdateVendorBody req, VendorService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Criticality, true, out VendorCriticality criticality))
                return Validation("Valid criticality required.");
            if (!Enum.TryParse(req.Status, true, out VendorStatus status))
                return Validation("Valid status required.");
            try
            {
                return Results.Ok(await svc.UpdateVendorAsync(
                    id, req.Name, criticality, status, req.LegalName, req.ServiceDescription,
                    req.PrimaryContactName, req.PrimaryContactEmail, req.PrimaryContactPhone,
                    req.OwnerUserId, req.RiskId, req.RowVersion, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);

        endpoints.MapGet("/api/v1/vendors/{id:guid}/contacts", async (Guid id, VendorService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListContactsAsync(id, ct))).RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors/{id:guid}/contacts", async (Guid id, CreateContactBody req, VendorService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.AddContactAsync(id, req.Name, req.Email, req.Phone, req.Role, req.IsPrimary, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);

        endpoints.MapGet("/api/v1/vendors/{id:guid}/links", async (Guid id, VendorService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListLinksAsync(id, ct))).RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors/{id:guid}/links", async (
            Guid id, LinkBody req, ClaimsPrincipal principal, ICurrentUserService currentUser, VendorService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out VendorLinkTargetType targetType))
                return Validation("Valid targetType required.");
            try { return Results.Ok(await svc.AddLinkAsync(id, targetType, req.TargetId, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);

        endpoints.MapGet("/api/v1/vendors/{id:guid}/cis", async (
            Guid id, ConfigurationItemService cis, VendorService vendors, CancellationToken ct) =>
        {
            if (await vendors.GetVendorAsync(id, ct) is null) return Results.NotFound();
            return Results.Ok(await cis.ListByVendorAsync(id, ct));
        }).RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors/{id:guid}/cis/{ciId:guid}", async (
            Guid id, Guid ciId, SetCiVendorBody req, ConfigurationItemService cis, VendorService vendors, CancellationToken ct) =>
        {
            if (await vendors.GetVendorAsync(id, ct) is null) return Results.NotFound();
            try
            {
                return Results.Ok(await cis.SetVendorAsync(ciId, id, req.RowVersion, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);
    }

    private static void MapContracts(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/vendors/contracts", async (Guid? vendorId, string? status, VendorService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListContractsAsync(vendorId, ParseEnum<ContractStatus>(status), ct)))
            .RequirePermission(VendorRead);

        endpoints.MapGet("/api/v1/vendors/contracts/{id:guid}", async (Guid id, VendorService svc, CancellationToken ct) =>
        {
            ContractDto? item = await svc.GetContractAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors/{vendorId:guid}/contracts", async (
            Guid vendorId, CreateContractBody req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            VendorService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                ContractDto created = await svc.CreateContractAsync(
                    vendorId, req.Title, req.OwnerUserId ?? session.Id, req.StartDate,
                    req.ContractType, req.EndDate, req.RenewalDate, req.AutoRenew,
                    req.SlaReference, req.ManagedDocumentId, req.Notes, ct);
                return Results.Created($"/api/v1/vendors/contracts/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ContractManage);

        endpoints.MapPost("/api/v1/vendors/contracts/{id:guid}/transition", async (
            Guid id, TransitionBody req, VendorService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out ContractStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.TransitionContractAsync(id, status, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ContractManage);
    }

    private static void MapAssessments(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/vendors/assessments", async (Guid? vendorId, string? status, VendorService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAssessmentsAsync(vendorId, ParseEnum<VendorAssessmentStatus>(status), ct)))
            .RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors/{vendorId:guid}/assessments", async (
            Guid vendorId, CreateAssessmentBody req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            VendorService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.AssessmentType ?? "DueDiligence", true, out VendorAssessmentType type))
                return Validation("Valid assessmentType required.");
            try
            {
                return Results.Ok(await svc.CreateAssessmentAsync(
                    vendorId, type, req.OwnerUserId ?? session.Id, req.ReviewerUserId,
                    req.ScheduledAtUtc, req.DueAtUtc, req.RiskId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorAssess);

        endpoints.MapPost("/api/v1/vendors/assessments/{id:guid}/transition", async (
            Guid id, TransitionAssessmentBody req, VendorService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out VendorAssessmentStatus status))
                return Validation("Valid status required.");
            VendorAssessmentResult? result = null;
            if (!string.IsNullOrWhiteSpace(req.Result))
            {
                if (!Enum.TryParse(req.Result, true, out VendorAssessmentResult parsed))
                    return Validation("Valid result required.");
                result = parsed;
            }
            try { return Results.Ok(await svc.TransitionAssessmentAsync(id, status, result, req.Summary, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorAssess);
    }

    private static void MapAccess(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/vendors/{id:guid}/access", async (
            Guid id, VendorService vendors, AccessCaseService cases, ManagedAccountService accounts,
            AdminUsersService users, CancellationToken ct) =>
        {
            if (await vendors.GetVendorAsync(id, ct) is null) return Results.NotFound();
            return Results.Ok(new
            {
                accessCases = await cases.ListByVendorAsync(id, ct),
                managedAccounts = await accounts.ListByVendorAsync(id, ct),
                contacts = await vendors.ListContactsAsync(id, ct),
                vendorUsers = await users.ListByUserTypeAsync("Vendor", ct),
            });
        }).RequirePermission(VendorRead);

        endpoints.MapPost("/api/v1/vendors/{id:guid}/access/cases/{caseId:guid}", async (
            Guid id, Guid caseId, AccessCaseService cases, VendorService vendors, CancellationToken ct) =>
        {
            if (await vendors.GetVendorAsync(id, ct) is null) return Results.NotFound();
            try { return Results.Ok(await cases.SetVendorAsync(caseId, id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);

        endpoints.MapPost("/api/v1/vendors/{id:guid}/access/accounts/{accountId:guid}", async (
            Guid id, Guid accountId, ManagedAccountService accounts, VendorService vendors, CancellationToken ct) =>
        {
            if (await vendors.GetVendorAsync(id, ct) is null) return Results.NotFound();
            try { return Results.Ok(await accounts.SetVendorAsync(accountId, id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(VendorManage);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record CreateVendorBody(
        string Name, string? Criticality, string? LegalName, string? ServiceDescription,
        string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
        Guid? OwnerUserId, Guid? RiskId);
    private sealed record UpdateVendorBody(
        string Name, string Criticality, string Status, string? LegalName, string? ServiceDescription,
        string? PrimaryContactName, string? PrimaryContactEmail, string? PrimaryContactPhone,
        Guid? OwnerUserId, Guid? RiskId, string RowVersion);
    private sealed record CreateContactBody(string Name, string? Email, string? Phone, string? Role, bool IsPrimary);
    private sealed record CreateContractBody(
        string Title, Guid? OwnerUserId, DateOnly StartDate, string? ContractType,
        DateOnly? EndDate, DateOnly? RenewalDate, bool AutoRenew, string? SlaReference,
        Guid? ManagedDocumentId, string? Notes);
    private sealed record CreateAssessmentBody(
        string? AssessmentType, Guid? OwnerUserId, Guid? ReviewerUserId,
        DateTimeOffset? ScheduledAtUtc, DateTimeOffset? DueAtUtc, Guid? RiskId);
    private sealed record TransitionBody(string Status);
    private sealed record TransitionAssessmentBody(string Status, string? Result, string? Summary);
    private sealed record LinkBody(string TargetType, Guid TargetId);
    private sealed record SetCiVendorBody(string RowVersion);
}

public sealed class VendorReminderJob(
    VendorService vendors,
    INotificationService notifications,
    IClock clock)
{
    private static readonly int[] Thresholds = [90, 60, 30, 14, 7, 1, 0];

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        int sent = 0;
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        DateTimeOffset now = clock.UtcNow;

        foreach (var contract in await vendors.GetContractsForNotificationsAsync(cancellationToken))
        {
            DateOnly? due = contract.RenewalDate ?? contract.EndDate;
            if (due is not DateOnly d) continue;
            int days = d.DayNumber - today.DayNumber;
            foreach (int threshold in Thresholds)
            {
                bool match = threshold == 0 ? days < 0 : days == threshold;
                if (!match) continue;
                string eventKey = threshold == 0
                    ? $"tpm.contract_expired_{d:yyyyMMdd}"
                    : $"tpm.contract_due_{threshold}_{d:yyyyMMdd}";
                if (await vendors.HasNotificationAsync(contract.Id, eventKey, cancellationToken)) continue;
                Guid notifyUser = contract.OwnerUserId;
                await notifications.CreateAsync(
                    notifyUser, eventKey,
                    threshold is 0 or 1 or 7 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    threshold == 0
                        ? $"Contract expired: {contract.ContractNumber}"
                        : $"Contract in {days}d: {contract.ContractNumber}",
                    contract.Title, "Contract", contract.Id, "/it/vendors", cancellationToken);
                await vendors.RecordNotificationAsync(contract.Id, eventKey, cancellationToken);
                sent++;
                break;
            }
        }

        foreach (var assessment in await vendors.GetAssessmentsForNotificationsAsync(cancellationToken))
        {
            if (assessment.DueAtUtc is not DateTimeOffset due) continue;
            int days = (int)Math.Floor((due - now).TotalDays);
            foreach (int threshold in Thresholds)
            {
                bool match = threshold == 0 ? days < 0 : days == threshold;
                if (!match) continue;
                string eventKey = threshold == 0
                    ? $"tpm.assessment_overdue_{due:yyyyMMdd}"
                    : $"tpm.assessment_due_{threshold}_{due:yyyyMMdd}";
                if (await vendors.HasNotificationAsync(assessment.Id, eventKey, cancellationToken)) continue;
                await notifications.CreateAsync(
                    assessment.OwnerUserId, eventKey,
                    threshold is 0 or 1 or 7 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    threshold == 0
                        ? $"Assessment overdue: {assessment.AssessmentNumber}"
                        : $"Assessment due in {days}d: {assessment.AssessmentNumber}",
                    assessment.AssessmentType.ToString(), "VendorAssessment", assessment.Id, "/it/vendors",
                    cancellationToken);
                await vendors.RecordNotificationAsync(assessment.Id, eventKey, cancellationToken);
                sent++;
                break;
            }
        }

        return sent;
    }
}
