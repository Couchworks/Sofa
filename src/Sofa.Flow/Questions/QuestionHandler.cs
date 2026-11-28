using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Intake;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.Questions;

public static class QuestionHandler
{
    public static async Task<OutgoingMessages> Handle(
        RunEnded ended, FlowDb db, TimeProvider time, ILogger<RunEnded> logger, CancellationToken cancellation)
    {
        var toIntake = new OutgoingMessages();
        if (ended.Question is not { } asked)
        {
            return toIntake;
        }

        var workItem = await db.WorkItems.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == ended.WorkItemId && w.WorkspaceId == ended.WorkspaceId, cancellation);
        if (workItem is null)
        {
            logger.LogInformation("Run {RunId} asked Question {QuestionId} of no Work Item Flow has, so nobody is asked it",
                ended.RunId, asked.QuestionId);
            return toIntake;
        }

        if (await db.Questions.AnyAsync(q => q.Id == asked.QuestionId, cancellation))
        {
            return toIntake;
        }

        db.Questions.Add(new Question
        {
            Id = asked.QuestionId,
            WorkspaceId = workItem.WorkspaceId,
            WorkItemId = workItem.Id,
            RunId = ended.RunId,
            Phase = ended.Phase,
            Text = asked.Text,
            AskedAt = time.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(cancellation);
        }
        catch (DbUpdateException taken) when (taken.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return toIntake;
        }

        logger.LogInformation("Run {RunId} asked Question {QuestionId} of {Key} in its {Phase} Phase",
            ended.RunId, asked.QuestionId, workItem.Key, ended.Phase);
        toIntake.Add(new AskOnRequest(asked.QuestionId, workItem.RequestId, workItem.Key, ended.Phase, asked.Text));
        return toIntake;
    }

    public static async Task Handle(
        QuestionAnsweredOnRequest answered, FlowDb db, ILogger<QuestionAnsweredOnRequest> logger, CancellationToken cancellation)
    {
        logger.LogInformation(
            "Flow received the answer to Question {QuestionId} on Request {RequestId} from User {UserId}",
            answered.QuestionId, answered.RequestId, answered.AnsweredByUserId);
        var question = await db.Questions.SingleOrDefaultAsync(
            q => q.Id == answered.QuestionId && q.WorkspaceId == answered.WorkspaceId, cancellation);
        if (question is null)
        {
            logger.LogInformation("Flow asked no Question {QuestionId}, so its answer is kept nowhere", answered.QuestionId);
            return;
        }

        if (question.AnswerWith(answered.Answer, answered.AnsweredByUserId, answered.AnsweredAt))
        {
            await db.SaveChangesAsync(cancellation);
        }
    }
}
