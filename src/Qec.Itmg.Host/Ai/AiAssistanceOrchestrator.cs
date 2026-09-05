using System.Text.Json;
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
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.Security.Services;
using Qec.Itmg.ServiceDesk.Services;
using Qec.Itmg.AccessManagement.Services;
using Qec.Itmg.ThirdParty.Services;
using Qec.Itmg.Host.Reporting;

namespace Qec.Itmg.Host.Ai;

public sealed class AiAssistanceOrchestrator(
    IAiModelClient model,
    IAiRedactionPipeline redaction,
    AiInteractionService interactions,
    IOptions<AiOptions> options,
    KnowledgeArticleService kb,
    TicketService tickets,
    ProblemService problems,
    ChangeService changes,
    SecurityService security,
    AuditService audits,
    ContinuityService bcm,
    BusinessServiceService services,
    ConfigurationItemService cis,
    VendorService vendors,
    ManagedAccountService accounts,
    IBusinessAuditWriter businessAudit,
    ILogger<AiAssistanceOrchestrator> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<object> AskAsync(CurrentUserDto session, string question, CancellationToken ct)
    {
        AiInteraction interaction = await interactions.StartAsync(
            session.Id, "ask", options.Value.ProviderKind, options.Value.ModelName, "Internal", ct);
        int redactions = 0;
        try
        {
            AiRedactionResult cleaned = redaction.Redact(question, AiDataClassification.Internal);
            redactions = cleaned.RedactionCount;

            if (!options.Value.Enabled || !options.Value.IsConfigured)
            {
                // Read-only tool path without model: KB search heuristic.
                var articles = await kb.ListPublishedAsync(cleaned.Text, ct);
                await Complete(interaction, AiInteractionStatus.Disabled, 1, redactions, null, ct);
                return new
                {
                    aiGenerated = false,
                    providerStatus = "Disabled",
                    answer = "AI provider is disabled. Showing authorized published KB matches only.",
                    sources = articles.Take(5).Select(a => new { a.Id, a.Title, a.Slug }),
                    toolsUsed = new[] { "kb.search" },
                    interactionId = interaction.Id,
                };
            }

            string answer = await RunWithToolsAsync(session, interaction, cleaned.Text, "answer the user's ITMG question using tools only", ct);
            await Complete(interaction, AiInteractionStatus.Succeeded, interaction.ToolCallCount, redactions, null, ct);
            return new
            {
                aiGenerated = true,
                providerStatus = "Live",
                answer,
                note = "AI-generated. Non-authoritative. Tool results enforced as current user.",
                interactionId = interaction.Id,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI ask failed");
            await Complete(interaction, AiInteractionStatus.Failed, 0, redactions, "ask-failed", ct);
            return new { aiGenerated = false, error = "AI request failed.", interactionId = interaction.Id };
        }
    }

    public async Task<object> SuggestClassificationAsync(
        CurrentUserDto session, string? title, string? description, Guid? ticketId, CancellationToken ct)
    {
        AiInteraction interaction = await interactions.StartAsync(
            session.Id, "classification.suggest", options.Value.ProviderKind, options.Value.ModelName, "Internal", ct);
        string raw = $"{title}\n{description}";
        AiRedactionResult cleaned = redaction.Redact(raw, AiDataClassification.Internal);

        // Heuristic always available; model optional.
        string lower = cleaned.Text.ToLowerInvariant();
        string category = lower.Contains("password") || lower.Contains("access") ? "Access"
            : lower.Contains("email") || lower.Contains("outlook") ? "Email"
            : lower.Contains("network") || lower.Contains("vpn") ? "Network"
            : lower.Contains("security") || lower.Contains("phish") ? "Security"
            : "General";
        string priority = lower.Contains("down") || lower.Contains("outage") || lower.Contains("critical") ? "High" : "Medium";
        string security = lower.Contains("phish") || lower.Contains("malware") || lower.Contains("breach") ? "Suspected" : "None";

        string suggestionId = Guid.CreateVersion7().ToString("N");
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Ticket,
            AggregateId = ticketId ?? session.Id,
            BusinessNumber = suggestionId,
            Action = BusinessAuditAction.Created,
            FieldName = "AiClassificationSuggestion",
            NewValue = JsonSerializer.Serialize(new { category, priority, security, ticketId }, JsonOpts),
            Source = AuditSource.Api,
        }, ct);

        await Complete(interaction, options.Value.Enabled ? AiInteractionStatus.Succeeded : AiInteractionStatus.Disabled, 0, cleaned.RedactionCount, null, ct);
        return new
        {
            suggestionId,
            aiGenerated = options.Value.Enabled && options.Value.IsConfigured,
            source = options.Value.Enabled && options.Value.IsConfigured ? "ai" : "heuristic",
            suggestions = new
            {
                category = new { value = category, rationale = "Based on keywords in title/description.", confidence = 0.55 },
                priority = new { value = priority, rationale = "Impact language detected.", confidence = 0.5 },
                securityClassification = new { value = security, rationale = "Security keyword scan.", confidence = 0.45 },
            },
            note = "Suggestions are advisory. Human must Accept. AI will not auto-apply Restricted or change priority.",
            interactionId = interaction.Id,
        };
    }

    public async Task<object> SuggestKbAsync(CurrentUserDto session, string query, Guid? ticketId, CancellationToken ct)
    {
        AiInteraction interaction = await interactions.StartAsync(
            session.Id, "kb.suggest", options.Value.ProviderKind, options.Value.ModelName, "Internal", ct);
        AiToolInvocation tool = await interactions.StartToolAsync(interaction.Id, "kb.search", ct);
        AiRedactionResult cleaned = redaction.Redact(query, AiDataClassification.Internal);
        var articles = await kb.ListPublishedAsync(cleaned.Text, ct);
        await interactions.CompleteToolAsync(tool, $"matched={articles.Count}", ct);
        await Complete(interaction, AiInteractionStatus.Succeeded, 1, cleaned.RedactionCount, null, ct);
        return new
        {
            aiGenerated = false,
            note = "Published KB matches only. No automatic ticket resolution.",
            articles = articles.Take(8).Select(a => new { a.Id, a.Title, a.Slug, a.Summary, source = "kb" }),
            draftGuidance = articles.Count == 0
                ? "No published articles matched."
                : $"Review: {string.Join("; ", articles.Take(3).Select(a => a.Title))}",
            interactionId = interaction.Id,
        };
    }

    public async Task<object> SummarizeAsync(CurrentUserDto session, string recordType, Guid recordId, CancellationToken ct)
    {
        AiInteraction interaction = await interactions.StartAsync(
            session.Id, "summarize", options.Value.ProviderKind, options.Value.ModelName, "Internal", ct);
        string? payload = await LoadAuthorizedRecordAsync(session, recordType, recordId, ct);
        if (payload is null)
        {
            await Complete(interaction, AiInteractionStatus.Denied, 0, 0, "not-authorized-or-missing", ct);
            return ResultsPayload(false, "Record not found or not authorized.", interaction.Id);
        }

        AiRedactionResult cleaned = redaction.Redact(payload, AiDataClassification.Internal);
        if (!options.Value.Enabled || !options.Value.IsConfigured)
        {
            await Complete(interaction, AiInteractionStatus.Disabled, 0, cleaned.RedactionCount, null, ct);
            return new
            {
                aiGenerated = false,
                providerStatus = "Disabled",
                summary = cleaned.Text.Length > 800 ? cleaned.Text[..800] + "…" : cleaned.Text,
                note = "Structured authorized fields only (AI provider disabled). AI-generated label N/A.",
                interactionId = interaction.Id,
            };
        }

        string summary = await RunWithToolsAsync(session, interaction,
            $"Summarize this authorized record JSON for an IT operator:\n{cleaned.Text}",
            "produce a short advisory summary", ct);
        await Complete(interaction, AiInteractionStatus.Succeeded, interaction.ToolCallCount, cleaned.RedactionCount, null, ct);
        return new
        {
            aiGenerated = true,
            summary,
            note = "AI-generated advisory summary from authorized fields only.",
            interactionId = interaction.Id,
        };
    }

    public async Task<object> ReportQueryAsync(CurrentUserDto session, string question, CancellationToken ct)
    {
        AiInteraction interaction = await interactions.StartAsync(
            session.Id, "report.query", options.Value.ProviderKind, options.Value.ModelName, "Internal", ct);
        string reportKey = InferReportKey(question);
        string? requiredPerm = ReportPermission(reportKey);
        if (requiredPerm is null || !Can(session, requiredPerm))
        {
            await Complete(interaction, AiInteractionStatus.Denied, 0, 0, "report-permission", ct);
            return new { error = "Forbidden for required report permission.", reportKey, requiredPermission = requiredPerm, interactionId = interaction.Id };
        }

        AiToolInvocation tool = await interactions.StartToolAsync(interaction.Id, "report.run", ct, "Report", null);
        object aggregates = await RunReportAsync(session, reportKey, ct);
        await interactions.CompleteToolAsync(tool, "ok", ct);
        await Complete(interaction, AiInteractionStatus.Succeeded, 1, 0, null, ct);
        return new
        {
            aiGenerated = options.Value.Enabled && options.Value.IsConfigured,
            reportKey,
            aggregates,
            explanation = $"Executed permitted report '{reportKey}' as current user. Values are server aggregates; N/A when unsupported.",
            note = "No SQL generation. No vanity compliance score.",
            interactionId = interaction.Id,
        };
    }

    private async Task<string> RunWithToolsAsync(
        CurrentUserDto session,
        AiInteraction interaction,
        string userContent,
        string instruction,
        CancellationToken ct)
    {
        List<AiMessage> messages =
        [
            new("system", AiSystemPrompt.Text),
            new("user", $"{instruction}\n\nUser/data:\n{userContent}"),
        ];

        for (int round = 0; round < 3; round++)
        {
            AiModelResponse response = await model.CompleteAsync(
                new AiModelRequest(interaction.CorrelationId, messages, AiAllowlistedTools.ReadTools), ct);

            if (response.ToolCalls.Count == 0)
                return response.Content ?? string.Empty;

            messages.Add(new("assistant", response.Content ?? string.Empty, response.ToolCalls));
            foreach (AiToolCall call in response.ToolCalls)
            {
                if (AiDeniedToolCategories.IsDenied(call.Name))
                {
                    messages.Add(new("tool", JsonSerializer.Serialize(new { error = "Tool denied by AI policy." }), ToolCallId: call.Id, Name: call.Name));
                    continue;
                }

                AiToolInvocation inv = await interactions.StartToolAsync(interaction.Id, call.Name, ct);
                string toolResult = await ExecuteToolAsync(session, call, ct);
                await interactions.CompleteToolAsync(inv, "ok", ct);
                messages.Add(new("tool", toolResult, ToolCallId: call.Id, Name: call.Name));
            }
        }

        return "Tool loop completed without final content.";
    }

    private async Task<string> ExecuteToolAsync(CurrentUserDto session, AiToolCall call, CancellationToken ct)
    {
        using JsonDocument args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
        switch (call.Name)
        {
            case "kb.search":
            {
                string q = args.RootElement.TryGetProperty("query", out JsonElement qe) ? qe.GetString() ?? "" : "";
                var list = await kb.ListPublishedAsync(q, ct);
                return JsonSerializer.Serialize(list.Take(10).Select(a => new { a.Id, a.Title, a.Slug, a.Summary }), JsonOpts);
            }
            case "ticket.get":
            {
                if (!Guid.TryParse(args.RootElement.GetProperty("ticketId").GetString(), out Guid id))
                    return """{"error":"invalid id"}""";
                bool sec = Can(session, "incidents.security");
                TicketDto? t = Can(session, "tickets.read")
                    ? await tickets.GetAsync(id, sec, ct)
                    : await tickets.GetForRequesterAsync(id, session.Id, ct);
                if (t is null) return """{"error":"not found or forbidden"}""";
                var safe = new { t.Id, t.TicketNumber, t.Type, t.Title, t.Status, t.Priority, t.Category, t.IsMajorIncident };
                return JsonSerializer.Serialize(safe, JsonOpts);
            }
            case "problem.get":
            {
                if (!Can(session, "problems.read")) return """{"error":"forbidden"}""";
                if (!Guid.TryParse(args.RootElement.GetProperty("problemId").GetString(), out Guid id))
                    return """{"error":"invalid id"}""";
                var p = await problems.GetAsync(id, ct);
                return p is null ? """{"error":"not found"}""" : JsonSerializer.Serialize(new { p.Id, p.ProblemNumber, p.Title, p.Status }, JsonOpts);
            }
            case "change.get":
            {
                if (!Can(session, "change.read")) return """{"error":"forbidden"}""";
                if (!Guid.TryParse(args.RootElement.GetProperty("changeId").GetString(), out Guid id))
                    return """{"error":"invalid id"}""";
                var c = await changes.GetAsync(id, ct);
                return c is null ? """{"error":"not found"}""" : JsonSerializer.Serialize(new { c.Id, c.ChangeNumber, c.Title, c.Status }, JsonOpts);
            }
            case "report.run":
            {
                string key = args.RootElement.GetProperty("reportKey").GetString() ?? "";
                string? perm = ReportPermission(key);
                if (perm is null || !Can(session, perm)) return """{"error":"forbidden"}""";
                object data = await RunReportAsync(session, key, ct);
                return JsonSerializer.Serialize(data, JsonOpts);
            }
            case "security.dashboard":
            {
                if (!Can(session, "sec.dashboard") && !Can(session, "report.security")) return """{"error":"forbidden"}""";
                int secInc = await tickets.CountOpenSecurityIncidentsAsync(ct);
                return JsonSerializer.Serialize(await security.GetDashboardCountsAsync(secInc, ct), JsonOpts);
            }
            case "audit.readiness":
            {
                if (!Can(session, "audit.read") && !Can(session, "report.audit")) return """{"error":"forbidden"}""";
                return JsonSerializer.Serialize(await audits.GetInternalReadinessAsync(ct), JsonOpts);
            }
            case "bcm.dashboard":
            {
                if (!Can(session, "bcm.read") && !Can(session, "report.bcm")) return """{"error":"forbidden"}""";
                var svcList = await services.ListAsync(ct);
                int spofs = await cis.CountConfirmedSpofsAsync(ct);
                return JsonSerializer.Serialize(await bcm.GetDashboardCountsAsync(
                    svcList.Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(), spofs, ct), JsonOpts);
            }
            default:
                return """{"error":"unknown or denied tool"}""";
        }
    }

    private async Task<string?> LoadAuthorizedRecordAsync(CurrentUserDto session, string recordType, Guid id, CancellationToken ct)
    {
        switch (recordType.Trim().ToLowerInvariant())
        {
            case "ticket":
            case "incident":
            {
                TicketDto? t = Can(session, "tickets.read")
                    ? await tickets.GetAsync(id, Can(session, "incidents.security"), ct)
                    : await tickets.GetForRequesterAsync(id, session.Id, ct);
                return t is null ? null : JsonSerializer.Serialize(new { t.TicketNumber, t.Title, t.Status, t.Priority, t.Category, t.Description }, JsonOpts);
            }
            case "problem":
            {
                if (!Can(session, "problems.read")) return null;
                var p = await problems.GetAsync(id, ct);
                return p is null ? null : JsonSerializer.Serialize(p, JsonOpts);
            }
            case "change":
            {
                if (!Can(session, "change.read")) return null;
                var c = await changes.GetAsync(id, ct);
                return c is null ? null : JsonSerializer.Serialize(new { c.ChangeNumber, c.Title, c.Status, c.Type }, JsonOpts);
            }
            default:
                return null;
        }
    }

    private async Task<object> RunReportAsync(CurrentUserDto session, string reportKey, CancellationToken ct) =>
        reportKey.ToLowerInvariant() switch
        {
            "servicedesk" => await tickets.GetServiceDeskReportAsync(null, null, ct),
            "incidents" => await tickets.GetIncidentReportAsync(null, null, ct),
            "changes" => await changes.GetChangeReportAsync(null, null, ct),
            "security" => await security.GetDashboardCountsAsync(await tickets.CountOpenSecurityIncidentsAsync(ct), ct),
            "audit" => await audits.GetInternalReadinessAsync(ct),
            "bcm" => await bcm.GetDashboardCountsAsync(
                (await services.ListAsync(ct)).Select(s => (s.Id, s.Criticality, s.RtoMinutes, s.RpoMinutes)).ToList(),
                await cis.CountConfirmedSpofsAsync(ct), ct),
            "vendors" => await vendors.GetDashboardAsync(await accounts.CountActiveWithVendorAsync(ct), ct),
            "executive" => new
            {
                note = "Executive aggregates composed from authorized report groups only.",
                openTickets = Can(session, ReportEndpoints.ReportServiceDesk)
                    ? (await tickets.GetServiceDeskReportAsync(null, null, ct)).OpenTickets
                    : (int?)null,
            },
            "cmdb" => new { note = "Use /api/v1/reports/cmdb for full CMDB aggregates." },
            "compliance" => new { note = "Use /api/v1/reports/compliance for honest compliance counts." },
            _ => new { error = "Unknown report key." },
        };

    private static string InferReportKey(string question)
    {
        string q = question.ToLowerInvariant();
        if (q.Contains("vulnerab") || q.Contains("security")) return "security";
        if (q.Contains("change")) return "changes";
        if (q.Contains("incident")) return "incidents";
        if (q.Contains("contract") || q.Contains("vendor")) return "vendors";
        if (q.Contains("audit") || q.Contains("capa") || q.Contains("finding")) return "audit";
        if (q.Contains("bcm") || q.Contains("dr ") || q.Contains("continuity")) return "bcm";
        if (q.Contains("cmdb") || q.Contains("configuration")) return "cmdb";
        if (q.Contains("compliance") || q.Contains("control")) return "compliance";
        if (q.Contains("ticket") || q.Contains("service desk") || q.Contains("sla")) return "servicedesk";
        return "executive";
    }

    private static string? ReportPermission(string key) => key.ToLowerInvariant() switch
    {
        "servicedesk" => ReportEndpoints.ReportServiceDesk,
        "incidents" => ReportEndpoints.ReportIncident,
        "changes" => ReportEndpoints.ReportChange,
        "cmdb" => ReportEndpoints.ReportCmdb,
        "security" => ReportEndpoints.ReportSecurity,
        "compliance" => ReportEndpoints.ReportCompliance,
        "audit" => ReportEndpoints.ReportAudit,
        "bcm" => ReportEndpoints.ReportBcm,
        "vendors" => ReportEndpoints.ReportVendor,
        "executive" => ReportEndpoints.ReportExecutive,
        _ => null,
    };

    private async Task Complete(
        AiInteraction interaction, AiInteractionStatus status, int tools, int redactions, string? err, CancellationToken ct)
    {
        // refresh tool count from entity if needed
        await interactions.CompleteAsync(interaction, status, Math.Max(tools, interaction.ToolCallCount), redactions, err, ct);
    }

    private static object ResultsPayload(bool ai, string message, Guid id) =>
        new { aiGenerated = ai, summary = message, interactionId = id };

    private static bool Can(CurrentUserDto session, string permission) =>
        session.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
