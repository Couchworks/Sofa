using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Sofa.Flow.Projects;
using Sofa.Flow.Reports;
using Sofa.Flow.Runs;
using Wolverine;
using static Sofa.Flow.WorkItems.FlowStep;

namespace Sofa.Flow.WorkItems;

public sealed class WorkItemMoves(FlowDb db, TimeProvider time, ILogger<WorkItemMoves> logger)
{
    public DateTimeOffset Now => time.GetUtcNow();

    public Task<WorkItem?> FindAsync(Guid workspaceId, Guid workItemId, CancellationToken cancellation) =>
        WithItsRunsAndPhaseEnds().SingleOrDefaultAsync(w => w.Id == workItemId && w.WorkspaceId == workspaceId, cancellation);

    public async Task<WorkItem?> FindAsync(Guid workspaceId, string projectKey, string workItemKey, CancellationToken cancellation)
    {
        var key = ProjectRules.NormalizeKey(projectKey);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Key == key, cancellation);
        return project is null
            ? null
            : await WithItsRunsAndPhaseEnds().SingleOrDefaultAsync(
                w => w.ProjectId == project.Id && w.Key == workItemKey.Trim().ToUpperInvariant(), cancellation);
    }

    public async Task<FlowOfAProject> FlowOfAsync(Guid projectId, CancellationToken cancellation) =>
        new(await db.Phases.AsNoTracking().Where(p => p.ProjectId == projectId)
            .Select(p => new PhaseInFlow(p.Name, p.Position, p.Gate))
            .ToListAsync(cancellation));

    public async Task<OutgoingMessages> MessagesForAsync(WorkItem workItem, IReadOnlyList<FlowStep> steps, CancellationToken cancellation)
    {
        var messages = new OutgoingMessages();
        if (steps.Count == 0)
        {
            return messages;
        }

        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == workItem.ProjectId, cancellation);
        foreach (var step in steps)
        {
            switch (step)
            {
                case EnterPhase entered:
                    messages.Add(new WorkItemEnteredPhase(workItem.Id, workItem.WorkspaceId, workItem.RequestId, workItem.Key, entered.Phase));
                    break;
                case StartRunOf start:
                    messages.Add(await StartRunAsync(project, workItem, start, cancellation));
                    break;
                case ReportPhase report:
                    messages.Add(new ReportPhaseOnRequest(workItem.WorkspaceId, workItem.Id, report.Phase, report.RunId, Merged: null));
                    break;
                case ReportMerged merged:
                    messages.Add(new ReportPhaseOnRequest(
                        workItem.WorkspaceId, workItem.Id, PhaseName.Implement, merged.ImplementedByRunId, merged.PullRequest));
                    break;
                case AwaitUser awaiting:
                    messages.Add(new WorkItemAwaitsUser(awaiting.WaitId, workItem.WorkspaceId, workItem.ProjectId, project.Key,
                        workItem.Id, workItem.Key, workItem.Title, workItem.DelegatingUserId!.Value, awaiting.Kind, awaiting.Why,
                        awaiting.Url, Now));
                    break;
                case FlowStep.StopAwaiting stopped:
                    messages.Add(new WorkItemNoLongerAwaitsUser(stopped.WaitId, workItem.WorkspaceId, workItem.Id, Now));
                    break;
                case ReachMainBranch:
                    messages.Add(new WorkItemReachedMainBranch(workItem.Id, workItem.WorkspaceId, workItem.RequestId, workItem.Key));
                    break;
            }
        }

        logger.LogInformation("{Key} is {State}{Phase}: {Steps}", workItem.Key, workItem.State,
            workItem.Phase is { } phase ? $" in {phase}" : "", string.Join(", ", steps.Select(s => s.GetType().Name)));
        return messages;
    }

    private async Task<StartRun> StartRunAsync(Project project, WorkItem workItem, StartRunOf start, CancellationToken cancellation)
    {
        var phase = await db.Phases.AsNoTracking().SingleAsync(p => p.ProjectId == project.Id && p.Name == start.Phase, cancellation);
        var agent = await db.Agents.AsNoTracking().SingleAsync(a => a.ProjectId == project.Id && a.Id == phase.CastAgentId, cancellation);
        return new StartRun(
            start.RunId,
            workItem.WorkspaceId,
            project.Id,
            workItem.Id,
            phase.Name,
            agent.Id,
            agent.Vendor,
            agent.Model,
            phase.Sandbox,
            workItem.DelegatingUserId!.Value,
            Priority.None,
            Briefs.For(project, workItem, phase, agent),
            RunLimits.DefaultRetryLimit,
            workItem.Key);
    }

    private IQueryable<WorkItem> WithItsRunsAndPhaseEnds() =>
        db.WorkItems.Include(w => w.Runs).Include(w => w.PhaseEnds);
}
