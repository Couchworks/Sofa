using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;

namespace Sofa.Identity.Workspaces;

public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeams(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/users", UsersOf);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/users/by-github-account/{accountId:long}", HolderOfGitHubAccount);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/teams", TeamsOf);
        endpoints.MapPost("/workspaces/{workspaceId:guid}/teams/{teamId:guid}/users", AddToTeam);
        endpoints.MapDelete("/workspaces/{workspaceId:guid}/teams/{teamId:guid}/users/{userId:guid}", RemoveFromTeam);
        return endpoints;
    }

    private static async Task<List<WorkspaceMember>> UsersOf(Guid workspaceId, IdentityDb db, CancellationToken cancellation) =>
        await (
            from workspaceUser in db.WorkspaceUsers
            join user in db.Users on workspaceUser.UserId equals user.Id
            where workspaceUser.WorkspaceId == workspaceId
            orderby user.Name
            select new WorkspaceMember(user.Id, user.Name, user.Email, user.GitHubLogin, workspaceUser.IsAdmin))
            .ToListAsync(cancellation);

    private static async Task<IResult> HolderOfGitHubAccount(
        Guid workspaceId, long accountId, IdentityDb db, CancellationToken cancellation)
    {
        var holder = await (
            from workspaceUser in db.WorkspaceUsers
            join user in db.Users on workspaceUser.UserId equals user.Id
            where workspaceUser.WorkspaceId == workspaceId && user.GitHubAccountId == accountId
            select new GitHubAccountHolder(user.Id, user.Name))
            .SingleOrDefaultAsync(cancellation);
        return holder is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No User of the Workspace holds that GitHub account.")
            : Results.Ok(holder);
    }

    private static async Task<List<WorkspaceTeam>> TeamsOf(Guid workspaceId, IdentityDb db, CancellationToken cancellation)
    {
        var teams = await db.Teams
            .Where(team => team.WorkspaceId == workspaceId)
            .OrderBy(team => team.CreatedAt).ThenBy(team => team.Id)
            .ToListAsync(cancellation);
        var members = await (
            from teamUser in db.TeamUsers
            join user in db.Users on teamUser.UserId equals user.Id
            where teamUser.WorkspaceId == workspaceId
            orderby user.Name
            select new { teamUser.TeamId, Member = new TeamMember(user.Id, user.Name, user.GitHubLogin) })
            .ToListAsync(cancellation);

        return teams
            .Select(team => new WorkspaceTeam(team.Id, team.Name,
                members.Where(member => member.TeamId == team.Id).Select(member => member.Member).ToList()))
            .ToList();
    }

    private static async Task<IResult> AddToTeam(
        Guid workspaceId, Guid teamId, TeamMemberToAdd added, IdentityDb db, TimeProvider time, CancellationToken cancellation)
    {
        if (!await db.Teams.AnyAsync(team => team.WorkspaceId == workspaceId && team.Id == teamId, cancellation))
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Team.");
        }

        if (!await db.WorkspaceUsers.AnyAsync(w => w.WorkspaceId == workspaceId && w.UserId == added.UserId, cancellation))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Only a User of the Workspace can join its Team.");
        }

        if (await db.TeamUsers.AnyAsync(t => t.TeamId == teamId && t.UserId == added.UserId, cancellation))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "They are in the Team already.");
        }

        db.TeamUsers.Add(new TeamUser { WorkspaceId = workspaceId, TeamId = teamId, UserId = added.UserId, JoinedAt = time.GetUtcNow() });
        await db.SaveChangesAsync(cancellation);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveFromTeam(
        Guid workspaceId, Guid teamId, Guid userId, IdentityDb db, CancellationToken cancellation)
    {
        var removed = await db.TeamUsers
            .Where(t => t.WorkspaceId == workspaceId && t.TeamId == teamId && t.UserId == userId)
            .ExecuteDeleteAsync(cancellation);
        return removed == 0
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "They are not in that Team.")
            : Results.NoContent();
    }
}
