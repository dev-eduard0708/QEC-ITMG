using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Ai;
using Qec.Itmg.Ai.Domain;
using Qec.Itmg.Ai.Services;
using Qec.Itmg.Audit.Services;
using Qec.Itmg.BusinessContinuity.Services;
using Qec.Itmg.ChangeManagement.Services;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.Ai;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Security.Services;
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;

namespace Qec.Itmg.Host.Ai;

public static class AiEndpoints
{
    public const string AiUse = "ai.use";
    public const string AiAdmin = "ai.admin";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/ai/readiness", GetReadiness).RequirePermission(AiAdmin);
        endpoints.MapGet("/api/v1/ai/interactions", ListInteractionsAsync).RequirePermission(AiAdmin);
        endpoints.MapPost("/api/v1/ai/ask", AskAsync).RequirePermission(AiUse);
        endpoints.MapPost("/api/v1/ai/suggest/classification", SuggestClassificationAsync).RequirePermission(AiUse);
        endpoints.MapPost("/api/v1/ai/suggest/kb", SuggestKbAsync).RequirePermission(AiUse);
        endpoints.MapPost("/api/v1/ai/summarize", SummarizeAsync).RequirePermission(AiUse);
        endpoints.MapPost("/api/v1/ai/reports/query", ReportQueryAsync).RequirePermission(AiUse);
        endpoints.MapPost("/api/v1/ai/suggestions/{suggestionId}/accept", AcceptSuggestionAsync).RequirePermission(AiUse);
        endpoints.MapPost("/api/v1/ai/suggestions/{suggestionId}/reject", RejectSuggestionAsync).RequirePermission(AiUse);
        return endpoints;
    }

    private static IResult GetReadiness(IAiModelClient client, IOptions<AiOptions> options)
    {
        AiReadiness r = client.GetReadiness();
        return Results.Ok(new
        {
            r.Enabled,
            r.Configured,
            r.ProviderKind,
            r.ModelName,
            r.Status,
            r.LastSuccessUtc,
            r.LastFailureUtc,
            r.LastErrorSummary,
            credentialReferenceConfigured = !string.IsNullOrWhiteSpace(options.Value.CredentialReference),
            note = "Credential values are never returned. AI never enables itself silently.",
        });
    }

    private static async Task<IResult> ListInteractionsAsync(
        AiInteractionService interactions, int? take, CancellationToken ct) =>
        Results.Ok(await interactions.ListAsync(null, take ?? 30, ct));

    private static async Task<IResult> AskAsync(
        AskAiRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        AiAssistanceOrchestrator orchestrator,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Question))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["question"] = ["Required."] });
        return Results.Ok(await orchestrator.AskAsync(session, body.Question, ct));
    }

    private static async Task<IResult> SuggestClassificationAsync(
        ClassificationSuggestRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        AiAssistanceOrchestrator orchestrator,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        return Results.Ok(await orchestrator.SuggestClassificationAsync(session, body.Title, body.Description, body.TicketId, ct));
    }

    private static async Task<IResult> SuggestKbAsync(
        KbSuggestRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        AiAssistanceOrchestrator orchestrator,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        return Results.Ok(await orchestrator.SuggestKbAsync(session, body.Query, body.TicketId, ct));
    }

    private static async Task<IResult> SummarizeAsync(
        SummarizeRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        AiAssistanceOrchestrator orchestrator,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(body.RecordType) || body.RecordId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["record"] = ["recordType and recordId required."] });
        return Results.Ok(await orchestrator.SummarizeAsync(session, body.RecordType, body.RecordId, ct));
    }

    private static async Task<IResult> ReportQueryAsync(
        ReportQueryRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        AiAssistanceOrchestrator orchestrator,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Question))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["question"] = ["Required."] });
        return Results.Ok(await orchestrator.ReportQueryAsync(session, body.Question, ct));
    }

    private static async Task<IResult> AcceptSuggestionAsync(
        string suggestionId,
        AcceptSuggestionRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        TicketService tickets,
        IBusinessAuditWriter audit,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        if (body.TicketId is null || body.TicketId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ticketId"] = ["Required."] });

        TicketDto? ticket = await tickets.GetAsync(body.TicketId.Value, Can(session, "incidents.security"), ct);
        if (ticket is null) return Results.NotFound();

        // Apply only via normal ticket update path — no privileged AI write.
        if (!string.IsNullOrWhiteSpace(body.Category))
        {
            await tickets.UpdateAsync(
                body.TicketId.Value,
                ticket.Title,
                ticket.Description,
                Enum.Parse<TicketPriority>(ticket.Priority, true),
                ticket.ConfigurationItemId,
                body.Category,
                body.RowVersion ?? ticket.RowVersion,
                ct);
        }

        await audit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Ticket,
            AggregateId = body.TicketId.Value,
            BusinessNumber = ticket.TicketNumber,
            Action = BusinessAuditAction.Updated,
            FieldName = "AiSuggestionAccepted",
            NewValue = JsonSerializer.Serialize(new { suggestionId, body.Category, body.Priority, actor = session.Upn }, JsonOpts),
            Source = AuditSource.Api,
        }, ct);

        return Results.Ok(new { accepted = true, suggestionId, note = "Applied through normal ticket update authorization." });
    }

    private static async Task<IResult> RejectSuggestionAsync(
        string suggestionId,
        RejectSuggestionRequest body,
        ClaimsPrincipal principal,
        ICurrentUserService currentUser,
        IBusinessAuditWriter audit,
        CancellationToken ct)
    {
        CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
        if (session is null) return Results.Unauthorized();
        await audit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Ticket,
            AggregateId = body.TicketId ?? session.Id,
            BusinessNumber = suggestionId,
            Action = BusinessAuditAction.Updated,
            FieldName = "AiSuggestionRejected",
            NewValue = JsonSerializer.Serialize(new { suggestionId, actor = session.Upn }, JsonOpts),
            Source = AuditSource.Api,
        }, ct);
        return Results.Ok(new { rejected = true, suggestionId });
    }

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public sealed record AskAiRequest(string Question);
public sealed record ClassificationSuggestRequest(string? Title, string? Description, Guid? TicketId);
public sealed record KbSuggestRequest(string Query, Guid? TicketId);
public sealed record SummarizeRequest(string RecordType, Guid RecordId);
public sealed record ReportQueryRequest(string Question);
public sealed record AcceptSuggestionRequest(Guid? TicketId, string? Category, string? Priority, string? RowVersion);
public sealed record RejectSuggestionRequest(Guid? TicketId);
