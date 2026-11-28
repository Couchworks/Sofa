using Sofa.AppGateway.Access;
using Sofa.AppGateway.Flow;

namespace Sofa.AppGateway.Identity;

public static class WorkspaceMemberEndpoints
{
    public static RouteGroupBuilder MapUsersOfTheWorkspace(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/user-names", UsersOf)
            .WithName("GetUserNames")
            .WithSummary("The Users of the Workspace by name, to say who delegated, answered or decided something.");
        return workspace;
    }

    private static async Task<List<DelegatingUser>> UsersOf(SignedInWorkspaceUser user, IdentityUsers identity, CancellationToken cancellation) =>
        (await identity.UsersOfAsync(user.WorkspaceId, cancellation))
        .Select(member => new DelegatingUser(member.UserId, member.Name))
        .OrderBy(member => member.Name, StringComparer.CurrentCulture)
        .ToList();
}
