using Sofa.Contracts.Execution;
using Sofa.Contracts.Intake;
using Sofa.Flow.Data;
using Sofa.Flow.Questions;
using Wolverine;

namespace Sofa.Flow.WorkItems;

public static class RunEndedHandler
{
    public static async Task<OutgoingMessages> Handle(
        RunEnded ended, FlowDb db, WorkItemMoves moves, ILogger<RunEnded> logger, CancellationToken cancellation)
    {
        var outgoing = new OutgoingMessages();
        if (await moves.FindAsync(ended.WorkspaceId, ended.WorkItemId, cancellation) is not { } workItem)
        {
            if (ended.Question is { } unasked)
            {
                logger.LogInformation("Run {RunId} asked Question {QuestionId} of no Work Item Flow has, so nobody is asked it",
                    ended.RunId, unasked.QuestionId);
            }

            return outgoing;
        }

        if (ended.Question is { } asked && await db.Questions.FindAsync([asked.QuestionId], cancellation) is null)
        {
            db.Questions.Add(new Question
            {
                Id = asked.QuestionId,
                WorkspaceId = workItem.WorkspaceId,
                WorkItemId = workItem.Id,
                RunId = ended.RunId,
                Phase = ended.Phase,
                Text = asked.Text,
                AskedAt = moves.Now,
            });
            logger.LogInformation("Run {RunId} asked Question {QuestionId} of {Key} in its {Phase} Phase",
                ended.RunId, asked.QuestionId, workItem.Key, ended.Phase);
            outgoing.Add(new AskOnRequest(asked.QuestionId, workItem.RequestId, workItem.Key, ended.Phase, asked.Text));
        }

        var steps = workItem.HearRunEnded(ended, await moves.FlowOfAsync(workItem.ProjectId, cancellation), moves.Now);
        outgoing.AddRange(await moves.MessagesForAsync(workItem, steps, cancellation));
        await db.SaveChangesAsync(cancellation);
        return outgoing;
    }
}
