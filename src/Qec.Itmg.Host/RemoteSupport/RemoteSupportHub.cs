using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Qec.Itmg.Identity.CurrentUser;
using Qec.Itmg.RemoteSupport.Services;
using System.Security.Claims;

namespace Qec.Itmg.Host.RemoteSupport;

[Authorize]
public sealed class RemoteSupportHub(
    ICurrentUserService currentUser,
    RemoteSessionService sessions,
    RemoteSessionChatService chat) : Hub
{
    public const string HubPath = "/hubs/remote-support";

    public static string GroupName(Guid sessionId) => $"remote-session:{sessionId:N}";

    public async Task JoinSession(Guid sessionId)
    {
        CurrentUserDto? me = await currentUser.GetSessionAsync(Context.User ?? new ClaimsPrincipal());
        if (me is null)
            throw new HubException("session_unavailable");

        RemoteSessionRequestDto? item = await sessions.GetAsync(sessionId, Context.ConnectionAborted);
        if (item is null)
            throw new HubException("not_found");

        if (!CanAccess(me, item))
            throw new HubException("forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        await chat.EnsureChatStartedAuditAsync(sessionId);
    }

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    private static bool CanAccess(CurrentUserDto me, RemoteSessionRequestDto item)
    {
        if (item.TargetUserId == me.Id) return true;
        if (item.TechnicianUserId == me.Id || item.RequestedByUserId == me.Id) return true;
        return me.Permissions.Any(p =>
            p is "remote.request" or "remote.attended" or "remote.audit.read" or "remote.admin");
    }
}

public sealed class SignalRRemoteSupportChatNotifier(IHubContext<RemoteSupportHub> hub) : IRemoteSupportChatNotifier
{
    public Task MessageAddedAsync(RemoteSessionMessageDto message, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(RemoteSupportHub.GroupName(message.RemoteSessionRequestId))
            .SendAsync("remoteChatMessage", message, cancellationToken);
}
