using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
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
using Qec.Itmg.ServiceDesk.Domain;
using Qec.Itmg.ServiceDesk.Services;
using Qec.Itmg.Host.ServiceDesk;

namespace Qec.Itmg.Host.RemoteSupport;

public static class RemoteSupportEndpoints
{
    public const string RemoteRequest = "remote.request";
    public const string RemoteAttended = "remote.attended";
    public const string RemoteUnattended = "remote.unattended";
    public const string RemoteAuditRead = "remote.audit.read";
    public const string RemoteAdmin = "remote.admin";
    public const string RemoteSelfRequest = "remote.self.request";

    public static IEndpointRouteBuilder MapRemoteSupportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/remote-support/readiness", (
            RemoteSessionService svc,
            IRemoteEndpointEnrollmentEngine enrollment,
            IOptions<RemoteSupportOptions> options,
            RemoteSupportHelperPackageService helperPackage) =>
        {
            RemoteEngineStatus engine = svc.GetEngineStatus();
            RemoteSupportOptions cfg = options.Value;
            AgentBootstrapInfo bootstrap = enrollment.GetAgentBootstrap();
            return Results.Ok(new
            {
                engine.Enabled,
                engine.Configured,
                engine.ProviderKind,
                engine.Status,
                engine.LastSuccessUtc,
                engine.LastFailureUtc,
                engine.LastErrorSummary,
                engine.UnattendedEnabled,
                agentEnrollmentAvailable = bootstrap.Available,
                sessionCreationAvailable = engine.Configured && (
                    string.Equals(engine.Status, "Healthy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(engine.Status, "Configured", StringComparison.OrdinalIgnoreCase)),
                helperArtifactAvailable = helperPackage.IsAvailable,
                meshDeviceGroupConfigured = cfg.HasMeshDeviceGroup,
            });
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
            RemoteSessionChatService chat,
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
                await chat.PostSystemMessageAsync(
                    created.Id,
                    RemoteSessionChatService.SystemEvents.Requested,
                    "Remote support requested.",
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
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            bool mfa = HasMfa(principal);
            try
            {
                RemoteSessionRequestDto? before = await svc.GetAsync(id, ct);
                string deviceLabel = "device";
                if (before?.RemoteEndpointId is Guid epId)
                {
                    RemoteEndpointDto? ep = await endpointsSvc.GetAsync(epId, ct);
                    if (!string.IsNullOrWhiteSpace(ep?.DeviceName))
                        deviceLabel = ep.DeviceName;
                }

                await chat.PostSystemMessageAsync(
                    id,
                    RemoteSessionChatService.SystemEvents.Connecting,
                    $"Connecting to {deviceLabel}",
                    ct);
                RemoteSessionRequestDto updated = await svc.StartAsync(
                    id,
                    session.Id,
                    session.Permissions.Contains(RemoteAttended),
                    session.Permissions.Contains(RemoteUnattended),
                    mfa,
                    ct);
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.Started, "Remote session started.", ct);
                await notifications.NotifySessionStartedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("engine", StringComparison.OrdinalIgnoreCase))
                {
                    await chat.PostSystemMessageAsync(
                        id, RemoteSessionChatService.SystemEvents.Failed,
                        "Remote connection is temporarily unavailable. You can continue chatting with IT.", ct);
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
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                RemoteSessionRequestDto updated = await svc.EndAsync(id, session.Id, byTechnician: true, req?.Reason, ct);
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.Ended, "Remote session ended.", ct);
                await notifications.NotifySessionEndedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteAttended);

        endpoints.MapGet("/api/v1/me/remote-support/onboarding", async (
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            EmployeeRemoteOnboardingService onboarding,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            return Results.Ok(await onboarding.GetAsync(session.Id, ct));
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/remote-support", async (
            CreateEmployeeRemoteHelpRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSessionChatService chat,
            RemoteEndpointService endpointsSvc,
            TicketService tickets,
            TicketNotificationService ticketNotifications,
            RemoteSupportNotificationService notifications,
            IRemoteCiLookup ciLookup,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!session.Permissions.Contains(RemoteSelfRequest)
                && !session.Permissions.Contains(RemoteRequest)
                && !session.Permissions.Contains(RemoteAdmin))
                return Results.Forbid();
            if (!string.Equals(session.UserType, "Employee", StringComparison.OrdinalIgnoreCase)
                && !session.Permissions.Contains(RemoteAdmin)
                && !session.Permissions.Contains(RemoteRequest))
                return Results.Json(new { error = "forbidden", message = "Active employee identity is required." }, statusCode: StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(req.Reason))
                return Validation("A short description is required.");

            try
            {
                Guid? ticketId = null;
                TicketDto? ticketDto = null;
                Ticket ticket = await tickets.CreateAsync(
                    TicketType.ServiceRequest,
                    TruncateTitle(req.Reason),
                    req.Reason.Trim(),
                    session.Id,
                    TicketPriority.Medium,
                    req.ConfigurationItemId,
                    "Remote Support",
                    cancellationToken: ct);
                ticketId = ticket.Id;
                ticketDto = await tickets.GetForRequesterAsync(ticket.Id, session.Id, ct);
                if (ticketDto is not null)
                    await ticketNotifications.NotifyTicketCreatedAsync(ticketDto, ct);

                RemoteSessionRequestDto created = await svc.CreateEmployeeSelfRequestAsync(
                    session.Id,
                    req.Reason.Trim(),
                    ticketId,
                    req.ConfigurationItemId,
                    ct);

                await chat.PostSystemMessageAsync(
                    created.Id,
                    RemoteSessionChatService.SystemEvents.SelfRequested,
                    "Support request created.",
                    ct);

                if (req.ConfigurationItemId is Guid managedCi)
                {
                    RemoteCiProjection? ci = await ciLookup.GetAsync(managedCi, ct);
                    string label = ci?.CiNumber ?? "Company device";
                    RemoteEndpointDto endpoint = await endpointsSvc.AttachManagedDeviceAsync(
                        created.Id,
                        session.Id,
                        managedCi,
                        label,
                        ci?.RemoteEngineNodeId,
                        ct);
                    if (endpoint.IsReadyForRemote)
                    {
                        await chat.PostSystemMessageAsync(
                            created.Id,
                            RemoteSessionChatService.SystemEvents.DeviceReady,
                            "Device ready for remote support.",
                            ct);
                    }
                    else
                    {
                        await chat.PostSystemMessageAsync(
                            created.Id,
                            RemoteSessionChatService.SystemEvents.AgentPreparing,
                            "Company device selected. Waiting for remote agent readiness.",
                            ct);
                    }
                }
                else
                {
                    await chat.PostSystemMessageAsync(
                        created.Id,
                        RemoteSessionChatService.SystemEvents.EnrollmentIssued,
                        "Support Helper ready to download when you prepare this computer.",
                        ct);
                }

                await notifications.NotifySelfRequestedAsync(created, session.DisplayName, ct);
                return Results.Created($"/api/v1/me/remote-support/{created.Id}", created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/me/remote-support/{id:guid}/enrollment", async (
            Guid id,
            HttpContext http,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                EnrollmentIssueResult issued = await endpointsSvc.IssueEnrollmentAsync(id, session.Id, ip, ct);
                await chat.PostSystemMessageAsync(
                    id,
                    RemoteSessionChatService.SystemEvents.EnrollmentIssued,
                    "One-time Support Helper enrollment issued.",
                    ct);
                return Results.Ok(issued);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/remote-support/{id:guid}/helper-package", async (
            Guid id,
            HttpContext http,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteEndpointService endpointsSvc,
            RemoteSupportHelperPackageService helperPackage,
            IOptions<RemoteSupportOptions> options,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!helperPackage.IsAvailable)
            {
                return Results.Json(
                    new { error = "helper_unavailable", message = "Support Helper is not available on this environment." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                EnrollmentIssueResult issued = await endpointsSvc.IssueEnrollmentAsync(id, session.Id, ip, ct);
                string publicBase = options.Value.PublicAppBaseUrl;
                if (string.IsNullOrWhiteSpace(publicBase))
                {
                    publicBase = $"{http.Request.Scheme}://{http.Request.Host.Value}";
                }

                (byte[] content, string fileName) = await helperPackage.BuildPackageAsync(issued, publicBase, ct);
                await chat.PostSystemMessageAsync(
                    id,
                    RemoteSessionChatService.SystemEvents.HelperDownloaded,
                    "Support Helper downloaded.",
                    ct);
                http.Response.Headers.CacheControl = "no-store";
                return Results.File(content, "application/zip", fileName);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/remote-support/sessions/{id:guid}/endpoints", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteEndpointService endpointsSvc,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (!CanView(session, item)) return Results.Forbid();
            return Results.Ok(await endpointsSvc.ListForSessionAsync(id, item.TargetUserId, ct));
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/select-endpoint", async (
            Guid id,
            SelectEndpointRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteEndpointService endpointsSvc,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            bool isSupport = session.Permissions.Contains(RemoteRequest)
                || session.Permissions.Contains(RemoteAttended)
                || session.Permissions.Contains(RemoteAdmin);
            if (!CanView(session, item)) return Results.Forbid();
            if (req.EndpointId == Guid.Empty) return Validation("EndpointId is required.");
            try
            {
                await endpointsSvc.BindEndpointToSessionAsync(id, req.EndpointId, session.Id, isSupport, ct);
                return Results.Ok(await svc.GetAsync(id, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/endpoints/{id:guid}/status", async (
            Guid id,
            ReportEndpointStatusRequest req,
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            if (!endpointsSvc.ValidateReportSecret(id, req.ReportSecret))
                return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            try
            {
                await endpointsSvc.ReportEndpointStatusAsync(id, req.EngineNodeId, req.ConnectionStatus, req.AgentVersion, ct);
                RemoteEndpointDto? ep = await endpointsSvc.GetAsync(id, ct);
                if (ep?.CurrentRemoteSessionRequestId is Guid sid && ep.IsReadyForRemote)
                {
                    await chat.PostSystemMessageAsync(
                        sid,
                        RemoteSessionChatService.SystemEvents.DeviceReady,
                        $"Computer ready: {ep.DeviceName}",
                        ct);
                }

                return Results.Ok(ep);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).AllowAnonymous();

        endpoints.MapPost("/api/v1/me/remote-support/{id:guid}/select-managed-device", async (
            Guid id,
            SelectManagedDeviceRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            IRemoteCiLookup ciLookup,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (req.ConfigurationItemId == Guid.Empty)
                return Validation("ConfigurationItemId is required.");
            try
            {
                RemoteCiProjection? ci = await ciLookup.GetAsync(req.ConfigurationItemId, ct)
                    ?? throw new InvalidOperationException("Device was not found.");
                RemoteEndpointDto endpoint = await endpointsSvc.AttachManagedDeviceAsync(
                    id,
                    session.Id,
                    req.ConfigurationItemId,
                    ci.CiNumber,
                    ci.RemoteEngineNodeId,
                    ct);
                await chat.PostSystemMessageAsync(
                    id,
                    endpoint.IsReadyForRemote
                        ? RemoteSessionChatService.SystemEvents.DeviceReady
                        : RemoteSessionChatService.SystemEvents.AgentPreparing,
                    endpoint.IsReadyForRemote
                        ? "Device ready for remote support."
                        : "Company device selected. Waiting for remote agent readiness.",
                    ct);
                return Results.Ok(endpoint);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/me/remote-support/{id:guid}/endpoint", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteEndpointService endpointsSvc,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (item.TargetUserId != session.Id) return Results.Forbid();
            RemoteEndpointDto? endpoint = await endpointsSvc.GetForSessionAsync(id, ct);
            return Results.Ok(endpoint);
        }).RequireAuthorization();

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
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                RemoteSessionRequestDto updated = await svc.AllowAsync(id, session.Id, ip, ct);
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.Allowed, "Remote access approved.", ct);
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
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                RemoteSessionRequestDto updated = await svc.DeclineAsync(id, session.Id, ip, ct);
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.Declined, "Remote access declined.", ct);
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
            RemoteSessionChatService chat,
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
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.Ended, "Remote session ended.", ct);
                await notifications.NotifySessionEndedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        // Session chat (employee + authorized IT)
        endpoints.MapGet("/api/v1/remote-support/sessions/{id:guid}/messages", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (!CanView(session, item)) return Results.Forbid();
            return Results.Ok(await chat.ListAsync(id, ct));
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/messages", async (
            Guid id,
            PostRemoteChatMessageRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (!CanChat(session, item)) return Results.Forbid();
            try
            {
                await chat.EnsureChatStartedAuditAsync(id, ct);
                RemoteSessionMessageDto created = await chat.PostUserMessageAsync(id, session.Id, req.MessageText, ct);
                await notifications.NotifyChatMessageAsync(item, session.Id, created.MessageText, ct);
                return Results.Ok(created);
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

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/take", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!session.Permissions.Contains(RemoteRequest)
                && !session.Permissions.Contains(RemoteAttended)
                && !session.Permissions.Contains(RemoteAdmin))
                return Results.Forbid();
            try
            {
                RemoteSessionRequestDto updated = await svc.AssignTechnicianAsync(id, session.Id, ct);
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.TechnicianJoined, "Technician joined the request.", ct);
                await notifications.NotifyTechnicianAssignedAsync(updated, session.DisplayName, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/assign", async (
            Guid id,
            AssignTechnicianRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!session.Permissions.Contains(RemoteAdmin) && !session.Permissions.Contains(RemoteRequest))
                return Results.Forbid();
            if (req.TechnicianUserId == Guid.Empty)
                return Validation("TechnicianUserId is required.");
            try
            {
                RemoteSessionRequestDto updated = await svc.AssignTechnicianAsync(id, req.TechnicianUserId, ct);
                await chat.PostSystemMessageAsync(
                    id, RemoteSessionChatService.SystemEvents.TechnicianJoined, "Technician assigned.", ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/sessions/{id:guid}/request-access", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteSessionChatService chat,
            RemoteSupportNotificationService notifications,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!session.Permissions.Contains(RemoteAttended)
                && !session.Permissions.Contains(RemoteRequest)
                && !session.Permissions.Contains(RemoteAdmin))
                return Results.Forbid();
            try
            {
                RemoteSessionRequestDto updated = await svc.RequestEmployeeAccessAsync(id, session.Id, ct);
                await chat.PostSystemMessageAsync(
                    id,
                    RemoteSessionChatService.SystemEvents.AccessRequested,
                    "IT is requesting permission to connect.",
                    ct);
                await notifications.NotifyRequestedAsync(updated, ct);
                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/remote-support/sessions/{id:guid}/endpoint", async (
            Guid id,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteSessionService svc,
            RemoteEndpointService endpointsSvc,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            RemoteSessionRequestDto? item = await svc.GetAsync(id, ct);
            if (item is null) return Results.NotFound();
            if (!CanView(session, item)) return Results.Forbid();
            return Results.Ok(await endpointsSvc.GetForSessionAsync(id, ct));
        }).RequireAuthorization();

        endpoints.MapGet("/api/v1/remote-support/endpoints", async (
            string? kind,
            string? status,
            bool? includeExpired,
            int? take,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            RemoteEndpointService endpointsSvc,
            CancellationToken ct) =>
        {
            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            if (!session.Permissions.Contains(RemoteAdmin)
                && !session.Permissions.Contains(RemoteRequest)
                && !session.Permissions.Contains(RemoteAuditRead))
                return Results.Forbid();
            return Results.Ok(await endpointsSvc.ListAsync(kind, status, includeExpired ?? false, take ?? 100, ct));
        }).RequireAuthorization();

        endpoints.MapPost("/api/v1/remote-support/endpoints/{id:guid}/link-ci", async (
            Guid id,
            LinkEndpointCiRequest req,
            IRemoteCiLookup ciLookup,
            RemoteEndpointService endpointsSvc,
            CancellationToken ct) =>
        {
            if (req.ConfigurationItemId == Guid.Empty)
                return Validation("ConfigurationItemId is required.");
            try
            {
                _ = await ciLookup.GetAsync(req.ConfigurationItemId, ct)
                    ?? throw new InvalidOperationException("Configuration item was not found.");
                return Results.Ok(await endpointsSvc.LinkEndpointToCiAsync(id, req.ConfigurationItemId, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteAdmin);

        endpoints.MapPost("/api/v1/remote-support/endpoints/{id:guid}/expire", async (
            Guid id,
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            try
            {
                RemoteEndpointDto? before = await endpointsSvc.GetAsync(id, ct);
                await endpointsSvc.ExpireEndpointAsync(id, ct);
                if (before?.CurrentRemoteSessionRequestId is Guid sessionId)
                {
                    await chat.PostSystemMessageAsync(
                        sessionId,
                        RemoteSessionChatService.SystemEvents.DeviceExpired,
                        "Temporary device access expired.",
                        ct);
                }

                return Results.Ok(new { expired = true });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequirePermission(RemoteAdmin);

        // Helper redeems one-time enrollment without browser cookies.
        endpoints.MapPost("/api/v1/remote-support/enrollments/redeem", async (
            RedeemEnrollmentHttpRequest req,
            HttpContext http,
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Token)
                || string.IsNullOrWhiteSpace(req.DeviceName)
                || string.IsNullOrWhiteSpace(req.OperatingSystem))
                return Validation("Token, DeviceName, and OperatingSystem are required.");
            try
            {
                string? ip = http.Connection.RemoteIpAddress?.ToString();
                EnrollmentRedeemResult redeemed = await endpointsSvc.RedeemEnrollmentAsync(
                    new EnrollmentRedeemRequest(
                        req.Token,
                        req.DeviceName,
                        req.OperatingSystem,
                        req.OperatingSystemVersion,
                        req.Architecture,
                        req.HelperVersion,
                        req.ReportedEngineNodeId,
                        req.AgentStatus),
                    ip,
                    ct);
                await chat.PostSystemMessageAsync(
                    redeemed.RemoteSessionRequestId,
                    RemoteSessionChatService.SystemEvents.DeviceRegistered,
                    $"Device detected: {redeemed.DeviceName} · {req.OperatingSystem} · {redeemed.ConnectionStatus}",
                    ct);
                if (redeemed.WaitingForRemoteAgent)
                {
                    await chat.PostSystemMessageAsync(
                        redeemed.RemoteSessionRequestId,
                        RemoteSessionChatService.SystemEvents.AgentPreparing,
                        "Your computer was detected, but remote connection is not ready yet. You can continue chatting with IT.",
                        ct);
                }
                else
                {
                    await chat.PostSystemMessageAsync(
                        redeemed.RemoteSessionRequestId,
                        RemoteSessionChatService.SystemEvents.DeviceReady,
                        "Device ready for remote support.",
                        ct);
                }

                return Results.Ok(redeemed);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).AllowAnonymous();

        // Development-only mock registration when helper binary is unavailable.
        endpoints.MapPost("/api/v1/me/remote-support/{id:guid}/dev-mock-endpoint", async (
            Guid id,
            DevMockEndpointRequest req,
            ClaimsPrincipal principal,
            ICurrentUserService currentUser,
            IOptions<RemoteSupportOptions> options,
            IHostEnvironment env,
            RemoteEndpointService endpointsSvc,
            RemoteSessionChatService chat,
            CancellationToken ct) =>
        {
            if (!env.IsDevelopment() || !options.Value.AllowDevelopmentMockEnrollment)
                return Results.Json(new { error = "not_available" }, statusCode: StatusCodes.Status404NotFound);

            CurrentUserDto? session = await currentUser.GetSessionAsync(principal, ct);
            if (session is null) return SessionUnavailable();
            try
            {
                EnrollmentIssueResult issued = await endpointsSvc.IssueEnrollmentAsync(
                    id, session.Id, "dev-mock", ct);
                EnrollmentRedeemResult redeemed = await endpointsSvc.RedeemEnrollmentAsync(
                    new EnrollmentRedeemRequest(
                        issued.Token,
                        string.IsNullOrWhiteSpace(req.DeviceName) ? "DEV-MOCK-PC" : req.DeviceName.Trim(),
                        string.IsNullOrWhiteSpace(req.OperatingSystem) ? "Windows 11 (Development)" : req.OperatingSystem.Trim(),
                        req.OperatingSystemVersion,
                        req.Architecture ?? "x64",
                        "dev-mock",
                        req.ReportedEngineNodeId,
                        "installing"),
                    "dev-mock",
                    ct);
                await chat.PostSystemMessageAsync(
                    id,
                    RemoteSessionChatService.SystemEvents.DeviceRegistered,
                    $"[Development] Device detected: {redeemed.DeviceName}",
                    ct);
                return Results.Ok(redeemed);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return FromEx(ex);
            }
        }).RequireAuthorization();

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

    private static bool CanChat(CurrentUserDto session, RemoteSessionRequestDto item) =>
        item.TargetUserId == session.Id
        || item.TechnicianUserId == session.Id
        || item.RequestedByUserId == session.Id
        || session.Permissions.Contains(RemoteAttended)
        || session.Permissions.Contains(RemoteRequest)
        || session.Permissions.Contains(RemoteAdmin);

    private static string TruncateTitle(string reason)
    {
        string trimmed = reason.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..77] + "...";
    }

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

    public sealed record CreateEmployeeRemoteHelpRequest(
        string Reason,
        Guid? ConfigurationItemId);

    public sealed record SelectManagedDeviceRequest(Guid ConfigurationItemId);

    public sealed record AssignTechnicianRequest(Guid TechnicianUserId);

    public sealed record LinkEndpointCiRequest(Guid ConfigurationItemId);

    public sealed record RedeemEnrollmentHttpRequest(
        string Token,
        string DeviceName,
        string OperatingSystem,
        string? OperatingSystemVersion,
        string? Architecture,
        string? HelperVersion,
        string? ReportedEngineNodeId,
        string? AgentStatus);

    public sealed record DevMockEndpointRequest(
        string? DeviceName,
        string? OperatingSystem,
        string? OperatingSystemVersion,
        string? Architecture,
        string? ReportedEngineNodeId);

    public sealed record SelectEndpointRequest(Guid EndpointId);

    public sealed record ReportEndpointStatusRequest(
        string ReportSecret,
        string? EngineNodeId,
        string? ConnectionStatus,
        string? AgentVersion);

    public sealed record CreateUnattendedRemoteRequest(
        Guid ConfigurationItemId,
        string? Reason,
        Guid? TicketId,
        Guid? ChangeRequestId,
        string? RequestedPrivileges,
        Guid? TechnicianUserId);

    public sealed record EndRemoteRequest(string? Reason);

    public sealed record PostRemoteChatMessageRequest(string MessageText);

    public sealed record SetRemoteMappingRequest(
        string? RemoteEngineNodeId,
        string? RemoteEngineProvider,
        bool UnattendedRemotePermitted,
        string? RowVersion);
}

public sealed class RemoteSessionPollingJob(
    RemoteSessionService sessions,
    RemoteSessionChatService chat,
    RemoteSupportNotificationService notifications,
    RemoteEndpointService endpoints)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RemoteSessionRequestDto> expired = await sessions.ExpireDueAsync(cancellationToken);
        foreach (RemoteSessionRequestDto item in expired)
        {
            await chat.PostSystemMessageAsync(
                item.Id,
                RemoteSessionChatService.SystemEvents.Expired,
                "Consent expired.",
                cancellationToken);
            await notifications.NotifyExpiredAsync(item, cancellationToken);
        }

        await endpoints.ExpireDueTemporaryEndpointsAsync(cancellationToken);
        await endpoints.SynchronizeActiveEndpointsAsync(cancellationToken);
        await sessions.PollActiveSessionsAsync(cancellationToken);
    }
}
