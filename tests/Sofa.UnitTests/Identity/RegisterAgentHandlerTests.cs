using Sofa.Contracts.Identity;
using Sofa.Identity.Agents;

namespace Sofa.UnitTests.Identity;

public class RegisterAgentHandlerTests
{
    private static readonly Agent Registered = new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        Name = "Lead",
        OwnerTeamId = Guid.NewGuid(),
    };

    [Fact]
    public void Registering_the_same_Agent_again_is_the_same_Agent()
    {
        var again = new RegisterAgent(Registered.Id, Registered.WorkspaceId, " Lead ", AgentOwner.Team(Registered.OwnerTeamId!.Value));

        Assert.True(RegisterAgentHandler.IsTheSameAgent(Registered, again));
    }

    [Fact]
    public void Another_owner_under_the_same_id_is_another_Agent()
    {
        var other = new RegisterAgent(Registered.Id, Registered.WorkspaceId, "Lead", AgentOwner.User(Guid.NewGuid()));

        Assert.False(RegisterAgentHandler.IsTheSameAgent(Registered, other));
    }

    [Fact]
    public void An_Agent_is_owned_by_its_User_or_its_Team()
    {
        var userId = Guid.NewGuid();
        var ownedByUser = new Agent { Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), Name = "Builder", OwnerUserId = userId };

        Assert.Equal(AgentOwner.User(userId), ownedByUser.Owner);
        Assert.Equal(AgentOwner.Team(Registered.OwnerTeamId!.Value), Registered.Owner);
    }
}
