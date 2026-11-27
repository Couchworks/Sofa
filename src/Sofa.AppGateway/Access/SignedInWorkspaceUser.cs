using Sofa.AppGateway.Identity;

namespace Sofa.AppGateway.Access;

public sealed record SignedInWorkspaceUser(Guid UserId, Guid WorkspaceId, bool IsAdmin, IReadOnlyList<Guid> TeamIds)
{
    public static ValueTask<SignedInWorkspaceUser?> BindAsync(HttpContext context) =>
        ValueTask.FromResult(context.Features.Get<SignedInWorkspaceUser>());
}

public sealed record SignedInAdmin(Guid UserId, Guid WorkspaceId)
{
    public static ValueTask<SignedInAdmin?> BindAsync(HttpContext context) =>
        ValueTask.FromResult(context.Features.Get<SignedInAdmin>());
}

public interface IFindSignedInWorkspaceUser
{
    Task<SignedInWorkspaceUser?> FindAsync(HttpContext context);
}

public sealed class WorkspaceUserInTheRoute(IdentityUsers users) : IFindSignedInWorkspaceUser
{
    public async Task<SignedInWorkspaceUser?> FindAsync(HttpContext context)
    {
        if (SignedInUser.IdOf(context.User) is not { } userId
            || !Guid.TryParse(context.GetRouteValue(WorkspaceAccess.WorkspaceRouteValue) as string, out var workspaceId))
        {
            return null;
        }

        var profile = await users.ProfileOfAsync(userId, context.RequestAborted);
        return profile?.Workspaces.SingleOrDefault(workspace => workspace.Id == workspaceId) is { } workspace
            ? new SignedInWorkspaceUser(userId, workspaceId, workspace.IsAdmin, workspace.Teams.Select(team => team.Id).ToList())
            : null;
    }
}

public sealed class WorkspaceAdmins(IdentityUsers users)
{
    public async Task<bool> IsAdminAsync(Guid userId, Guid workspaceId, CancellationToken cancellation) =>
        await users.ProfileOfAsync(userId, cancellation) is { } profile
        && profile.Workspaces.Any(workspace => workspace.Id == workspaceId && workspace.IsAdmin);
}
