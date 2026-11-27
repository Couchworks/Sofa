using Sofa.Identity.Workspaces;

namespace Sofa.UnitTests.Identity;

public class TheWorkspacesTeamTests
{
    private static readonly Guid Workspace = Guid.NewGuid();
    private static readonly Guid Ada = Guid.NewGuid();
    private static readonly Guid AddedBea = Guid.NewGuid();

    [Fact]
    public void The_Workspaces_Team_is_its_oldest()
    {
        var engines = new Team { WorkspaceId = Workspace, Name = "Engines", CreatedAt = DateTimeOffset.UnixEpoch };
        var looms = new Team { WorkspaceId = Workspace, Name = "Looms", CreatedAt = DateTimeOffset.UnixEpoch.AddDays(1) };

        Assert.Same(engines, TheWorkspacesTeam.Of([looms, engines]));
        Assert.Null(TheWorkspacesTeam.Of([]));
    }

    [Fact]
    public void Merging_an_added_User_carries_their_Teams_over_where_the_User_is_not_in_them_yet()
    {
        var engines = Guid.NewGuid();
        var looms = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var beasTeams = new[]
        {
            new TeamUser { WorkspaceId = Workspace, TeamId = engines, UserId = AddedBea },
            new TeamUser { WorkspaceId = Workspace, TeamId = looms, UserId = AddedBea },
            new TeamUser { WorkspaceId = elsewhere, TeamId = Guid.NewGuid(), UserId = AddedBea },
        };

        var carried = TheWorkspacesTeam.CarriedOver(beasTeams, Ada, new HashSet<Guid> { Workspace }, new HashSet<Guid> { looms }, DateTimeOffset.UnixEpoch)
            .ToList();

        var only = Assert.Single(carried);
        Assert.Equal((Workspace, engines, Ada), (only.WorkspaceId, only.TeamId, only.UserId));
    }
}
