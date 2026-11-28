using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Sofa.Flow.Data;
using Sofa.Flow.WorkItems;
using Wolverine;

namespace Sofa.Flow.Questions;

public static class QuestionHandler
{
    public static async Task<OutgoingMessages> Handle(
        QuestionAnsweredOnRequest answered, FlowDb db, WorkItemMoves moves, ILogger<QuestionAnsweredOnRequest> logger, CancellationToken cancellation)
    {
        logger.LogInformation(
            "Flow received the answer to Question {QuestionId} on Request {RequestId} from User {UserId}",
            answered.QuestionId, answered.RequestId, answered.AnsweredByUserId);
        var question = await db.Questions.SingleOrDefaultAsync(
            q => q.Id == answered.QuestionId && q.WorkspaceId == answered.WorkspaceId, cancellation);
        if (question is null)
        {
            logger.LogInformation("Flow asked no Question {QuestionId}, so its answer is kept nowhere", answered.QuestionId);
            return [];
        }

        var (_, outgoing) = await AnswerAsync(question, answered.Answer, answered.AnsweredByUserId, answered.AnsweredAt, db, moves, cancellation);
        return outgoing;
    }

    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        AnswerQuestion command, FlowDb db, WorkItemMoves moves, CancellationToken cancellation)
    {
        if (await moves.FindAsync(command.WorkspaceId, command.ProjectKey, command.WorkItemKey, cancellation) is not { } workItem)
        {
            return (FlowAnswer.Refused(FlowRefusal.NotFound, "The Workspace has no such Work Item."), []);
        }

        var question = await db.Questions.SingleOrDefaultAsync(q => q.Id == command.QuestionId && q.WorkItemId == workItem.Id, cancellation);
        if (question is null)
        {
            return (FlowAnswer.Refused(FlowRefusal.NotFound, $"{workItem.Key} has no such Question."), []);
        }

        if (string.IsNullOrWhiteSpace(command.Answer) || command.Answer.Length > QuestionRules.LongestAnswer)
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, $"An answer has 1 to {QuestionRules.LongestAnswer} characters."), []);
        }

        var (answer, outgoing) = await AnswerAsync(question, command.Answer, command.ByUserId, moves.Now, db, moves, cancellation);
        if (answer.Refusal is null)
        {
            outgoing.Add(new QuestionAnsweredInSofa(
                question.Id, workItem.RequestId, workItem.Key, question.Phase, question.Text, command.ByUserId, question.Answer!,
                question.AnsweredAt!.Value));
        }

        return (answer, outgoing);
    }

    private static async Task<(FlowAnswer, OutgoingMessages)> AnswerAsync(
        Question question, string answer, Guid byUserId, DateTimeOffset at, FlowDb db, WorkItemMoves moves, CancellationToken cancellation)
    {
        if (!question.AnswerWith(answer, byUserId, at))
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, "The Question is already answered."), []);
        }

        var workItem = (await moves.FindAsync(question.WorkspaceId, question.WorkItemId, cancellation))!;
        var steps = workItem.HearQuestionAnswered(question.Id, moves.Now);
        var outgoing = await moves.MessagesForAsync(workItem, steps, cancellation);
        await db.SaveChangesAsync(cancellation);
        return (FlowAnswer.Done, outgoing);
    }
}
