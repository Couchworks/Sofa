using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Sofa.Contracts.Notices;
using Sofa.Notices.Data;
using Wolverine;

namespace Sofa.Notices.Inbox;

public static class InboxHandler
{
    public static async Task<OutgoingMessages> Handle(
        RequestAwaitingDelegation awaiting, NoticesDb db, TimeProvider time, ILogger<RequestAwaitingDelegation> logger, CancellationToken cancellation)
    {
        if (await db.InboxItems.AnyAsync(i => i.Kind == InboxItemKind.RequestAwaitingDelegation && i.SubjectId == awaiting.RequestId, cancellation))
        {
            return [];
        }

        db.InboxItems.Add(new InboxItem
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = awaiting.WorkspaceId,
            Kind = InboxItemKind.RequestAwaitingDelegation,
            For = InboxAudience.TheWorkspacesAdmins,
            SubjectId = awaiting.RequestId,
            ProjectId = awaiting.ProjectId,
            ProjectKey = awaiting.ProjectKey,
            Title = awaiting.Title,
            Detail = $"{awaiting.Request.Repository}#{awaiting.Request.Number} was labelled by {awaiting.MarkedByGitHubLogin}, " +
                "whose GitHub account is linked to no User of the Workspace, so it waits for a delegating User.",
            Url = awaiting.Request.Url,
            RaisedAt = time.GetUtcNow(),
        });

        try
        {
            await db.SaveChangesAsync(cancellation);
        }
        catch (DbUpdateException raised) when (raised.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return [];
        }

        logger.LogInformation("Put {Repository}#{Number} in the Inbox of Workspace {WorkspaceId}'s Admins: it waits for a delegating User",
            awaiting.Request.Repository, awaiting.Request.Number, awaiting.WorkspaceId);
        return [new InboxChanged(awaiting.WorkspaceId, UserId: null)];
    }

    public static async Task<OutgoingMessages> Handle(
        WorkItemAwaitsUser awaits, NoticesDb db, ILogger<WorkItemAwaitsUser> logger, CancellationToken cancellation)
    {
        var kind = KindOf(awaits.Kind);
        if (await db.InboxItems.AnyAsync(i => i.Kind == kind && i.SubjectId == awaits.WaitId, cancellation))
        {
            return [];
        }

        db.InboxItems.Add(new InboxItem
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = awaits.WorkspaceId,
            Kind = kind,
            For = InboxAudience.TheUser,
            UserId = awaits.UserId,
            SubjectId = awaits.WaitId,
            ProjectId = awaits.ProjectId,
            ProjectKey = awaits.ProjectKey,
            WorkItemKey = awaits.WorkItemKey,
            Title = $"{awaits.WorkItemKey}: {awaits.Title}",
            Detail = awaits.Why.Length <= InboxRules.LongestDetail ? awaits.Why : awaits.Why[..(InboxRules.LongestDetail - 1)] + "…",
            Url = awaits.Url,
            RaisedAt = awaits.Since,
        });

        try
        {
            await db.SaveChangesAsync(cancellation);
        }
        catch (DbUpdateException raised) when (raised.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return [];
        }

        logger.LogInformation("Put {Key} in the Inbox of User {UserId}: {Kind}", awaits.WorkItemKey, awaits.UserId, kind);
        return [new InboxChanged(awaits.WorkspaceId, awaits.UserId)];
    }

    public static async Task<OutgoingMessages> Handle(WorkItemNoLongerAwaitsUser settled, NoticesDb db, CancellationToken cancellation)
    {
        var items = await db.InboxItems
            .Where(i => i.SubjectId == settled.WaitId && i.WorkspaceId == settled.WorkspaceId && i.SettledAt == null)
            .ToListAsync(cancellation);
        foreach (var item in items)
        {
            item.SettledAt = settled.At;
        }

        await db.SaveChangesAsync(cancellation);
        var changed = new OutgoingMessages();
        changed.AddRange(items.Select(i => new InboxChanged(i.WorkspaceId, i.For == InboxAudience.TheUser ? i.UserId : null)).Distinct());
        return changed;
    }

    public static InboxItemKind KindOf(WaitKind kind) => kind switch
    {
        WaitKind.Question => InboxItemKind.Question,
        WaitKind.Approval => InboxItemKind.Approval,
        WaitKind.RunGoneWrong => InboxItemKind.RunGoneWrong,
        WaitKind.PullRequestClosedUnmerged => InboxItemKind.PullRequestClosedUnmerged,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Notices knows no such wait."),
    };
}

public static class InboxRules
{
    public const int LongestDetail = 2000;
}
