using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qec.Itmg.Cmdb.Services;
using Qec.Itmg.Contracts.RemoteSupport;
using Qec.Itmg.Contracts.Secrets;
using Qec.Itmg.Identity.Authorization;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.RemoteSupport;
using Qec.Itmg.RemoteSupport.Domain;
using Qec.Itmg.RemoteSupport.Services;

namespace Qec.Itmg.Host.RemoteSupport;

public static class RemoteSupportEndpoints
{
    public const string RemoteRequest = "remote.request";
    public const string RemoteAttended = "remote.attended";
    public const string RemoteUnattended = "remote.unattended";
    public const string RemoteAuditRead = "remote.audit.read";
    public const string RemoteAdmin = "remote.admin";

    public static IEndpointRouteBuilder MapRemoteSupportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/remote-support/readiness", (
            RemoteSessionService svc,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser) =>
        {
            // readiness visible to remote.request or remote.admin
            return Results.Ok(svc.GetEngineStatus());
        }).RequireAuthorization();

        RouteGroupBuilder it = endpoints.MapGroup("/api/v1/remote-support/sessions");

        it.MapGet(string.Empty, async (
            int? page, int? pageSize, string? status, Guid? ticketId, Guid? configurationItemId,
            ClaimsPrincipal principal, ICurrentUserService currentUser, RemoteSessionService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool canAudit = session.Permissions.Contains(RemoteAuditRead) || session.Permissions.Contains(RemoteRequest);
            if (!canAudit) return Results.Forbid();
            return Results.Ok(await svc.ListAsync(page ?? 1, pageSize ?? 25, status, null, null, ticketId, configurationItemId, ct));
        }).RequirePermission(RemoteRequest);

        it.MapGet("/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, RemoteSessionService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (!CanView(session, item)) return Results.Forbid();
            return Results.Ok(item);
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/sessions/attended", async (
            CreateAttendedRemoteRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (req.ConfigurationItemId == Guid.Empty || req.TargetUserId == Guid.Empty)
                return Validation("ConfigurationItemId and TargetUserId are required.");
            try
            {
                RemoteSessionRequestDto created = await svc.CreateAttendedAsync(
                    req.ConfigurationItemId,
                    session.Id,
                    req.TargetUserId,
                    req.Reason ?? string.Empty,
                    req.TicketId,
                    req.ChangeRequestId,
                    req.RequestedPrivileges,
                    req.TechnicianUserId ?? session.Id,
                    ct);
                await notifications.NotifyRequestedAsync(created, ct);
                return Results.Created($"/api/v1/remote-support/sessions/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteRequest);

        endpoints.MapPost("/api/v1/remote-support/sessions/unattended", async (
            CreateUnattendedRemoteRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool mfa = HasMfa(principal);
            try
            {
                RemoteSessionRequestDto created = await svc.CreateUnattendedAsync(
                    req.ConfigurationItemId,
                    session.Id,
                    req.Reason ?? string.Empty,
                    req.TicketId,
                    req.ChangeRequestId,
                    req.RequestedPrivileges,
                    req.TechnicianUserId ?? session.Id,
                    mfa,
                    ct);
                return Results.Created($"/api/v1/remote-support/sessions/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteUnattended);

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/start", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool mfa = HasMfa(principal);
            try
            {
                RemoteSessionRequestDto updated = await svc.StartAsync(
                    id,
                    session.Id,
                    session.Permissions.Contains(RemoteAttended),
                    session.Permissions.Contains(RemoteUnattended),
                    mfa,
                    ct);
                await notifications.NotifySessionStartedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("engine", StringComparison.OrdinalIgnoreCase))
                {
                    RemoteSessionRequestDto? current = await svc.GetAsync(id, ct);
                    if (current is not null)
                        await notifications.NotifyEngineFailedAsync(current, ex.Message, ct);
                }

                return FromEx(ex);
            }
            catch (ArgumentException ex)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/end", async (
            Guid id,
            EndRemoteRequest? req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                RemoteSessionRequestDto updated = await svc.EndAsync(id, session.Id, byTechnician: true, req?.Reason, ct);
                await notifications.NotifySessionEndedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteAttended);

        // Employee consent surface
        endpoints.MapGet("/api/v1/me/remote-support", async (
            int? page, int? pageSize, string? status,
            ClaimsPrincipal principal, ICurrentUserService currentUser, RemoteSessionService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await svc.ListAsync(page ?? 1, pageSize ?? 25, status, session.Id, null, null, null, ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/remote-support/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ICurrentUserService currentUser, RemoteSessionService svc, CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (item.TargetUserId != session.Id) return Results.Forbid();
            return Results.Ok(item);
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/remote-support/{id:guid}/allow", async (
            Guid id,
            HttpContext http,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                RemoteSessionRequestDto updated = await svc.AllowAsync(id, session.Id, ip, ct);
                await notifications.NotifyAllowedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/remote-support/{id:guid}/decline", async (
            Guid id,
            HttpContext http,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                RemoteSessionRequestDto updated = await svc.DeclineAsync(id, session.Id, ip, ct);
                await notifications.NotifyDeclinedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/remote-support/{id:guid}/end", async (
            Guid id,
            EndRemoteRequest? req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? existing = await svc.GetAsync(id, ct);
            if (existing is null) return Results.NotFound();
            if (existing.TargetUserId != session.Id) return Results.Forbid();
            try
            {
                RemoteSessionRequestDto updated = await svc.EndAsync(id, session.Id, byTechnician: false, req?.Reason, ct);
                await notifications.NotifySessionEndedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        // CI remote mapping (remote.admin)
        endpoints.MapPut("/api/v1/cmdb/cis/{id:guid}/remote-mapping", async (
            Guid id,
            SetRemoteMappingRequest req,
            ConfigurationItemService cis,
            CancellationToken ct) =>
        {
            try
            {
                ConfigurationItemDto updated = await cis.SetRemoteEngineMappingAsync(
                    id,
                    req.RemoteEngineNodeId,
                    req.RemoteEngineProvider,
                    req.UnattendedRemotePermitted,
                    req.RowVersion ?? string.Empty,
                    ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteAdmin);

        // Signed webhook for session completion
        endpoints.MapPost("/api/v1/remote-support/webhooks/meshcentral", async (
            HttpRequest request,
            IOptions<RemoteSupportOptions> options,
            ISecretResolver secrets,
            RemoteSessionService svc,
            RemoteSupportNotificationService notifications,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            ILogger logger = loggerFactory.CreateLogger("RemoteSupportWebhook");
            RemoteSupportOptions opts = options.Value;
            if (string.IsNullOrWhiteSpace(opts.WebhookSignatureReference))
                return Results.Json(new { error = "not_configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);

            string? secret = await secrets.ResolveAsync(opts.WebhookSignatureReference, ct);
            if (string.IsNullOrEmpty(secret))
                return Results.Json(new { error = "not_configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);

            request.EnableBuffering();
            using MemoryStream ms = new();
            await request.Body.CopyToAsync(ms, ct);
            byte[] body = ms.ToArray();
            if (body.Length > Math.Max(1024, opts.MaxWebhookPayloadBytes))
                return Results.Json(new { error = "payload_too_large" }, statusCode: StatusCodes.Status413PayloadTooLarge);

            string? timestamp = request.Headers["X-ITMG-Timestamp"].FirstOrDefault();
            string? signature = request.Headers["X-ITMG-Signature"].FirstOrDefault();
            if (!ValidateTimestamp(timestamp, opts.WebhookTimestampSkewSeconds))
                return Results.Json(new { error = "stale" }, statusCode: StatusCodes.Status401Unauthorized);
            if (!ValidateHmac(signature, timestamp, body, secret))
            {
                logger.LogWarning("Remote support webhook HMAC validation failed");
                return Results.Json(new { error = "invalid_signature" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            using JsonDocument doc = JsonDocument.Parse(body.Length == 0 ? "{}" : Encoding.UTF8.GetString(body));
            JsonElement root = doc.RootElement;
            string? engineSessionId = ReadString(root, "sessionId") ?? ReadString(root, "engineSessionId");
            if (string.IsNullOrWhiteSpace(engineSessionId))
                return Results.Json(new { error = "sessionId_required" }, statusCode: StatusCodes.Status400BadRequest);

            RemoteSessionOutcome outcome = Enum.TryParse(ReadString(root, "outcome"), true, out RemoteSessionOutcome o)
                ? o
                : RemoteSessionOutcome.Completed;
            DateTimeOffset? endedAt = null;
            if (DateTimeOffset.TryParse(ReadString(root, "endedAt"), out DateTimeOffset ended))
                endedAt = ended;

            bool changed = await svc.CompleteFromEngineAsync(
                engineSessionId,
                outcome,
                ReadString(root, "endReason"),
                ReadBool(root, "elevationUsed"),
                ReadString(root, "recordingReference"),
                endedAt,
                ct);

            if (changed)
            {
                // best-effort notify using engine session lookup via list is heavy; skip detailed notify if not found
            }

            return Results.Ok(new { accepted = true, applied = changed });
        }).AllowAnonymous();

        return endpoints;
    }

    private static bool CanView(CurrentUserDto session, RemoteSessionRequestDto item) =>
        session.Permissions.Contains(RemoteAuditRead)
        || session.Permissions.Contains(RemoteRequest)
        || session.Permissions.Contains(RemoteAttended)
        || item.TargetUserId == session.Id
        || item.TechnicianUserId == session.Id
        || item.RequestedByUserId == session.Id;

    private static bool HasMfa(ClaimsPrincipal principal)
    {
        // Prefer amr/acr claims when OIDC provides them; no MFA infra yet means false unless claim present.
        string? amr = principal.FindFirst("amr")?.Value;
        if (!string.IsNullOrWhiteSpace(amr)
            && amr.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(v => v.Equals("mfa", StringComparison.OrdinalIgnoreCase)
                    || v.Equals("otp", StringComparison.OrdinalIgnoreCase)
                    || v.Equals("hwk", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string? acr = principal.FindFirst("acr")?.Value;
        return !string.IsNullOrWhiteSpace(acr)
            && (acr.Contains("mfa", StringComparison.OrdinalIgnoreCase)
                || acr.Contains("urn:mace:incommon:iap:silver", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateTimestamp(string? timestampHeader, int skewSeconds)
    {
        if (!long.TryParse(timestampHeader, out long unix))
            return false;
        DateTimeOffset ts = DateTimeOffset.FromUnixTimeSeconds(unix);
        return Math.Abs((DateTimeOffset.UtcNow - ts).TotalSeconds) <= Math.Max(30, skewSeconds);
    }

    private static bool ValidateHmac(string? signatureHeader, string? timestampHeader, ReadOnlySpan<byte> body, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(timestampHeader))
            return false;
        string provided = signatureHeader.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            provided = provided["sha256=".Length..];

        byte[] key = Encoding.UTF8.GetBytes(secret);
        byte[] payload = Encoding.UTF8.GetBytes($"{timestampHeader}.");
        byte[] message = new byte[payload.Length + body.Length];
        payload.CopyTo(message, 0);
        body.CopyTo(message.AsSpan(payload.Length));
        byte[] hash = HMACSHA256.HashData(key, message);
        string expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static bool? ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement el) && (el.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? el.GetBoolean()
            : null;

    private static IResult SessionUnavailable() =>
        Results.Json(new { error = "session_unavailable" }, statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Validation(string message) =>
        Results.Json(new { error = "validation", message }, statusCode: StatusCodes.Status400BadRequest);

    private static IResult FromEx(Exception ex) =>
        Results.Json(new { error = "invalid_operation", message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);

    public sealed record CreateAttendedRemoteRequest(
        Guid ConfigurationItemId,
        Guid TargetUserId,
        string? Reason,
        Guid? TicketId,
        Guid? ChangeRequestId,
        string? RequestedPrivileges,
        Guid? TechnicianUserId);

    public sealed record CreateUnattendedRemoteRequest(
        Guid ConfigurationItemId,
        string? Reason,
        Guid? TicketId,
        Guid? ChangeRequestId,
        string? RequestedPrivileges,
        Guid? TechnicianUserId);

    public sealed record EndRemoteRequest(string? Reason);

    public sealed record SetRemoteMappingRequest(
        string? RemoteEngineNodeId,
        string? RemoteEngineProvider,
        bool UnattendedRemotePermitted,
        string? RowVersion);
}

public sealed class RemoteSessionPollingJob(RemoteSessionService sessions)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await sessions.ExpireDueAsync(cancellationToken);
        await sessions.PollActiveSessionsAsync(cancellationToken);
    }
}
