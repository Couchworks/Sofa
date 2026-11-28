using Sofa.Contracts.Code;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.WorkItems;

public static class PullRequestHandler
{
    public static Task<OutgoingMessages> Handle(PullRequestOpened opened, FlowDb db, WorkItemMoves moves, ILogger<PullRequestOpened> logger, CancellationToken cancellation) =>
        HearAsync(opened, db, moves, logger, cancellation);

    public static Task<OutgoingMessages> Handle(PullRequestMerged merged, FlowDb db, WorkItemMoves moves, ILogger<PullRequestMerged> logger, CancellationToken cancellation) =>
        HearAsync(merged, db, moves, logger, cancellation);

    public static Task<OutgoingMessages> Handle(
        PullRequestClosedUnmerged closed, FlowDb db, WorkItemMoves moves, ILogger<PullRequestClosedUnmerged> logger, CancellationToken cancellation) =>
        HearAsync(closed, db, moves, logger, cancellation);

    private static async Task<OutgoingMessages> HearAsync(
        IPullRequestEvent heard, FlowDb db, WorkItemMoves moves, ILogger logger, CancellationToken cancellation)
    {
        var pull = heard.PullRequest;
        if (await moves.FindAsync(pull.WorkspaceId, pull.ProjectKey, pull.WorkItemKey, cancellation) is not { } workItem
            || workItem.ProjectId != pull.ProjectId)
        {
            logger.LogInformation("Flow has no Work Item {Key} for pull request {Repository}#{Number}", pull.WorkItemKey, pull.Repository, pull.Number);
            return [];
        }

        var steps = workItem.HearPullRequest(new WorkItemPullRequest(pull.Repository, pull.Number, pull.Url, pull.State), moves.Now);
        var outgoing = await moves.MessagesForAsync(workItem, steps, cancellation);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Flow heard {Key}'s pull request {Repository}#{Number} is {State}", workItem.Key, pull.Repository, pull.Number, pull.State);
        return outgoing;
    }
}
