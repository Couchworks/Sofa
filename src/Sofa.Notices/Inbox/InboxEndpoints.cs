using Microsoft.EntityFrameworkCore;
using Sofa.Notices.Data;
using Sofa.Notices.SignIns;

namespace Sofa.Notices.Inbox;

public sealed record InboxItemSummary(
    Guid Id,
    string Kind,
    string For,
    Guid SubjectId,
    string ProjectKey,
    string? WorkItemKey,
    string Title,
    string Detail,
    string Url,
    DateTimeOffset RaisedAt);

public static class InboxEndpoints
{
    public const string RunnersPage = "/runners";

    public static IEndpointRouteBuilder MapInbox(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/inbox/admins", (Guid workspaceId, NoticesDb db, CancellationToken cancellation) =>
            UnsettledAsync(db.InboxItems.Where(i => i.WorkspaceId == workspaceId && i.For == InboxAudience.TheWorkspacesAdmins), cancellation));
        endpoints.MapGet("/workspaces/{workspaceId:guid}/inbox/users/{userId:guid}", InboxOfAsync);
        return endpoints;
    }

    private static async Task<List<InboxItemSummary>> InboxOfAsync(
        Guid workspaceId, Guid userId, NoticesDb db, TimeProvider time, CancellationToken cancellation)
    {
        var items = await UnsettledAsync(
            db.InboxItems.Where(i => i.WorkspaceId == workspaceId && i.For == InboxAudience.TheUser && i.UserId == userId), cancellation);
        var signIns = await db.RunnerSignIns.AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId && s.UserId == userId)
            .ToListAsync(cancellation);
        var now = time.GetUtcNow();
        items.AddRange(signIns.Select(signIn => SignInItem(signIn, now)).OfType<InboxItemSummary>());
        return items.OrderByDescending(i => i.RaisedAt).ToList();
    }

    public static InboxItemSummary? SignInItem(RunnerSignIn signIn, DateTimeOffset now)
    {
        if (SignInWarnings.RefusesRuns(signIn))
        {
            return new InboxItemSummary(signIn.RunnerId, nameof(InboxItemKind.SubscriptionSignedOut), nameof(InboxAudience.TheUser),
                signIn.RunnerId, "", null, $"{signIn.RunnerName} is signed out",
                $"Its Runs are refused until it is signed in again. Run `{SignInWarnings.SignInCommand(signIn.RunnerId)}` on Sofa's box.",
                RunnersPage, signIn.RefusedARunAt!.Value);
        }

        if (SignInWarnings.EndsSoon(signIn, now))
        {
            var endsAt = signIn.EndsAt!.Value;
            return new InboxItemSummary(signIn.RunnerId, nameof(InboxItemKind.SubscriptionSignInEnding), nameof(InboxAudience.TheUser),
                signIn.RunnerId, "", null,
                endsAt <= now ? $"{signIn.RunnerName}'s sign-in has ended" : $"{signIn.RunnerName}'s sign-in ends soon",
                $"It ends at {endsAt:u}; after that its Runs are refused. Run `{SignInWarnings.SignInCommand(signIn.RunnerId)}` on Sofa's box to sign it in again.",
                RunnersPage, endsAt - SignInWarnings.WarnBefore);
        }

        return null;
    }

    private static async Task<List<InboxItemSummary>> UnsettledAsync(IQueryable<InboxItem> items, CancellationToken cancellation) =>
        (await items.AsNoTracking()
            .Where(i => i.SettledAt == null)
            .OrderByDescending(i => i.RaisedAt)
            .ToListAsync(cancellation))
        .Select(i => new InboxItemSummary(
            i.Id, i.Kind.ToString(), i.For.ToString(), i.SubjectId, i.ProjectKey, i.WorkItemKey, i.Title, i.Detail, i.Url, i.RaisedAt))
        .ToList();
}
