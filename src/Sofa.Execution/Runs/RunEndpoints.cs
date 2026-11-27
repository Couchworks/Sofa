using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Execution;
using Sofa.Execution.Data;

namespace Sofa.Execution.Runs;

public static class RunEndpoints
{
    public const int MostRunsListed = 100;

    public static IEndpointRouteBuilder MapRuns(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/runs", RunsOf);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/runs/{runId:guid}", async (
            Guid workspaceId, Guid runId, ExecutionDb db, CancellationToken cancellation) =>
            await db.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.WorkspaceId == workspaceId && r.Id == runId, cancellation) is { } run
                ? Results.Ok(run.Details)
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Run."));
        return endpoints;
    }

    private static async Task<List<RunDetails>> RunsOf(Guid workspaceId, ExecutionDb db, CancellationToken cancellation) =>
        (await db.Runs.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.QueuedAt)
            .Take(MostRunsListed)
            .ToListAsync(cancellation))
        .Select(run => run.Details)
        .ToList();
}
