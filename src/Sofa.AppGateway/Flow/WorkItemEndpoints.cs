using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Access;
using Sofa.AppGateway.Identity;
using Sofa.Contracts.Flow;

namespace Sofa.AppGateway.Flow;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItems(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/projects/{key}/work-items", WorkItemsOf)
            .WithName("GetWorkItems")
            .WithSummary("The Project's Work Items, newest first, each with its Request and its delegating User.")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return workspace;
    }

    private static async Task<Results<Ok<List<WorkItemOfProject>>, ProblemHttpResult>> WorkItemsOf(
        string key, SignedInWorkspaceUser user, FlowClient flow, IdentityUsers identity, CancellationToken cancellation)
    {
        if (await flow.WorkItemsOfAsync(user.WorkspaceId, key, cancellation) is not { } workItems)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Project.");
        }

        var users = workItems.Any(w => w.DelegatingUserId is not null)
            ? (await identity.UsersOfAsync(user.WorkspaceId, cancellation)).ToDictionary(u => u.UserId, u => u.Name)
            : [];
        return TypedResults.Ok(workItems
            .Select(w => new WorkItemOfProject(
                w.Key,
                w.Title,
                w.RequestId,
                w.Request,
                w.DelegatingUserId is { } delegating
                    ? new DelegatingUser(delegating, users.GetValueOrDefault(delegating, "a User no longer in the Workspace"))
                    : null,
                w.OpenedAt))
            .ToList());
    }
}

public sealed record WorkItemOfProject(
    string Key, string Title, Guid RequestId, RequestRef Request, DelegatingUser? DelegatedBy, DateTimeOffset OpenedAt);

public sealed record DelegatingUser(Guid UserId, string Name);
