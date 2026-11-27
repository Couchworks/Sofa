using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts.Intake;
using Sofa.Notices.Data;

namespace Sofa.Notices.Inbox;

public static class InboxHandler
{
    public static async Task Handle(
        RequestAwaitingDelegation awaiting, NoticesDb db, TimeProvider time, ILogger<RequestAwaitingDelegation> logger, CancellationToken cancellation)
    {
        if (await db.InboxItems.AnyAsync(i => i.Kind == InboxItemKind.RequestAwaitingDelegation && i.SubjectId == awaiting.RequestId, cancellation))
        {
            return;
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
            return;
        }

        logger.LogInformation("Put {Repository}#{Number} in the Inbox of Workspace {WorkspaceId}'s Admins: it waits for a delegating User",
            awaiting.Request.Repository, awaiting.Request.Number, awaiting.WorkspaceId);
    }
}
