using Microsoft.AspNetCore.SignalR;
using Sofa.AppGateway.Access;

namespace Sofa.AppGateway.Live;

public sealed class LiveHub : Hub
{
    public const string Path = "/live";

    public override async Task OnConnectedAsync()
    {
        if (Context.GetHttpContext()?.Features.Get<SignedInWorkspaceUser>() is not { } user)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, LiveGroups.OfTheWorkspace(user.WorkspaceId));
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveGroups.OfTheUser(user.WorkspaceId, user.UserId));
        if (user.IsAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, LiveGroups.OfTheAdmins(user.WorkspaceId));
        }

        await base.OnConnectedAsync();
    }
}

public static class LiveGroups
{
    public static string OfTheWorkspace(Guid workspaceId) => $"workspace:{workspaceId}";

    public static string OfTheUser(Guid workspaceId, Guid userId) => $"workspace:{workspaceId}:user:{userId}";

    public static string OfTheAdmins(Guid workspaceId) => $"workspace:{workspaceId}:admins";
}

public static class LiveMessages
{
    public const string WorkItemChanged = "workItemChanged";
    public const string RunChanged = "runChanged";
    public const string InboxChanged = "inboxChanged";
}

public sealed record WorkItemChangedLive(string ProjectKey, string WorkItemKey);

public sealed record RunChangedLive(Guid RunId, Guid WorkItemId, string State);
