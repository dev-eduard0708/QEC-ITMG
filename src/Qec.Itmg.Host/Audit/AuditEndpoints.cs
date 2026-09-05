using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qec.Itmg.Audit.Domain;
using Qec.Itmg.Audit.Services;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Continuity;
using Qec.Itmg.Contracts.Evidence;
using Qec.Itmg.DocumentManagement.Domain;
using Qec.Itmg.DocumentManagement.Services;
using Qec.Itmg.Evidence.Domain;
using Qec.Itmg.Evidence.Services;
using Qec.Itmg.Governance.Domain;
using Qec.Itmg.Governance.Services;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Platform.Attachments;
using Qec.Itmg.Platform.Domain;
using Qec.Itmg.Platform.Notifications;

namespace Qec.Itmg.Host.Audit;

public static class AuditEndpoints
{
    public const string AuditRead = "audit.read";
    public const string AuditManage = "audit.manage";
    public const string FindingManage = "finding.manage";
    public const string EvidenceExport = "evidence.export";

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapEngagements(endpoints);
        MapQuestions(endpoints);
        MapFindings(endpoints);
        MapCapa(endpoints);
        MapEvidenceRequests(endpoints);
        MapReadiness(endpoints);
        MapExport(endpoints);
        return endpoints;
    }

    private static void MapEngagements(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder read = endpoints.MapGroup("/api/v1/audits").RequirePermission(AuditRead);
        read.MapGet(string.Empty, async (
            int? page, int? pageSize, string? search, string? status, string? type, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListEngagementsAsync(
                page ?? 1, pageSize ?? 25, search, ParseEnum<AuditEngagementStatus>(status), ParseEnum<AuditType>(type), ct)));
        read.MapGet("/{id:guid}", async (Guid id, AuditService svc, CancellationToken ct) =>
        {
            AuditEngagementDto? item = await svc.GetEngagementAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        read.MapGet("/{id:guid}/scope", async (Guid id, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListScopeAsync(id, ct)));

        endpoints.MapPost("/api/v1/audits", async (
            CreateEngagementRequest req, AuditService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.AuditType ?? "Internal", true, out AuditType type))
                return Validation("Valid auditType required.");
            try
            {
                AuditEngagementDto created = await svc.CreateEngagementAsync(
                    req.Title, type, req.Objective, req.ScopeSummary, req.LeadAuditorUserId, req.OwnerUserId,
                    req.StartDate, req.EndDate, seedIsa315Questions: type == AuditType.ISA315Profile, ct);
                return Results.Created($"/api/v1/audits/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPut("/api/v1/audits/{id:guid}", async (
            Guid id, UpdateEngagementRequest req, AuditService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpdateEngagementAsync(
                    id, req.Title, req.Objective, req.ScopeSummary, req.LeadAuditorUserId, req.OwnerUserId,
                    req.StartDate, req.EndDate, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/{id:guid}/transition", async (
            Guid id, TransitionRequest req, AuditService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out AuditEngagementStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.TransitionEngagementAsync(id, status, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/{id:guid}/scope", async (
            Guid id, ScopeRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AuditService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.TargetType, true, out AuditScopeTargetType targetType))
                return Validation("Valid targetType required.");
            try { return Results.Ok(await svc.AddScopeAsync(id, targetType, req.TargetId, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapDelete("/api/v1/audits/{id:guid}/scope/{linkId:guid}", async (
            Guid id, Guid linkId, AuditService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.RemoveScopeAsync(id, linkId, ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);
    }

    private static void MapQuestions(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audits/{id:guid}/questions", async (Guid id, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListQuestionsAsync(id, ct))).RequirePermission(AuditRead);

        endpoints.MapPost("/api/v1/audits/{id:guid}/questions", async (
            Guid id, CreateQuestionRequest req, AuditService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.ResponseType ?? "Text", true, out AuditQuestionResponseType responseType))
                return Validation("Valid responseType required.");
            try
            {
                return Results.Ok(await svc.AddQuestionAsync(
                    id, req.Category, req.QuestionText, responseType, req.Required ?? true, req.SortOrder,
                    req.QuestionCode, req.FrameworkRequirementId, req.InternalControlId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/{id:guid}/questions/{questionId:guid}/answer", async (
            Guid id, Guid questionId, AnswerRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AuditService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try { return Results.Ok(await svc.AnswerQuestionAsync(id, questionId, req.Response, session.Id, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/{id:guid}/questions/{questionId:guid}/review", async (
            Guid id, Guid questionId, NotesRequest req, AuditService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.ReviewQuestionAsync(id, questionId, req.Notes, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/{id:guid}/questions/{questionId:guid}/na", async (
            Guid id, Guid questionId, NotesRequest req, AuditService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.MarkQuestionNaAsync(id, questionId, req.Notes, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);
    }

    private static void MapFindings(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audits/findings", async (
            Guid? engagementId, string? status, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListFindingsAsync(engagementId, ParseEnum<FindingStatus>(status), ct)))
            .RequirePermission(AuditRead);

        endpoints.MapGet("/api/v1/audits/{id:guid}/findings", async (Guid id, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListFindingsAsync(id, null, ct))).RequirePermission(AuditRead);

        endpoints.MapGet("/api/v1/audits/findings/{findingId:guid}", async (Guid findingId, AuditService svc, CancellationToken ct) =>
        {
            FindingDto? item = await svc.GetFindingAsync(findingId, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).RequirePermission(AuditRead);

        endpoints.MapPost("/api/v1/audits/{id:guid}/findings", async (
            Guid id, CreateFindingRequest req, AuditService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Severity ?? "Medium", true, out FindingSeverity severity))
                return Validation("Valid severity required.");
            try
            {
                return Results.Ok(await svc.CreateFindingAsync(
                    id, req.Title, req.Description, severity, req.InternalControlId, req.OwnerUserId, req.DueAtUtc, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FindingManage);

        endpoints.MapPost("/api/v1/audits/findings/{findingId:guid}/transition", async (
            Guid findingId, FindingTransitionRequest req, AuditService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out FindingStatus status))
                return Validation("Valid status required.");
            try
            {
                return Results.Ok(await svc.TransitionFindingAsync(
                    findingId, status, req.AcceptedRiskReason, req.ExceptionReference, req.OverrideCapaGate == true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FindingManage);

        endpoints.MapGet("/api/v1/audits/findings/{findingId:guid}/responses", async (
            Guid findingId, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListManagementResponsesAsync(findingId, ct))).RequirePermission(AuditRead);

        endpoints.MapPost("/api/v1/audits/findings/{findingId:guid}/responses", async (
            Guid findingId, ManagementResponseRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AuditService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                return Results.Ok(await svc.AddManagementResponseAsync(
                    findingId, req.ResponseText, session.Id, req.TargetDate, req.ManagementOwnerUserId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FindingManage);
    }

    private static void MapCapa(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audits/capa", async (
            Guid? findingId, Guid? engagementId, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCapaAsync(findingId, engagementId, ct))).RequirePermission(AuditRead);

        endpoints.MapGet("/api/v1/audits/capa/summary", async (Guid? engagementId, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCapaSummaryAsync(engagementId, ct))).RequirePermission(AuditRead);

        endpoints.MapPost("/api/v1/audits/findings/{findingId:guid}/capa", async (
            Guid findingId, CreateCapaRequest req, AuditService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.CreateCapaAsync(
                    findingId, req.Title, req.Description, req.OwnerUserId, req.DueAtUtc, req.IsMandatory ?? true, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FindingManage);

        endpoints.MapPost("/api/v1/audits/capa/{capaId:guid}/transition", async (
            Guid capaId, CapaTransitionRequest req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AuditService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Enum.TryParse(req.Status, true, out CorrectiveActionStatus status))
                return Validation("Valid status required.");
            try
            {
                return Results.Ok(await svc.TransitionCapaAsync(
                    capaId, status, status == CorrectiveActionStatus.Verified ? session.Id : null, req.Notes, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(FindingManage);
    }

    private static void MapEvidenceRequests(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audits/evidence-requests", async (
            Guid? engagementId, string? status, AuditService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListEvidenceRequestsAsync(engagementId, ParseEnum<EvidenceRequestStatus>(status), ct)))
            .RequirePermission(AuditRead);

        endpoints.MapPost("/api/v1/audits/{id:guid}/evidence-requests", async (
            Guid id, CreateEvidenceRequestBody req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AuditService svc, AuditNotificationService notify, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            try
            {
                EvidenceRequestDto created = await svc.CreateEvidenceRequestAsync(
                    id, req.Title, req.Description, session.Id, req.AuditQuestionId, req.InternalControlId,
                    req.RequestedFromUserId, req.DueAtUtc, ct);
                await notify.NotifyRequestedAsync(created, ct);
                return Results.Ok(created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/evidence-requests/{requestId:guid}/fulfill", async (
            Guid requestId, FulfillRequest req, AuditService svc, AuditNotificationService notify, CancellationToken ct) =>
        {
            try
            {
                EvidenceRequestDto updated = await svc.FulfillEvidenceRequestAsync(requestId, req.EvidenceId, req.Notes, ct);
                await notify.NotifyFulfilledAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);

        endpoints.MapPost("/api/v1/audits/evidence-requests/{requestId:guid}/status", async (
            Guid requestId, EvidenceRequestStatusBody req, AuditService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse(req.Status, true, out EvidenceRequestStatus status))
                return Validation("Valid status required.");
            try { return Results.Ok(await svc.UpdateEvidenceRequestStatusAsync(requestId, status, req.Notes, ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(AuditManage);
    }

    private static void MapReadiness(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/audits/readiness", async (
            AuditService audits,
            InternalControlService controls,
            IEvidenceCoverageQuery evidenceCoverage,
            EvidenceService evidence,
            DocumentService documents,
            BusinessServiceService businessServices,
            IDrTestCoverageQuery drTestCoverage,
            IClock clock,
            CancellationToken ct) =>
        {
            AuditReadinessCounts internalCounts = await audits.GetInternalReadinessAsync(ct);
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
            var services = await businessServices.ListAsync(ct);
            DrTestCoverageSnapshot drSnap = await drTestCoverage.GetMissingForCriticalServicesAsync(
                services.Select(s => (s.Id, s.Criticality)).ToList(),
                clock.UtcNow,
                365,
                ct);

            return Results.Ok(new
            {
                controlsWithoutAcceptedEvidence = snap.ControlsMissingEvidence,
                expiredEvidence = expired.ExpiredCount,
                openFindings = internalCounts.OpenFindings,
                overdueCapa = internalCounts.OverdueCapa,
                policiesOverdueReview = policies.ReviewOverdueCount,
                openEvidenceRequests = internalCounts.OpenEvidenceRequests,
                overdueEvidenceRequests = internalCounts.OverdueEvidenceRequests,
                capaCompletedAwaitingVerification = internalCounts.CompletedCapaAwaitingVerification,
                capaVerified = internalCounts.VerifiedCapa,
                drTestsMissingForCriticalServices = drSnap.CriticalServicesMissingRecentDrTest,
                note = "Counts only. Not an audit certification or readiness score.",
            });
        }).RequirePermission(AuditRead);
    }

    private static void MapExport(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/audits/{id:guid}/export-pack", async (
            Guid id, ExportPackRequest? req, ClaimsPrincipal principal, ICurrentUserService currentUser,
            AuditService audits, EvidenceService evidence, IAttachmentStorageService attachments,
            IBusinessAuditWriter businessAudit, IClock clock, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return Results.Unauthorized();
            if (!Can(session, EvidenceExport))
                return Results.Forbid();

            try
            {
                var pack = await audits.LoadPackDataAsync(id, ct);
                bool includeConfidential = Can(session, EvidenceExport);
                List<EvidenceDto> includedEvidence = [];
                List<(string Path, byte[] Bytes, string Sha256, string Classification)> files = [];

                foreach (Guid evidenceId in pack.Requests.Where(x => x.EvidenceId.HasValue).Select(x => x.EvidenceId!.Value).Distinct())
                {
                    EvidenceDto? item = await evidence.GetAsync(evidenceId, includeConfidential, ct);
                    if (item is null)
                        return Results.Problem(
                            detail: $"Fail-closed: evidence {evidenceId} is not authorized for this exporter.",
                            statusCode: StatusCodes.Status403Forbidden);
                    if (!includeConfidential && item.Classification != EvidenceClassification.Internal.ToString())
                        return Results.Problem(
                            detail: $"Fail-closed: restricted evidence {item.EvidenceNumber} cannot be included.",
                            statusCode: StatusCodes.Status403Forbidden);

                    string? sha = null;
                    if (item.CurrentAttachmentId is Guid aid)
                    {
                        AttachmentMetadata? meta = await attachments.GetMetadataAsync(aid, ct);
                        if (meta is not null)
                        {
                            await using Stream stream = await attachments.OpenReadAsync(aid, ct);
                            using MemoryStream ms = new();
                            await stream.CopyToAsync(ms, ct);
                            byte[] bytes = ms.ToArray();
                            sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                            string safe = Sanitize($"{item.EvidenceNumber}_{meta.OriginalFileName}");
                            files.Add(($"evidence/{safe}", bytes, sha, item.Classification));
                        }
                    }

                    includedEvidence.Add(item);
                }

                var manifest = new
                {
                    Purpose = req?.Purpose ?? "Audit engagement evidence pack",
                    ExportedAtUtc = clock.UtcNow,
                    ActorUserId = session.Id,
                    Engagement = new
                    {
                        pack.Engagement.Id,
                        pack.Engagement.AuditNumber,
                        pack.Engagement.Title,
                        AuditType = pack.Engagement.AuditType.ToString(),
                        Status = pack.Engagement.Status.ToString(),
                        pack.Engagement.Objective,
                        pack.Engagement.ScopeSummary,
                    },
                    Scope = pack.Scope.Select(x => new { TargetType = x.TargetType.ToString(), x.TargetId }),
                    Questions = pack.Questions.Select(x => new
                    {
                        x.QuestionCode, x.Category, x.QuestionText, Status = x.Status.ToString(), x.Response,
                    }),
                    Findings = pack.Findings.Select(x => new
                    {
                        x.FindingNumber, x.Title, Severity = x.Severity.ToString(), Status = x.Status.ToString(),
                    }),
                    ManagementResponses = pack.Responses.Select(x => new { x.FindingId, x.ResponseText, x.RespondedAtUtc }),
                    CorrectiveActions = pack.Capas.Select(x => new
                    {
                        x.ActionNumber, x.Title, Status = x.Status.ToString(), x.DueAtUtc,
                    }),
                    EvidenceRequests = pack.Requests.Select(x => new
                    {
                        x.Title, Status = x.Status.ToString(), x.EvidenceId, x.DueAtUtc,
                    }),
                    Evidence = includedEvidence.Select(x => new
                    {
                        x.Id, x.EvidenceNumber, x.Title, x.Classification, x.Status,
                    }),
                    FileHashes = files.Select(f => new { Path = f.Path, f.Sha256, f.Classification }),
                    Disclaimer = "Export pack supports engagement response. It is not audit certification.",
                };

                using MemoryStream zipStream = new();
                using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    byte[] manifestBytes = Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                    ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json");
                    await using (Stream entryStream = manifestEntry.Open())
                        await entryStream.WriteAsync(manifestBytes, ct);

                    foreach ((string path, byte[] bytes, string _, string _) in files)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(path);
                        await using Stream entryStream = entry.Open();
                        await entryStream.WriteAsync(bytes, ct);
                    }
                }

                string evidenceIds = string.Join(",", includedEvidence.Select(x => x.EvidenceNumber));
                string classifications = string.Join(",", files.Select(f => f.Classification).Distinct().Concat(includedEvidence.Select(x => x.Classification)).Distinct());
                await businessAudit.AppendAsync(new BusinessAuditEntry
                {
                    AggregateType = AuditAggregateType.AuditEngagement,
                    AggregateId = pack.Engagement.Id,
                    BusinessNumber = pack.Engagement.AuditNumber,
                    Action = BusinessAuditAction.Updated,
                    FieldName = "ExportPack",
                    NewValue =
                        $"purpose={req?.Purpose ?? "engagement-pack"};evidence=[{evidenceIds}];classification={classifications};actor={session.Id};result=ok",
                    Source = AuditSource.Api,
                }, ct);

                string fileName = $"{pack.Engagement.AuditNumber}-pack-{clock.UtcNow:yyyyMMddHHmmss}.zip";
                return Results.File(zipStream.ToArray(), "application/zip", fileName);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FromEx(ex); }
        }).RequirePermission(EvidenceExport);
    }

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct =>
        Enum.TryParse(value, true, out TEnum result) ? result : null;

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult FromEx(Exception ex) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);

    private sealed record CreateEngagementRequest(
        string Title, string? AuditType, string? Objective, string? ScopeSummary,
        Guid? LeadAuditorUserId, Guid? OwnerUserId, DateOnly? StartDate, DateOnly? EndDate);
    private sealed record UpdateEngagementRequest(
        string Title, string? Objective, string? ScopeSummary,
        Guid? LeadAuditorUserId, Guid? OwnerUserId, DateOnly? StartDate, DateOnly? EndDate);
    private sealed record TransitionRequest(string Status);
    private sealed record ScopeRequest(string TargetType, Guid TargetId);
    private sealed record CreateQuestionRequest(
        string Category, string QuestionText, string? ResponseType, bool? Required, int? SortOrder,
        string? QuestionCode, Guid? FrameworkRequirementId, Guid? InternalControlId);
    private sealed record AnswerRequest(string? Response);
    private sealed record NotesRequest(string? Notes);
    private sealed record CreateFindingRequest(
        string Title, string Description, string? Severity, Guid? InternalControlId, Guid? OwnerUserId, DateTimeOffset? DueAtUtc);
    private sealed record FindingTransitionRequest(
        string Status, string? AcceptedRiskReason, string? ExceptionReference, bool? OverrideCapaGate);
    private sealed record ManagementResponseRequest(string ResponseText, DateOnly? TargetDate, Guid? ManagementOwnerUserId);
    private sealed record CreateCapaRequest(
        string Title, string Description, Guid OwnerUserId, DateTimeOffset? DueAtUtc, bool? IsMandatory);
    private sealed record CapaTransitionRequest(string Status, string? Notes);
    private sealed record CreateEvidenceRequestBody(
        string Title, string? Description, Guid? AuditQuestionId, Guid? InternalControlId,
        Guid? RequestedFromUserId, DateTimeOffset? DueAtUtc);
    private sealed record FulfillRequest(Guid EvidenceId, string? Notes);
    private sealed record EvidenceRequestStatusBody(string Status, string? Notes);
    private sealed record ExportPackRequest(string? Purpose);
}

public sealed class AuditNotificationService(INotificationService notifications)
{
    public Task NotifyRequestedAsync(EvidenceRequestDto req, CancellationToken ct)
    {
        if (req.RequestedFromUserId is not Guid uid) return Task.CompletedTask;
        return notifications.CreateAsync(
            uid, "audit.evidence_requested", NotificationSeverity.Info,
            $"Evidence requested: {req.Title}",
            req.Description ?? "Please provide evidence for the audit engagement.",
            "EvidenceRequest", req.Id, $"/it/audits/{req.AuditEngagementId}", ct);
    }

    public Task NotifyFulfilledAsync(EvidenceRequestDto req, CancellationToken ct) =>
        notifications.CreateAsync(
            req.CreatedByUserId, "audit.evidence_fulfilled", NotificationSeverity.Info,
            $"Evidence fulfilled: {req.Title}",
            "An evidence request was fulfilled.",
            "EvidenceRequest", req.Id, $"/it/audits/{req.AuditEngagementId}", ct);

    public Task NotifyDueAsync(EvidenceRequestDto req, string eventKey, CancellationToken ct)
    {
        if (req.RequestedFromUserId is not Guid uid) return Task.CompletedTask;
        return notifications.CreateAsync(
            uid,
            eventKey,
            eventKey.Contains("overdue") ? NotificationSeverity.Warning : NotificationSeverity.Info,
            eventKey.Contains("overdue") ? $"Evidence overdue: {req.Title}" : $"Evidence due soon: {req.Title}",
            req.DueAtUtc is DateTimeOffset due ? $"Due {due:u}." : "Please fulfill this request.",
            "EvidenceRequest", req.Id, $"/it/audits/{req.AuditEngagementId}", ct);
    }
}

public sealed class AuditEvidenceRequestReminderJob(
    AuditService audits,
    AuditNotificationService notify,
    IClock clock)
{
    private static readonly int[] Thresholds = [30, 14, 7, 1, 0];

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        int sent = 0;
        DateTimeOffset now = clock.UtcNow;
        foreach (var entity in await audits.GetDueEvidenceRequestCandidatesAsync(cancellationToken))
        {
            if (entity.DueAtUtc is not DateTimeOffset due) continue;
            int days = (int)Math.Floor((due - now).TotalDays);
            foreach (int threshold in Thresholds)
            {
                bool match = threshold == 0 ? days < 0 : days == threshold;
                if (!match) continue;
                string eventKey = threshold == 0 ? "audit.evidence_overdue" : $"audit.evidence_due_{threshold}";
                if (await audits.HasNotificationAsync(entity.Id, eventKey, cancellationToken)) continue;
                EvidenceRequestDto dto = new(
                    entity.Id, entity.AuditEngagementId, entity.AuditQuestionId, entity.InternalControlId,
                    entity.Title, entity.Description, entity.RequestedFromUserId, entity.DueAtUtc,
                    entity.Status.ToString(), entity.EvidenceId, entity.CreatedByUserId, entity.CreatedAtUtc,
                    entity.FulfilledAtUtc, entity.Notes, days < 0);
                await notify.NotifyDueAsync(dto, eventKey, cancellationToken);
                await audits.RecordNotificationAsync(entity.Id, eventKey, cancellationToken);
                sent++;
                break;
            }
        }

        return sent;
    }
}
