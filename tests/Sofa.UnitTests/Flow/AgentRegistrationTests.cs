using Sofa.Contracts.Flow;
using Sofa.Contracts.Identity;
using Sofa.Flow.Projects;

namespace Sofa.UnitTests.Flow;

public class AgentRegistrationTests
{
    private static Agent NewAgent() => DefaultFlow.For(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch).Agents[0];

    [Fact]
    public void A_new_Agent_waits_for_Identity_to_register_it()
    {
        Assert.Equal(AgentRegistrationState.Pending, NewAgent().Registration);
    }

    [Fact]
    public void An_Agent_Identity_refuses_keeps_the_reason_until_it_is_asked_again()
    {
        var agent = NewAgent();

        agent.HeardFromIdentity(AgentRegistration.Refused(agent.Id, "Another Agent already has that id."));
        var refused = (agent.Registration, agent.RegistrationRefusedBecause);
        agent.AskIdentityAgain();
        var askedAgain = (agent.Registration, agent.RegistrationRefusedBecause);
        agent.HeardFromIdentity(AgentRegistration.Accepted(agent.Id));

        Assert.Equal((AgentRegistrationState.Refused, "Another Agent already has that id."), refused);
        Assert.Equal((AgentRegistrationState.Pending, (string?)null), askedAgain);
        Assert.Equal((AgentRegistrationState.Registered, (string?)null), (agent.Registration, agent.RegistrationRefusedBecause));
    }
}
