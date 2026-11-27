namespace Sofa.Identity.Workspaces;

public static class TheWorkspacesTeam
{
    public static Team? Of(IEnumerable<Team> teamsOfTheWorkspace) =>
        teamsOfTheWorkspace.OrderBy(team => team.CreatedAt).ThenBy(team => team.Id).FirstOrDefault();

    public static IEnumerable<TeamUser> CarriedOver(
        IEnumerable<TeamUser> ofTheUserMergedAway, Guid into, IReadOnlySet<Guid> workspacesInto, IReadOnlySet<Guid> teamsInto, DateTimeOffset now) =>
        ofTheUserMergedAway
            .Where(membership => workspacesInto.Contains(membership.WorkspaceId) && !teamsInto.Contains(membership.TeamId))
            .Select(membership => new TeamUser
            {
                WorkspaceId = membership.WorkspaceId, TeamId = membership.TeamId, UserId = into, JoinedAt = now,
            });
}
