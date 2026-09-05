using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.Audit.Services;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.BusinessContinuity.Services;
using Qec.Itmg.ChangeManagement.Services;
using Qec.Itmg.Cmdb.Domain;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Compliance.Services;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Continuity;
using Qec.Itmg.Contracts.Evidence;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Services;
using Qec.Itmg.Evidence.Services;
using Qec.Itmg.Governance.Domain;
using Qec.Itmg.Governance.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Reporting.Services;
using Qec.Itmg.Security.Services;
using Qec.Itmg.ServiceDesk.Services;
using Qec.Itmg.ThirdParty.Services;

namespace Qec.Itmg.Host.Reporting;

public static class ReportEndpoints
{
    public const string ReportServiceDesk = "report.servicedesk";
    public const string ReportIncident = "report.incident";
    public const string ReportChange = "report.change";
    public const string ReportCmdb = "report.cmdb";
    public const string ReportSecurity = "report.security";
    public const string ReportCompliance = "report.compliance";
    public const string ReportAudit = "report.audit";
    public const string ReportBcm = "report.bcm";
    public const string ReportVendor = "report.vendor";
    public const string ReportExecutive = "report.executive";
    public const string ReportExport = "report.export";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/reports/executive", GetExecutiveAsync).RequirePermission(ReportExecutive);
        endpoints.MapGet("/api/v1/reports/executive/snapshots", GetExecutiveSnapshotsAsync).RequirePermission(ReportExecutive);
        endpoints.MapGet("/api/v1/reports/servicedesk", GetServiceDeskAsync).RequirePermission(ReportServiceDesk);
        endpoints.MapGet("/api/v1/reports/incidents", GetIncidentsAsync).RequirePermission(ReportIncident);
        endpoints.MapGet("/api/v1/reports/changes", GetChangesAsync).RequirePermission(ReportChange);
        endpoints.MapGet("/api/v1/reports/cmdb", GetCmdbAsync).RequirePermission(ReportCmdb);
        endpoints.MapGet("/api/v1/reports/security", GetSecurityAsync).RequirePermission(ReportSecurity);
        endpoints.MapGet("/api/v1/reports/compliance", GetComplianceAsync).RequirePermission(ReportCompliance);
        endpoints.MapGet("/api/v1/reports/audit", GetAuditAsync).RequirePermission(ReportAudit);
        endpoints.MapGet("/api/v1/reports/bcm", GetBcmAsync).RequirePermission(ReportBcm);
        endpoints.MapGet("/api/v1/reports/vendors", GetVendorsAsync).RequirePermission(ReportVendor);
        endpoints.MapGet("/api/v1/reports/{reportKey}/export.csv", ExportCsvAsync);
        return endpoints;
    }

    private static async Task<IResult> GetExecutiveAsync(
        ClaimsPrincipal principal, ICurrentUserService currentUser,
        TicketService tickets, ChangeService changes, SecurityService security,
        ContinuityService bcm, BusinessServiceService services, ConfigurationItemService cis,
        VendorService vendors, ManagedAccountService accounts, AuditService audits,
        InternalControlService controls, DocumentService documents, EvidenceService evidence,
        IEvidenceCoverageQuery evidenceCoverage, IDrTestCoverageQuery drCoverage,
        FrameworkService frameworks, CoverageService coverage, IClock clock, CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();

        DateTimeOffset now = clock.UtcNow;
        Dictionary<string, object?> tiles = new();

        if (Can(session, ReportServiceDesk) || Can(session, ReportIncident))
        {
            var sd = await tickets.GetServiceDeskReportAsync(null, null, ct);
            tiles["serviceHealth"] = new { sd.OpenTickets, sd.SlaBreachedOpen, sd.Backlog, generatedAtUtc = sd.GeneratedAtUtc, source = "live" };
            var inc = await tickets.GetIncidentReportAsync(null, null, ct);
            tiles["incidents"] = new { inc.OpenIncidents, inc.MajorIncidentsOpen, inc.MedianMttaMinutes, inc.MedianMttrMinutes, generatedAtUtc = inc.GeneratedAtUtc, source = "live" };
        }
        if (Can(session, ReportChange))
        {
            var chg = await changes.GetChangeReportAsync(null, null, ct);
            tiles["changes"] = new { chg.Successful, chg.Failed, chg.RolledBack, chg.Emergency, generatedAtUtc = chg.GeneratedAtUtc, source = "live" };
        }
        if (Can(session, ReportSecurity))
        {
            int secInc = await tickets.CountOpenSecurityIncidentsAsync(ct);
            var sec = await security.GetDashboardCountsAsync(secInc, ct);
            tiles["security"] = new { sec.OpenVulnerabilities, sec.CriticalHighVulnerabilities, sec.OverdueRemediation, sec.OpenRisks, sec.OpenExceptions, generatedAtUtc = now, source = "live" };
        }
        if (Can(session, ReportCompliance))
        {
            tiles["compliance"] = await BuildComplianceTilesAsync(
                controls, documents, evidence, evidenceCoverage, frameworks, coverage, clock, ct);
        }
        if (Can(session, ReportAudit))
        {
            var ready = await audits.GetInternalReadinessAsync(ct);
            tiles["audit"] = new { ready.OpenFindings, ready.OverdueCapa, ready.OpenEvidenceRequests, generatedAtUtc = now, source = "live" };
        }
        if (Can(session, ReportBcm))
        {
            var svcList = await services.ListAsync(ct);
            int spofs = await cis.CountConfirmedSpofsAsync(ct);
            var dash = await bcm.GetDashboardCountsAsync(
                svcList.Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(), spofs, ct);
            tiles["bcm"] = new
            {
                dash.CriticalServices,
                dash.ServicesWithoutApprovedBia,
                dash.ServicesMissingRecentDrTest,
                dash.RtoMisses,
                dash.RpoMisses,
                dash.ConfirmedSpofs,
                generatedAtUtc = now,
                source = "live",
            };
        }
        if (Can(session, ReportVendor))
        {
            int priv = await accounts.CountActiveWithVendorAsync(ct);
            var v = await vendors.GetDashboardAsync(priv, ct);
            tiles["vendors"] = new { v.CriticalVendors, v.ContractsExpiring, v.ExpiredContracts, v.AssessmentsOverdue, v.OpenVendorLinkedRisks, generatedAtUtc = now, source = "live" };
        }

        return Results.Ok(new
        {
            generatedAtUtc = now,
            asOfUtc = now,
            source = "live",
            note = "Executive tiles are counts/states only. No vanity compliance score.",
            tiles,
        });
    }

    private static async Task<IResult> GetExecutiveSnapshotsAsync(
        ReportSnapshotService snapshots, int? take, CancellationToken ct) =>
        Results.Ok(await snapshots.ListAsync(ReportSnapshotService.ExecutiveKey, take ?? 30, ct));

    private static async Task<IResult> GetServiceDeskAsync(
        DateTimeOffset? from, DateTimeOffset? to, TicketService tickets, CancellationToken ct) =>
        Results.Ok(await tickets.GetServiceDeskReportAsync(from, to, ct));

    private static async Task<IResult> GetIncidentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, TicketService tickets, CancellationToken ct) =>
        Results.Ok(await tickets.GetIncidentReportAsync(from, to, ct));

    private static async Task<IResult> GetChangesAsync(
        DateTimeOffset? from, DateTimeOffset? to, ChangeService changes, CancellationToken ct) =>
        Results.Ok(await changes.GetChangeReportAsync(from, to, ct));

    private static async Task<IResult> GetCmdbAsync(
        ConfigurationItemService cis, BusinessServiceService services, IClock clock, CancellationToken ct)
    {
        var ciList = await cis.ListConfigurationItemsAsync(null, ct);
        var svcList = await services.ListAsync(ct);
        int spofs = await cis.CountConfirmedSpofsAsync(ct);
        return Results.Ok(new
        {
            generatedAtUtc = clock.UtcNow,
            source = "live",
            operationalCiCount = ciList.Count(x => x.Status == nameof(ConfigurationItemStatus.Active)),
            criticalCis = ciList.Count(x => x.Criticality is "High" or "Critical"),
            criticalServices = svcList.Count(x => (x.Criticality is "High" or "Critical") && x.IsActive),
            confirmedSpofs = spofs,
            note = "CMDB operational counts only. External Asset Management remains authoritative for financial inventory.",
        });
    }

    private static async Task<IResult> GetSecurityAsync(
        TicketService tickets, SecurityService security, IClock clock, CancellationToken ct)
    {
        int secInc = await tickets.CountOpenSecurityIncidentsAsync(ct);
        var dash = await security.GetDashboardCountsAsync(secInc, ct);
        return Results.Ok(new
        {
            generatedAtUtc = clock.UtcNow,
            source = "live",
            dash.OpenVulnerabilities,
            dash.CriticalHighVulnerabilities,
            dash.OverdueRemediation,
            dash.OpenSecurityIncidents,
            dash.OpenExceptions,
            dash.ExpiringExceptions,
            dash.OpenRisks,
            dash.HighResidualRisks,
            note = dash.Note,
        });
    }

    private static async Task<IResult> GetComplianceAsync(
        InternalControlService controls, DocumentService documents, EvidenceService evidence,
        IEvidenceCoverageQuery evidenceCoverage, FrameworkService frameworks, CoverageService coverage,
        IClock clock, CancellationToken ct) =>
        Results.Ok(await BuildComplianceTilesAsync(
            controls, documents, evidence, evidenceCoverage, frameworks, coverage, clock, ct));

    private static async Task<IResult> GetAuditAsync(
        AuditService audits, EvidenceService evidence, DocumentService documents,
        IEvidenceCoverageQuery evidenceCoverage, InternalControlService controls,
        BusinessServiceService services, IDrTestCoverageQuery drCoverage, IClock clock, CancellationToken ct)
    {
        var ready = await audits.GetInternalReadinessAsync(ct);
        var capa = await audits.GetCapaSummaryAsync(null, ct);
        ControlListResult controlPage = await controls.ListAsync(1, 100, null, null, ControlStatus.Active, ct);
        List<Guid> controlIds = controlPage.Items.Select(x => x.Id).ToList();
        EvidenceCoverageSnapshot snap = controlIds.Count == 0
            ? new(0, 0, 0)
            : await evidenceCoverage.GetForControlsAsync(controlIds, clock.UtcNow, ct);
        EvidenceListResult expired = await evidence.ListAsync(
            1, 1, null, null, null, null, null, null, expiredOnly: true, expiringSoonOnly: false,
            includeConfidential: true, ct);
        DocumentListResult policies = await documents.ListAsync(
            1, 1, null, DocumentType.Policy, null, publishedOnly: false, includeConfidential: true,
            reviewOverdueOnly: true, ct);
        var svcList = await services.ListAsync(ct);
        DrTestCoverageSnapshot dr = await drCoverage.GetMissingForCriticalServicesAsync(
            svcList.Select(s => (s.Id, s.Criticality)).ToList(), clock.UtcNow, 365, ct);

        return Results.Ok(new
        {
            generatedAtUtc = clock.UtcNow,
            source = "live",
            ready.OpenFindings,
            ready.OverdueCapa,
            capaOpen = capa.Open,
            capaVerified = capa.Verified,
            controlsWithoutAcceptedEvidence = snap.ControlsMissingEvidence,
            expiredEvidence = expired.ExpiredCount,
            policiesOverdueReview = policies.ReviewOverdueCount,
            drTestsMissingForCriticalServices = dr.CriticalServicesMissingRecentDrTest,
            note = "Counts only. Not an audit certification or readiness score.",
        });
    }

    private static async Task<IResult> GetBcmAsync(
        ContinuityService bcm, BusinessServiceService services, ConfigurationItemService cis,
        IClock clock, CancellationToken ct)
    {
        var svcList = await services.ListAsync(ct);
        int spofs = await cis.CountConfirmedSpofsAsync(ct);
        var dash = await bcm.GetDashboardCountsAsync(
            svcList.Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(), spofs, ct);
        var readiness = await bcm.GetServiceReadinessAsync(
            svcList.Select(s => (s.Id, s.Name, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(),
            await services.CountSpofsByServiceAsync(ct),
            ct);
        return Results.Ok(new
        {
            generatedAtUtc = clock.UtcNow,
            source = "live",
            dashboard = dash,
            serviceReadiness = readiness,
        });
    }

    private static async Task<object> BuildComplianceTilesAsync(
        InternalControlService controls, DocumentService documents, EvidenceService evidence,
        IEvidenceCoverageQuery evidenceCoverage, FrameworkService frameworks, CoverageService coverage,
        IClock clock, CancellationToken ct)
    {
        ControlListResult allControls = await controls.ListAsync(1, 500, null, null, null, ct);
        var byStatus = allControls.Items.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        var byDomain = allControls.Items.GroupBy(x => x.Domain).ToDictionary(g => g.Key, g => g.Count());

        FrameworkCoverageDto? frameworkCoverage = null;
        foreach (var fw in await frameworks.ListAsync(ct))
        {
            var detail = await frameworks.GetAsync(fw.Id, ct);
            FrameworkVersionDto? current = detail?.Versions.FirstOrDefault(v => v.IsCurrent) ?? detail?.Versions.FirstOrDefault();
            if (current is null) continue;
            frameworkCoverage = await coverage.GetCoverageAsync(current.Id, null, null, ct);
            break;
        }

        ControlListResult active = await controls.ListAsync(1, 200, null, null, ControlStatus.Active, ct);
        List<Guid> controlIds = active.Items.Select(x => x.Id).ToList();
        EvidenceCoverageSnapshot snap = controlIds.Count == 0
            ? new(0, 0, 0)
            : await evidenceCoverage.GetForControlsAsync(controlIds, clock.UtcNow, ct);
        EvidenceListResult expired = await evidence.ListAsync(
            1, 1, null, null, null, null, null, null, expiredOnly: true, expiringSoonOnly: false,
            includeConfidential: true, ct);
        DocumentListResult policies = await documents.ListAsync(
            1, 1, null, DocumentType.Policy, null, publishedOnly: false, includeConfidential: true,
            reviewOverdueOnly: true, ct);

        return new
        {
            generatedAtUtc = clock.UtcNow,
            source = "live",
            controlsByStatus = byStatus,
            controlsByDomain = byDomain,
            mappedRequirements = frameworkCoverage?.MappedRequirements,
            unmappedRequirements = frameworkCoverage?.UnmappedRequirements,
            assessedControls = frameworkCoverage?.AssessedControls,
            unassessedControls = frameworkCoverage?.UnassessedControls,
            assessmentResults = frameworkCoverage?.ResultDistribution,
            evidenceAvailable = frameworkCoverage?.EvidenceAvailable ?? snap.ControlsWithAvailableEvidence,
            evidenceMissing = frameworkCoverage?.EvidenceMissing ?? snap.ControlsMissingEvidence,
            expiredEvidence = expired.ExpiredCount,
            policiesOverdueReview = policies.ReviewOverdueCount,
            note = "Honest counts only. Mapping or uploaded files do not imply compliance. No percentage score.",
        };
    }

    private static async Task<IResult> GetVendorsAsync(
        VendorService vendors, ManagedAccountService accounts, IClock clock, CancellationToken ct)
    {
        int priv = await accounts.CountActiveWithVendorAsync(ct);
        var dash = await vendors.GetDashboardAsync(priv, ct);
        return Results.Ok(new { generatedAtUtc = clock.UtcNow, source = "live", dashboard = dash });
    }

    private static async Task<IResult> ExportCsvAsync(
        string reportKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        IBusinessAuditWriter businessAudit,
        TicketService tickets,
        ChangeService changes,
        SecurityService security,
        ContinuityService bcm,
        BusinessServiceService services,
        ConfigurationItemService cis,
        VendorService vendors,
        ManagedAccountService accounts,
        AuditService audits,
        InternalControlService controls,
        DocumentService documents,
        EvidenceService evidence,
        IEvidenceCoverageQuery evidenceCoverage,
        IDrTestCoverageQuery drCoverage,
        FrameworkService frameworks,
        CoverageService coverage,
        IClock clock,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        if (!Can(session, ReportExport)) return Results.Forbid();

        string? groupPermission = reportKey.ToLowerInvariant() switch
        {
            "servicedesk" => ReportServiceDesk,
            "incidents" => ReportIncident,
            "changes" => ReportChange,
            "cmdb" => ReportCmdb,
            "security" => ReportSecurity,
            "compliance" => ReportCompliance,
            "audit" => ReportAudit,
            "bcm" => ReportBcm,
            "vendors" => ReportVendor,
            "executive" => ReportExecutive,
            _ => null,
        };
        if (groupPermission is null || !Can(session, groupPermission))
            return Results.Forbid();

        object payload = reportKey.ToLowerInvariant() switch
        {
            "servicedesk" => await tickets.GetServiceDeskReportAsync(from, to, ct),
            "incidents" => await tickets.GetIncidentReportAsync(from, to, ct),
            "changes" => await changes.GetChangeReportAsync(from, to, ct),
            "cmdb" => await BuildCmdbPayloadAsync(cis, services, ct),
            "security" => await security.GetDashboardCountsAsync(await tickets.CountOpenSecurityIncidentsAsync(ct), ct),
            "compliance" => await BuildComplianceTilesAsync(
                controls, documents, evidence, evidenceCoverage, frameworks, coverage, clock, ct),
            "audit" => await audits.GetInternalReadinessAsync(ct),
            "bcm" => await bcm.GetDashboardCountsAsync(
                (await services.ListAsync(ct)).Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(),
                await cis.CountConfirmedSpofsAsync(ct), ct),
            "vendors" => await vendors.GetDashboardAsync(await accounts.CountActiveWithVendorAsync(ct), ct),
            "executive" => await BuildExecutiveExportAsync(
                session, tickets, changes, audits, from, to, ct),
            _ => throw new InvalidOperationException("Unknown report."),
        };

        string csv = ToCsv(payload);
        int rowCount = Math.Max(0, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1);
        const int maxRows = 5000;
        if (rowCount > maxRows)
            return Results.Problem(detail: $"Export exceeds safe row limit ({maxRows}).", statusCode: 400);

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.ReportExport,
            AggregateId = session.Id,
            BusinessNumber = reportKey,
            Action = BusinessAuditAction.Updated,
            FieldName = "Export",
            NewValue = JsonSerializer.Serialize(new
            {
                reportKey,
                from,
                to,
                rowCount,
                actor = session.Upn,
                at = clock.UtcNow,
            }),
            Source = AuditSource.Api,
        }, ct);

        byte[] bytes = Encoding.UTF8.GetBytes(csv);
        return Results.File(bytes, "text/csv", $"{reportKey}-{clock.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static async Task<object> BuildExecutiveExportAsync(
        CurrentUserDto session,
        TicketService tickets,
        ChangeService changes,
        AuditService audits,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        Dictionary<string, object?> row = new();
        if (Can(session, ReportServiceDesk) || Can(session, ReportIncident))
        {
            var sd = await tickets.GetServiceDeskReportAsync(from, to, ct);
            var inc = await tickets.GetIncidentReportAsync(from, to, ct);
            row["openTickets"] = sd.OpenTickets;
            row["openIncidents"] = inc.OpenIncidents;
        }
        if (Can(session, ReportChange))
            row["changeFailed"] = (await changes.GetChangeReportAsync(from, to, ct)).Failed;
        if (Can(session, ReportAudit))
            row["openFindings"] = (await audits.GetInternalReadinessAsync(ct)).OpenFindings;
        return row;
    }

    private static async Task<object> BuildCmdbPayloadAsync(
        ConfigurationItemService cis, BusinessServiceService services, CancellationToken ct)
    {
        var ciList = await cis.ListConfigurationItemsAsync(null, ct);
        var svcList = await services.ListAsync(ct);
        return new
        {
            operationalCiCount = ciList.Count(x => x.Status == nameof(ConfigurationItemStatus.Active)),
            criticalCis = ciList.Count(x => x.Criticality is "High" or "Critical"),
            criticalServices = svcList.Count(x => (x.Criticality is "High" or "Critical") && x.IsActive),
            confirmedSpofs = await cis.CountConfirmedSpofsAsync(ct),
        };
    }

    private static string ToCsv(object payload)
    {
        var sb = new StringBuilder();
        sb.AppendLine("metric,value");
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload, JsonOpts));
        Flatten(doc.RootElement, "", sb);
        return sb.ToString();
    }

    private static void Flatten(JsonElement el, string prefix, StringBuilder sb)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty p in el.EnumerateObject())
                    Flatten(p.Value, string.IsNullOrEmpty(prefix) ? p.Name : $"{prefix}.{p.Name}", sb);
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (JsonElement item in el.EnumerateArray())
                {
                    Flatten(item, $"{prefix}[{i}]", sb);
                    i++;
                    if (i >= 200) break;
                }
                break;
            case JsonValueKind.Null:
                sb.Append(Csv(prefix)).Append(',').AppendLine("");
                break;
            default:
                sb.Append(Csv(prefix)).Append(',').AppendLine(Csv(el.ToString()));
                break;
        }
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportSnapshotJob(
    ReportSnapshotService snapshots,
    TicketService tickets,
    ChangeService changes,
    SecurityService security,
    ContinuityService bcm,
    BusinessServiceService services,
    ConfigurationItemService cis,
    VendorService vendors,
    ManagedAccountService accounts,
    AuditService audits,
    IBusinessAuditWriter businessAudit,
    IClock clock,
    ILogger<ReportSnapshotJob> logger)
{
    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.UtcNow;
        var sd = await tickets.GetServiceDeskReportAsync(null, null, cancellationToken);
        var inc = await tickets.GetIncidentReportAsync(null, null, cancellationToken);
        var chg = await changes.GetChangeReportAsync(null, null, cancellationToken);
        int secInc = await tickets.CountOpenSecurityIncidentsAsync(cancellationToken);
        var sec = await security.GetDashboardCountsAsync(secInc, cancellationToken);
        var svcList = await services.ListAsync(cancellationToken);
        var bcmDash = await bcm.GetDashboardCountsAsync(
            svcList.Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(),
            await cis.CountConfirmedSpofsAsync(cancellationToken),
            cancellationToken);
        var vendorDash = await vendors.GetDashboardAsync(
            await accounts.CountActiveWithVendorAsync(cancellationToken), cancellationToken);
        var audit = await audits.GetInternalReadinessAsync(cancellationToken);

        var payload = new
        {
            capturedAtUtc = now,
            serviceDesk = new { sd.OpenTickets, sd.SlaBreachedOpen, sd.Backlog },
            incidents = new { inc.OpenIncidents, inc.MajorIncidentsOpen, inc.MedianMttaMinutes, inc.MedianMttrMinutes },
            changes = new { chg.Successful, chg.Failed, chg.RolledBack, chg.Emergency },
            security = new { sec.OpenVulnerabilities, sec.OverdueRemediation, sec.OpenRisks },
            audit = new { audit.OpenFindings, audit.OverdueCapa },
            bcm = new { bcmDash.CriticalServices, bcmDash.ServicesMissingRecentDrTest, bcmDash.RtoMisses, bcmDash.ConfirmedSpofs },
            vendors = new { vendorDash.CriticalVendors, vendorDash.ContractsExpiring, vendorDash.AssessmentsOverdue },
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var saved = await snapshots.UpsertAsync(
            ReportSnapshotService.ExecutiveKey, now, json, now.AddDays(-1), now, cancellationToken);

        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.ReportSnapshot,
            AggregateId = saved.Id,
            BusinessNumber = saved.SnapshotKey,
            Action = BusinessAuditAction.Updated,
            FieldName = "Snapshot",
            NewValue = JsonSerializer.Serialize(new
            {
                saved.SnapshotKey,
                snapshotDateUtc = saved.SnapshotDateUtc,
                at = now,
            }),
            Source = AuditSource.Job,
        }, cancellationToken);

        string result = $"snapshot={saved.SnapshotKey} date={saved.SnapshotDateUtc:u}";
        logger.LogInformation("Report executive snapshot captured: {Result}", result);
        return result;
    }
}
