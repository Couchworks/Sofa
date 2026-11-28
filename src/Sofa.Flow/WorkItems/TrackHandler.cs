using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.WorkItems;

public static class TrackHandler
{
    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        ChangeTrack command, FlowDb db, WorkItemMoves moves, ILogger<ChangeTrack> logger, CancellationToken cancellation)
    {
        if (await moves.FindAsync(command.WorkspaceId, command.ProjectKey, command.WorkItemKey, cancellation) is not { } workItem)
        {
            return (FlowAnswer.Refused(FlowRefusal.NotFound, "The Workspace has no such Work Item."), []);
        }

        var decision = workItem.ChangeTrack(command.Track);
        if (decision.Refusal is { } refusal)
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, refusal), []);
        }

        var outgoing = await moves.MessagesForAsync(workItem, decision.Steps, cancellation);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("User {UserId} put {Key} on the {Track} Track", command.ByUserId, workItem.Key, command.Track);
        return (FlowAnswer.Done, outgoing);
    }
}
