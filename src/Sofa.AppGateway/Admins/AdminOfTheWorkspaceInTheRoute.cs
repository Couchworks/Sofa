using Sofa.AppGateway.Identity;

namespace Sofa.AppGateway.Admins;

public sealed class AdminOfTheWorkspaceInTheRoute(WorkspaceAdmins admins) : IFindSignedInAdmin
{
    public async Task<SignedInAdmin?> FindAsync(HttpContext context)
    {
        if (SignedInUser.IdOf(context.User) is not { } userId
            || !Guid.TryParse(context.GetRouteValue(AdminsOnly.WorkspaceRouteValue) as string, out var workspaceId))
        {
            return null;
        }

        return await admins.IsAdminAsync(userId, workspaceId, context.RequestAborted)
            ? new SignedInAdmin(userId, workspaceId)
            : null;
    }
}

public sealed class WorkspaceAdmins(IdentityUsers users)
{
    public async Task<bool> IsAdminAsync(Guid userId, Guid workspaceId, CancellationToken cancellation) =>
        await users.ProfileOfAsync(userId, cancellation) is { } profile
        && profile.Workspaces.Any(workspace => workspace.Id == workspaceId && workspace.IsAdmin);
}
