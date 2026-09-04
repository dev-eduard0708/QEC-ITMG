using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Qec.Itmg.Cmdb.Persistence;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Governance.Domain;
using Qec.Itmg.Governance.Services;
using Qec.Itmg.Identity.Authorization;
namespace Qec.Itmg.Host.Governance;

public static class GovernanceEndpoints
{
    public const string GovRead = "gov.read";
    public const string GovManage = "gov.manage";
    public const string ControlRead = "control.read";
    public const string ControlManage = "control.manage";

    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapOrganization(endpoints);
        MapRegisters(endpoints);
        MapControls(endpoints);
        return endpoints;
    }

    private static void MapOrganization(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/governance").RequirePermission(GovRead);
        read.MapGet("/profile", async (OrganizationChartService svc, CancellationToken ct) =>
        {
            OrganizationProfileDto? profile = await svc.GetProfileAsync(ct);
            return profile is null ? Results.Ok((object?)null) : Results.Ok(profile);
        });
        read.MapGet("/organization/units", async (OrganizationChartService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListUnitsAsync(ct)));
        read.MapGet("/organization/units/{id:guid}", async (Guid id, OrganizationChartService svc, CancellationToken ct) =>
        {
            OrganizationalUnitDto? item = await svc.GetUnitAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        endpoints.MapPut("/api/v1/governance/profile", async (
            UpsertProfileRequest req, OrganizationChartService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpsertProfileAsync(req.LegalName, req.Timezone, req.ClassificationScheme, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(GovManage);

        endpoints.MapPost("/api/v1/governance/organization/units", async (
            CreateUnitRequest req, OrganizationChartService svc, CancellationToken ct) =>
        {
            try
            {
                OrganizationalUnitDto created = await svc.CreateUnitAsync(
                    req.Name, req.Code, req.ParentId, req.ManagerUserId, req.Description, ct);
                return Results.Created($"/api/v1/governance/organization/units/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(GovManage);

        endpoints.MapPut("/api/v1/governance/organization/units/{id:guid}", async (
            Guid id, UpdateUnitRequest req, OrganizationChartService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateUnitAsync(
                    id, req.Name, req.Code, req.ParentId, req.ManagerUserId, req.Description, req.IsActive, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(GovManage);

        endpoints.MapPost("/api/v1/governance/organization/units/{id:guid}/members", async (
            Guid id, MemberRequest req, OrganizationChartService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.AssignMemberAsync(id, req.UserId, ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(GovManage);

        endpoints.MapDelete("/api/v1/governance/organization/units/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId, OrganizationChartService svc, CancellationToken ct) =>
        {
            await svc.RemoveMemberAsync(id, userId, ct);
            return Results.NoContent();
        }).RequirePermission(GovManage);
    }

    private static void MapRegisters(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/governance/registers").RequirePermission(GovRead);

        group.MapGet("/applications", async (
            string? search, ConfigurationItemService cis, CmdbDbContext cmdb, CancellationToken ct) =>
            Results.Ok(await BuildCiRegisterAsync(cis, cmdb, ["application"], search, ct)));

        group.MapGet("/infrastructure", async (
            string? search, ConfigurationItemService cis, CmdbDbContext cmdb, CancellationToken ct) =>
            Results.Ok(await BuildCiRegisterAsync(cis, cmdb, ["server", "laptop"], search, ct)));

        group.MapGet("/interfaces", async (
            string? search, ConfigurationItemService cis, CmdbDbContext cmdb, CancellationToken ct) =>
            Results.Ok(await BuildCiRegisterAsync(cis, cmdb, ["interface", "integration"], search, ct)));

        group.MapGet("/business-services", async (
            string? search, BusinessServiceService services, CmdbDbContext cmdb, CancellationToken ct) =>
        {
            IReadOnlyList<BusinessServiceDto> all = await services.ListAsync(ct);
            IEnumerable<BusinessServiceDto> filtered = all;
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                filtered = all.Where(x =>
                    x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (x.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            List<BusinessServiceDto> list = filtered.ToList();
            Guid[] ids = list.Select(x => x.Id).ToArray();
            var links = await cmdb.BusinessServiceConfigurationItems.AsNoTracking()
                .Where(x => ids.Contains(x.BusinessServiceId))
                .ToListAsync(ct);
            var byService = links.GroupBy(x => x.BusinessServiceId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ConfigurationItemId).ToList());

            return Results.Ok(list.Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.OwnerUserId,
                s.Criticality,
                s.RtoMinutes,
                s.RpoMinutes,
                s.IsActive,
                s.UpdatedAtUtc,
                LinkedConfigurationItemIds = byService.GetValueOrDefault(s.Id) ?? [],
            }));
        });
    }

    private static async Task<IReadOnlyList<object>> BuildCiRegisterAsync(
        ConfigurationItemService cis,
        CmdbDbContext cmdb,
        IReadOnlyList<string> typeKeys,
        string? search,
        CancellationToken ct)
    {
        HashSet<string> keys = new(typeKeys, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<ConfigurationItemDto> all = await cis.ListConfigurationItemsAsync(search, ct);
        List<ConfigurationItemDto> filtered = all.Where(x => keys.Contains(x.CiTypeKey)).ToList();
        Guid[] ids = filtered.Select(x => x.Id).ToArray();

        var relationships = await cmdb.CiRelationships.AsNoTracking()
            .Where(x => ids.Contains(x.SourceCiId) || ids.Contains(x.TargetCiId))
            .ToListAsync(ct);
        var serviceLinks = await cmdb.BusinessServiceConfigurationItems.AsNoTracking()
            .Where(x => ids.Contains(x.ConfigurationItemId))
            .ToListAsync(ct);

        return filtered.Select(ci =>
        {
            var rels = relationships
                .Where(r => r.SourceCiId == ci.Id || r.TargetCiId == ci.Id)
                .Select(r => new
                {
                    r.Id,
                    r.SourceCiId,
                    r.TargetCiId,
                    RelationshipType = r.RelationshipType.ToString(),
                })
                .ToList();
            var serviceIds = serviceLinks
                .Where(l => l.ConfigurationItemId == ci.Id)
                .Select(l => l.BusinessServiceId)
                .Distinct()
                .ToList();
            return (object)new
            {
                ci.Id,
                ci.CiNumber,
                ci.Name,
                ci.CiTypeKey,
                ci.CiTypeName,
                ci.Status,
                ci.Criticality,
                ci.OwnerUserId,
                ci.UpdatedAtUtc,
                LinkedBusinessServiceIds = serviceIds,
                Relationships = rels,
            };
        }).ToList();
    }

    private static void MapControls(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/controls").RequirePermission(ControlRead);
        read.MapGet("/domains", (InternalControlService svc) => Results.Ok(svc.ListDomains()));
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? domain, string? status,
            InternalControlService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(
                page ?? 1, pageSize ?? 25, search, domain, ParseEnum<ControlStatus>(status), ct)));
        read.MapGet("/{id:guid}", async (Guid id, InternalControlService svc, CancellationToken ct) =>
        {
            ControlDetailDto? item = await svc.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        endpoints.MapPost("/api/v1/controls", async (
            CreateControlRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Frequency, true, out ControlFrequency frequency))
                return Validation("Valid frequency required.");
            if (!Enum.TryParse(req.AutomationType, true, out ControlAutomationType automation))
                return Validation("Valid automationType required.");
            try
            {
                ControlDetailDto created = await svc.CreateAsync(
                    req.Title, req.Objective, req.Description, req.Domain, frequency, automation,
                    req.PrimaryOwnerUserId, req.PrimaryOwnerRoleId, ct);
                return Results.Created($"/api/v1/controls/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPut("/api/v1/controls/{id:guid}", async (
            Guid id, UpdateControlRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Frequency, true, out ControlFrequency frequency))
                return Validation("Valid frequency required.");
            if (!Enum.TryParse(req.AutomationType, true, out ControlAutomationType automation))
                return Validation("Valid automationType required.");
            try
            {
                return Results.Ok(await svc.UpdateAsync(
                    id, req.Title, req.Objective, req.Description, frequency, automation,
                    req.PrimaryOwnerUserId, req.PrimaryOwnerRoleId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/activate", async (
            Guid id, InternalControlService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.ActivateAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/retire", async (
            Guid id, InternalControlService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.RetireAsync(id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/secondary-owners", async (
            Guid id, MemberRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            try { await svc.AddSecondaryOwnerAsync(id, req.UserId, ct); return Results.NoContent(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapDelete("/api/v1/controls/{id:guid}/secondary-owners/{userId:guid}", async (
            Guid id, Guid userId, InternalControlService svc, CancellationToken ct) =>
        {
            await svc.RemoveSecondaryOwnerAsync(id, userId, ct);
            return Results.NoContent();
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/links/configuration-items", async (
            Guid id, LinkGuidRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            try { await svc.LinkConfigurationItemAsync(id, req.Id, ct); return Results.NoContent(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapDelete("/api/v1/controls/{id:guid}/links/configuration-items/{ciId:guid}", async (
            Guid id, Guid ciId, InternalControlService svc, CancellationToken ct) =>
        {
            await svc.UnlinkConfigurationItemAsync(id, ciId, ct);
            return Results.NoContent();
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/links/business-services", async (
            Guid id, LinkGuidRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            try { await svc.LinkBusinessServiceAsync(id, req.Id, ct); return Results.NoContent(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapDelete("/api/v1/controls/{id:guid}/links/business-services/{serviceId:guid}", async (
            Guid id, Guid serviceId, InternalControlService svc, CancellationToken ct) =>
        {
            await svc.UnlinkBusinessServiceAsync(id, serviceId, ct);
            return Results.NoContent();
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/links/documents", async (
            Guid id, LinkGuidRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            try { await svc.LinkManagedDocumentAsync(id, req.Id, ct); return Results.NoContent(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapDelete("/api/v1/controls/{id:guid}/links/documents/{documentId:guid}", async (
            Guid id, Guid documentId, InternalControlService svc, CancellationToken ct) =>
        {
            await svc.UnlinkManagedDocumentAsync(id, documentId, ct);
            return Results.NoContent();
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/test-procedures", async (
            Guid id, CreateProcedureRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.AddTestProcedureAsync(
                    id, req.Title, req.ProcedureSteps, req.ExpectedResult, req.Purpose, req.SampleGuidance, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPut("/api/v1/controls/{id:guid}/test-procedures/{procedureId:guid}", async (
            Guid id, Guid procedureId, UpdateProcedureRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateTestProcedureAsync(
                    id, procedureId, req.Title, req.Purpose, req.ProcedureSteps, req.ExpectedResult,
                    req.SampleGuidance, req.IsActive, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPost("/api/v1/controls/{id:guid}/evidence-requirements", async (
            Guid id, CreateEvidenceRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            ControlFrequency? frequency = null;
            if (!string.IsNullOrWhiteSpace(req.Frequency))
            {
                if (!Enum.TryParse(req.Frequency, true, out ControlFrequency parsed))
                    return Validation("Valid frequency required.");
                frequency = parsed;
            }

            try
            {
                return Results.Ok(await svc.AddEvidenceRequirementAsync(
                    id, req.Description, frequency, req.RetentionNotes, req.IsRequired ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapPut("/api/v1/controls/{id:guid}/evidence-requirements/{requirementId:guid}", async (
            Guid id, Guid requirementId, UpdateEvidenceRequest req, InternalControlService svc, CancellationToken ct) =>
        {
            ControlFrequency? frequency = null;
            if (!string.IsNullOrWhiteSpace(req.Frequency))
            {
                if (!Enum.TryParse(req.Frequency, true, out ControlFrequency parsed))
                    return Validation("Valid frequency required.");
                frequency = parsed;
            }

            try
            {
                return Results.Ok(await svc.UpdateEvidenceRequirementAsync(
                    id, requirementId, req.Description, frequency, req.RetentionNotes, req.IsRequired, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(ControlManage);

        endpoints.MapDelete("/api/v1/controls/{id:guid}/evidence-requirements/{requirementId:guid}", async (
            Guid id, Guid requirementId, InternalControlService svc, CancellationToken ct) =>
        {
            await svc.DeleteEvidenceRequirementAsync(id, requirementId, ct);
            return Results.NoContent();
        }).RequirePermission(ControlManage);
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record UpsertProfileRequest(string LegalName, string Timezone, string? ClassificationScheme);
    private sealed record CreateUnitRequest(string Name, string? Code, Guid? ParentId, Guid? ManagerUserId, string? Description);
    private sealed record UpdateUnitRequest(string Name, string? Code, Guid? ParentId, Guid? ManagerUserId, string? Description, bool IsActive);
    private sealed record MemberRequest(Guid UserId);
    private sealed record CreateControlRequest(
        string Title, string Objective, string Description, string Domain,
        string Frequency, string AutomationType, Guid? PrimaryOwnerUserId, Guid? PrimaryOwnerRoleId);
    private sealed record UpdateControlRequest(
        string Title, string Objective, string Description,
        string Frequency, string AutomationType, Guid? PrimaryOwnerUserId, Guid? PrimaryOwnerRoleId);
    private sealed record LinkGuidRequest(Guid Id);
    private sealed record CreateProcedureRequest(
        string Title, string ProcedureSteps, string ExpectedResult, string? Purpose, string? SampleGuidance);
    private sealed record UpdateProcedureRequest(
        string Title, string? Purpose, string ProcedureSteps, string ExpectedResult, string? SampleGuidance, bool IsActive);
    private sealed record CreateEvidenceRequest(string Description, string? Frequency, string? RetentionNotes, bool? IsRequired);
    private sealed record UpdateEvidenceRequest(string Description, string? Frequency, string? RetentionNotes, bool IsRequired);
}
