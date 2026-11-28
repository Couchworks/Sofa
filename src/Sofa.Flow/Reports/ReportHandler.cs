using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Intake;
using Sofa.Flow.Data;

namespace Sofa.Flow.Reports;

public static class ReportHandler
{
    public static async Task<PostReport?> Handle(
        ReportPhaseOnRequest report, FlowDb db, RecordsContributions records, ILogger<ReportPhaseOnRequest> logger, CancellationToken cancellation)
    {
        var workItem = await db.WorkItems.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == report.WorkItemId && w.WorkspaceId == report.WorkspaceId, cancellation);
        if (workItem is null)
        {
            return null;
        }

        var contribution = await records.OfAsync(report.WorkspaceId, report.RunId, cancellation);
        if (contribution is null && report.Merged is null)
        {
            logger.LogWarning("Records has no Contribution of Run {RunId}, so {Key}'s {Phase} isn't reported", report.RunId, workItem.Key, report.Phase);
            return null;
        }

        var summary = contribution is null ? null : ReportRules.SummaryOf(contribution.Markdown);
        var detail = contribution is null ? "" : ReportRules.DetailOf(contribution);
        return report.Merged is { } merged
            ? new PostReport(workItem.RequestId, workItem.Key, report.Phase, ReportRules.MergedSummary(workItem.Key, merged.Url, summary), detail)
            : new PostReport(workItem.RequestId, workItem.Key, report.Phase, summary!, detail);
    }
}
