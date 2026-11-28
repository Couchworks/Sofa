using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.WorkItems;

public static class GateHandler
{
    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        ApprovePhase command, FlowDb db, WorkItemMoves moves, CancellationToken cancellation)
    {
        if (await moves.FindAsync(command.WorkspaceId, command.ProjectKey, command.WorkItemKey, cancellation) is not { } workItem)
        {
            return NoSuchWorkItem;
        }

        var flow = await moves.FlowOfAsync(workItem.ProjectId, cancellation);
        return await TakenAsync(workItem.Approve(command.Phase, command.ByUserId, moves.Now, flow), workItem, db, moves, cancellation);
    }

    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        SendPhaseBack command, FlowDb db, WorkItemMoves moves, CancellationToken cancellation)
    {
        if (await moves.FindAsync(command.WorkspaceId, command.ProjectKey, command.WorkItemKey, cancellation) is not { } workItem)
        {
            return NoSuchWorkItem;
        }

        if (WorkItemRules.CommentProblem(command.Comment) is { } problem)
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, problem), []);
        }

        return await TakenAsync(workItem.SendBack(command.Phase, command.ByUserId, command.Comment, moves.Now), workItem, db, moves, cancellation);
    }

    private static async Task<(FlowAnswer, OutgoingMessages)> TakenAsync(
        UsersDecision decision, WorkItem workItem, FlowDb db, WorkItemMoves moves, CancellationToken cancellation)
    {
        if (decision.Refusal is { } refusal)
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, refusal), []);
        }

        var outgoing = await moves.MessagesForAsync(workItem, decision.Steps, cancellation);
        await db.SaveChangesAsync(cancellation);
        return (FlowAnswer.Done, outgoing);
    }

    private static (FlowAnswer, OutgoingMessages) NoSuchWorkItem =>
        (FlowAnswer.Refused(FlowRefusal.NotFound, "The Workspace has no such Work Item."), []);
}
