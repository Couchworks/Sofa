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

    public static async Task Handle(
        QuestionAnsweredInSofa answered,
        IntakeDb db,
        IRequestReporter reporter,
        SofaAddress sofa,
        ILogger<QuestionAnsweredInSofa> logger,
        CancellationToken cancellation)
    {
        if (await db.QuestionComments.FindAsync([answered.QuestionId], cancellation) is not { } question || !question.IsOpen)
        {
            return;
        }

        if (await db.Requests.FindAsync([question.RequestId], cancellation) is not { } request)
        {
            return;
        }

        question.AnsweredInSofa = true;
        question.AnsweredByUserId = answered.AnsweredByUserId;
        question.Answer = answered.Answer;
        question.AnsweredAt = answered.AnsweredAt;
        await db.SaveChangesAsync(cancellation);

        var body = CommentBodies.QuestionAnsweredInSofa(answered.Phase, answered.WorkItemKey, answered.Question, answered.Answer,
            sofa.OfWorkItem(request.ProjectKey, answered.WorkItemKey));
        var edited = await reporter.UpdateAsync(new CommentRef(request.Issue, question.CommentId), body, cancellation);
        logger.LogInformation("Question {QuestionId} of {Key} was answered in Sofa, so a reply on {Repository}#{Number} no longer answers it{Edited}",
            question.QuestionId, answered.WorkItemKey, request.Repository, request.Number, edited ? "; its comment says so" : "");
    }
}
