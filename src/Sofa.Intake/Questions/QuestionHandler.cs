using Sofa.Contracts.Intake;
using Sofa.Intake.Data;
using Sofa.Intake.GitHub;
using Sofa.Intake.Reports;

namespace Sofa.Intake.Questions;

public static class QuestionHandler
{
    public static async Task Handle(
        AskOnRequest asked,
        IntakeDb db,
        IRequestReporter reporter,
        SofaAddress sofa,
        TimeProvider time,
        ILogger<AskOnRequest> logger,
        CancellationToken cancellation)
    {
        if (await db.QuestionComments.FindAsync([asked.QuestionId], cancellation) is not null)
        {
            return;
        }

        if (await db.Requests.FindAsync([asked.RequestId], cancellation) is not { } request)
        {
            logger.LogWarning("Asked no Question for {Key}: Intake has no Request {RequestId}", asked.WorkItemKey, asked.RequestId);
            return;
        }

        var posted = await reporter.PostAsync(request.Issue,
            CommentBodies.Question(asked.Phase, asked.WorkItemKey, asked.Question, sofa.OfWorkItem(request.ProjectKey, asked.WorkItemKey)),
            cancellation);
        db.QuestionComments.Add(new QuestionComment
        {
            QuestionId = asked.QuestionId,
            RequestId = request.Id,
            Phase = asked.Phase,
            WorkItemKey = asked.WorkItemKey,
            CommentId = posted.CommentId,
            AskedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Asked Question {QuestionId} of {Key} on {Repository}#{Number}",
            asked.QuestionId, asked.WorkItemKey, request.Repository, request.Number);
    }
}
