using Sofa.AppGateway.Access;

namespace Sofa.AppGateway.Notices;

public static class InboxEndpoints
{
    public static RouteGroupBuilder MapInbox(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/inbox", InboxOf)
            .WithName("GetInbox")
            .WithSummary("The signed-in User's Inbox: what waits for them, such as Questions and Approve Gates, and for an Admin what waits for the Workspace's Admins, newest first.");
        return workspace;
    }

    private static async Task<List<InboxItem>> InboxOf(SignedInWorkspaceUser user, NoticesClient notices, CancellationToken cancellation)
    {
        var items = await notices.InboxOfAsync(user.WorkspaceId, user.UserId, cancellation);
        if (user.IsAdmin)
        {
            items.AddRange(await notices.AdminsInboxAsync(user.WorkspaceId, cancellation));
        }

        return items.OrderByDescending(item => item.RaisedAt).ToList();
    }
}
