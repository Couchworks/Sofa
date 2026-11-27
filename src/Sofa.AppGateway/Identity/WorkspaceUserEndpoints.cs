using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Admins;
using Sofa.Contracts.Identity;

namespace Sofa.AppGateway.Identity;

public static class WorkspaceUserEndpoints
{
    public static RouteGroupBuilder MapWorkspaceUsers(this RouteGroupBuilder workspace)
    {
        workspace.MapPost("/users", AddByGitHubLogin)
            .WithName("AddUserByGitHubLogin")
            .WithSummary("Adds someone to the Workspace by their GitHub login; they sign in with GitHub to join.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return workspace;
    }

    private static async Task<Results<Created<UserAdded>, ProblemHttpResult>> AddByGitHubLogin(
        AddUserByGitHubLogin added, SignedInAdmin admin, IdentityUsers users, CancellationToken cancellation)
    {
        var answer = await users.AddByGitHubLoginAsync(admin.WorkspaceId, added, cancellation);
        return answer.Value is { } user
            ? TypedResults.Created((string?)null, user)
            : TypedResults.Problem(statusCode: answer.Status, title: answer.Problem);
    }
}
