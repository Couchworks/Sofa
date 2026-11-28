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
        endpoints.MapGet("/workspaces/{workspaceId:guid}/work-items/{workItemId:guid}", WorkItemAsync);
        return endpoints;
    }

    private static async Task<IResult> WorkItemAsync(Guid workspaceId, Guid workItemId, FlowDb db, CancellationToken cancellation)
    {
        var workItem = await db.WorkItems.AsNoTracking()
            .SingleOrDefaultAsync(w => w.WorkspaceId == workspaceId && w.Id == workItemId, cancellation);
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
            workItem.DelegatingUserId, workItem.OpenedAt, questions.Select(q => q.Details).ToList()));
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
            .Select(w => new WorkItemSummary(w.Id, w.Key, w.Title, w.RequestId, w.Request, w.DelegatingUserId, w.OpenedAt))
            .ToList());
    }
}
