using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Access;
using Sofa.Contracts.Identity;

namespace Sofa.AppGateway.Identity;

public static class TeamEndpoints
{
    public static RouteGroupBuilder MapTeams(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/users", (SignedInAdmin admin, IdentityUsers users, CancellationToken cancellation) =>
                users.UsersOfAsync(admin.WorkspaceId, cancellation))
            .WithName("GetWorkspaceUsers")
            .WithSummary("The Users of the Workspace.");

        workspace.MapGet("/teams", (SignedInAdmin admin, IdentityUsers users, CancellationToken cancellation) =>
                users.TeamsOfAsync(admin.WorkspaceId, cancellation))
            .WithName("GetTeams")
            .WithSummary("The Workspace's Teams and who is in each.");

        workspace.MapPost("/teams/{teamId:guid}/users", AddToTeam)
            .WithName("AddToTeam")
            .WithSummary("Adds a User of the Workspace to one of its Teams, so they may spend its team Runners.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        workspace.MapDelete("/teams/{teamId:guid}/users/{userId:guid}", RemoveFromTeam)
            .WithName("RemoveFromTeam")
            .WithSummary("Removes a User from a Team of the Workspace.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workspace;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AddToTeam(
        Guid teamId, TeamMemberToAdd added, SignedInAdmin admin, IdentityUsers users, CancellationToken cancellation)
    {
        var answer = await users.AddToTeamAsync(admin.WorkspaceId, teamId, added.UserId, cancellation);
        return answer.Succeeded ? TypedResults.NoContent() : TypedResults.Problem(statusCode: answer.Status, title: answer.Problem);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveFromTeam(
        Guid teamId, Guid userId, SignedInAdmin admin, IdentityUsers users, CancellationToken cancellation)
    {
        var answer = await users.RemoveFromTeamAsync(admin.WorkspaceId, teamId, userId, cancellation);
        return answer.Succeeded ? TypedResults.NoContent() : TypedResults.Problem(statusCode: answer.Status, title: answer.Problem);
    }
}
