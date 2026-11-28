using Sofa.Contracts.Execution;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.WorkItems;

public static class RetryHandler
{
    public static async Task<OutgoingMessages> Handle(
        RunRetriedByAUser retried, FlowDb db, WorkItemMoves moves, ILogger<RunRetriedByAUser> logger, CancellationToken cancellation)
    {
        if (await moves.FindAsync(retried.WorkspaceId, retried.WorkItemId, cancellation) is not { } workItem)
        {
            return [];
        }

        var steps = workItem.HearRetriedByAUser(retried, moves.Now);
        if (workItem.CurrentRunId != retried.RunId)
        {
            return [];
        }

        var outgoing = await moves.MessagesForAsync(workItem, steps, cancellation);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("{Key} follows Run {RunId}, User {UserId}'s Retry of Run {RetryOf}",
            workItem.Key, retried.RunId, retried.ByUserId, retried.RetryOf);
        return outgoing;
    }
}
