using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Sofa.Flow.Projects;

namespace Sofa.Flow.WorkItems;

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItems(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/projects/{key}/work-items", WorkItemsOf);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/projects/{key}/work-items/{workItemKey}", async (
            Guid workspaceId, string key, string workItemKey, FlowDb db, WorkItemMoves moves, CancellationToken cancellation) =>
            await DetailsAsync(await moves.FindAsync(workspaceId, key, workItemKey, cancellation), db, cancellation));
        endpoints.MapGet("/workspaces/{workspaceId:guid}/work-items/{workItemId:guid}", async (
            Guid workspaceId, Guid workItemId, FlowDb db, WorkItemMoves moves, CancellationToken cancellation) =>
            await DetailsAsync(await moves.FindAsync(workspaceId, workItemId, cancellation), db, cancellation));
        return endpoints;
    }

    private static async Task<IResult> DetailsAsync(WorkItem? workItem, FlowDb db, CancellationToken cancellation)
    {
        if (workItem is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Work Item.");
        }

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.WorkItemId == workItem.Id)
            .OrderBy(q => q.AskedAt)
            .ToListAsync(cancellation);
        return Results.Ok(new WorkItemDetails(
            workItem.Id, workItem.Key, workItem.Title, workItem.ProjectId, workItem.RequestId, workItem.Request,
            workItem.DelegatingUserId, workItem.OpenedAt, questions.Select(q => q.Details).ToList(),
            workItem.State, workItem.Phase, workItem.Track, workItem.TrackChosenByAUser, workItem.Waiting,
            workItem.Runs.OrderBy(r => r.StartedAt).ThenBy(r => r.RunId).Select(r => r.Details).ToList(),
            workItem.PhaseEnds.OrderBy(e => e.EndedAt).Select(e => e.Details).ToList(),
            workItem.PullRequest, workItem.DoneAt));
    }

    private static async Task<IResult> WorkItemsOf(Guid workspaceId, string key, FlowDb db, CancellationToken cancellation)
    {
        var normalized = ProjectRules.NormalizeKey(key);
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Key == normalized, cancellation);
        if (project is null)
        {
            return Results.NotFound();
        }

        var workItems = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == project.Id)
            .OrderByDescending(w => w.Number)
            .ToListAsync(cancellation);
        return Results.Ok(workItems
            .Select(w => new WorkItemSummary(w.Id, w.Key, w.Title, w.RequestId, w.Request, w.DelegatingUserId, w.OpenedAt, w.State, w.Phase, w.Track))
            .ToList());
    }
}
