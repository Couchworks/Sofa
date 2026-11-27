using Sofa.Contracts.Intake;
using Sofa.Intake.Data;
using Sofa.Intake.GitHub;

namespace Sofa.Intake.Reports;

public static class ReportHandler
{
    public static async Task Handle(
        PostReport report,
        IntakeDb db,
        IRequestReporter reporter,
        SofaAddress sofa,
        TimeProvider time,
        ILogger<PostReport> logger,
        CancellationToken cancellation)
    {
        if (await db.Requests.FindAsync([report.RequestId], cancellation) is not { } request)
        {
            logger.LogWarning("Posted no {Phase} Report for {Key}: Intake has no Request {RequestId}", report.Phase, report.WorkItemKey, report.RequestId);
            return;
        }

        var body = CommentBodies.Report(report.Phase, report.WorkItemKey, report.Summary, report.Detail,
            sofa.OfWorkItem(request.ProjectKey, report.WorkItemKey));
        var kept = await db.ReportComments.FindAsync([request.Id, report.Phase], cancellation);
        if (kept is not null && await reporter.UpdateAsync(new CommentRef(request.Issue, kept.CommentId), body, cancellation))
        {
            kept.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(cancellation);
            logger.LogInformation("Updated {Key}'s {Phase} Report on {Repository}#{Number}", report.WorkItemKey, report.Phase, request.Repository, request.Number);
            return;
        }

        var posted = await reporter.PostAsync(request.Issue, body, cancellation);
        if (kept is null)
        {
            db.ReportComments.Add(new ReportComment
            {
                RequestId = request.Id, Phase = report.Phase, CommentId = posted.CommentId, PostedAt = time.GetUtcNow(),
            });
        }
        else
        {
            kept.CommentId = posted.CommentId;
            kept.UpdatedAt = time.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Posted {Key}'s {Phase} Report on {Repository}#{Number}", report.WorkItemKey, report.Phase, request.Repository, request.Number);
    }
}
