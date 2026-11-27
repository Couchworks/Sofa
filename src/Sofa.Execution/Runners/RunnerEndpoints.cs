using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Execution;
using Sofa.Execution.Data;

namespace Sofa.Execution.Runners;

public static class RunnerEndpoints
{
    public static IEndpointRouteBuilder MapRunners(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/runners", RunnersSeenBy);
        endpoints.MapGet("/runners/{runnerId:guid}/sign-in", RunnerToSignIn);
        endpoints.MapPut("/runners/{runnerId:guid}/sign-in", RecordSignIn);
        return endpoints;
    }

    private static async Task<List<RunnerDetails>> RunnersSeenBy(
        Guid workspaceId,
        [FromQuery] Guid userId,
        [FromQuery(Name = "teamId")] Guid[]? teamIds,
        [FromQuery] bool? admin,
        ExecutionDb db,
        CancellationToken cancellation)
    {
        var teams = teamIds ?? [];
        var runners = await db.Runners.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId
                        && (r.OwnerUserId == userId || (r.OwnerTeamId != null && (admin == true || teams.Contains(r.OwnerTeamId.Value)))))
            .ToListAsync(cancellation);
        return runners
            .Where(runner => RunnerRules.IsSeenBy(runner, userId, teams, admin == true))
            .OrderBy(runner => runner.Owner.Kind).ThenBy(runner => runner.Name).ThenBy(runner => runner.CreatedAt)
            .Select(runner => runner.Details)
            .ToList();
    }

    private static async Task<IResult> RunnerToSignIn(Guid runnerId, ExecutionDb db, CancellationToken cancellation) =>
        await db.Runners.FindAsync([runnerId], cancellation) is { } runner
            ? Results.Ok(new RunnerToSignIn(runner.Id, runner.WorkspaceId, runner.Name, runner.Kind, runner.WorkerVolume))
            : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Sofa has no such Runner.");

    private static async Task<IResult> RecordSignIn(
        Guid runnerId, SignInSeen seen, ExecutionDb db, TimeProvider time, ILogger<SignInSeen> logger, CancellationToken cancellation)
    {
        var runner = await db.Runners.FindAsync([runnerId], cancellation);
        if (runner is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Sofa has no such Runner.");
        }

        if (runner.Kind != RunnerKind.Subscription)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Only a subscription Runner signs in; an API key Runner has its key.");
        }

        runner.Saw(seen, time.GetUtcNow());
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Subscription Runner {RunnerId} is {SignedIn}, until {EndsAt}",
            runner.Id, runner.SignedIn ? "signed in" : "not signed in", runner.SignInEndsAt);
        return Results.Ok(runner.Details);
    }
}
