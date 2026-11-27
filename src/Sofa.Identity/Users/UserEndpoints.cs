using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;

namespace Sofa.Identity.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/{id:guid}", async (Guid id, IdentityDb db, CancellationToken cancellation) =>
            await ProfileOfAsync(id, db, cancellation) is { } profile ? Results.Ok(profile) : Results.NotFound());
        return endpoints;
    }

    public static async Task<UserProfile?> ProfileOfAsync(Guid id, IdentityDb db, CancellationToken cancellation)
    {
        var user = await db.Users.FindAsync([id], cancellation);
        if (user is null)
        {
            return null;
        }

        var workspaces = await (
            from workspaceUser in db.WorkspaceUsers
            join workspace in db.Workspaces on workspaceUser.WorkspaceId equals workspace.Id
            where workspaceUser.UserId == id
            orderby workspace.Name
            select new { workspace.Id, workspace.Name, workspaceUser.IsAdmin }).ToListAsync(cancellation);

        var teams = await (
            from teamUser in db.TeamUsers
            join team in db.Teams on teamUser.TeamId equals team.Id
            where teamUser.UserId == id
            orderby team.Name
            select new { team.WorkspaceId, team.Id, team.Name }).ToListAsync(cancellation);

        return new UserProfile(user.Id, user.Name, user.Email, user.GitHubLogin, workspaces
            .Select(w => new UserWorkspace(w.Id, w.Name, w.IsAdmin, teams
                .Where(t => t.WorkspaceId == w.Id)
                .Select(t => new UserTeam(t.Id, t.Name))
                .ToList()))
            .ToList());
    }
}
