using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Access;
using Sofa.Contracts.Code;

namespace Sofa.AppGateway.Code;

public static class PullRequestEndpoints
{
    public static RouteGroupBuilder MapPullRequests(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/projects/{key}/work-items/{workItemKey}/pull-requests", PullRequestsOf)
            .WithName("GetPullRequestsOfWorkItem")
            .WithSummary("The pull requests naming the Work Item in their title or branch, oldest first, each as GitHub has it now.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return workspace;
    }

    private static async Task<Results<Ok<List<PullRequestOfWorkItem>>, ProblemHttpResult>> PullRequestsOf(
        string key, string workItemKey, SignedInWorkspaceUser user, CodeClient code, CancellationToken cancellation) =>
        await code.PullRequestsOfAsync(user.WorkspaceId, key, workItemKey, cancellation) is { } pulls
            ? TypedResults.Ok(pulls)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Project.");
}
