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
        return endpoints;
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
