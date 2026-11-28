using Microsoft.EntityFrameworkCore;
using Sofa.Notices.Data;

namespace Sofa.Notices.Inbox;

public sealed record InboxItemSummary(
    Guid Id, string Kind, string For, Guid SubjectId, string ProjectKey, string Title, string Detail, string Url, DateTimeOffset RaisedAt);

public static class InboxEndpoints
{
    public static IEndpointRouteBuilder MapInbox(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/inbox/admins", (Guid workspaceId, NoticesDb db, CancellationToken cancellation) =>
            UnsettledAsync(db.InboxItems.Where(i => i.WorkspaceId == workspaceId && i.For == InboxAudience.TheWorkspacesAdmins), cancellation));
        endpoints.MapGet("/workspaces/{workspaceId:guid}/inbox/users/{userId:guid}", (Guid workspaceId, Guid userId, NoticesDb db, CancellationToken cancellation) =>
            UnsettledAsync(db.InboxItems.Where(i => i.WorkspaceId == workspaceId && i.For == InboxAudience.TheUser && i.UserId == userId), cancellation));
        return endpoints;
    }

    private static async Task<List<InboxItemSummary>> UnsettledAsync(IQueryable<InboxItem> items, CancellationToken cancellation) =>
        (await items.AsNoTracking()
            .Where(i => i.SettledAt == null)
            .OrderByDescending(i => i.RaisedAt)
            .ToListAsync(cancellation))
        .Select(i => new InboxItemSummary(
            i.Id, i.Kind.ToString(), i.For.ToString(), i.SubjectId, i.ProjectKey, i.Title, i.Detail, i.Url, i.RaisedAt))
        .ToList();
}
