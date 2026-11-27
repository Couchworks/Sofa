using Sofa.Contracts.Flow;
using Sofa.Contracts.Identity;
using Sofa.Flow.Projects;

namespace Sofa.UnitTests.Flow;

public class DefaultFlowTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid TeamId = Guid.NewGuid();

    private static readonly (IReadOnlyList<Agent> Agents, IReadOnlyList<Phase> Phases) Default =
        DefaultFlow.For(ProjectId, WorkspaceId, TeamId, DateTimeOffset.UnixEpoch);

    [Fact]
    public void A_new_Project_goes_Triage_Spec_Plan_Implement_every_Phase_Solo()
    {
        Assert.Equal([PhaseName.Triage, PhaseName.Spec, PhaseName.Plan, PhaseName.Implement],
            Default.Phases.OrderBy(p => p.Position).Select(p => p.Name));
        Assert.All(Default.Phases, phase => Assert.Equal(Pattern.Solo, phase.Pattern));
    }

    [Fact]
    public void Its_default_Agents_are_the_Triager_on_Haiku_and_the_Lead_and_Builder_on_Opus()
    {
        Assert.Equal(
            [(AgentRole.Triager, "haiku"), (AgentRole.Lead, "opus"), (AgentRole.Builder, "opus")],
            Default.Agents.Select(a => (a.Role, a.Model)));
        Assert.All(Default.Agents, agent =>
        {
            Assert.Equal(Vendor.Anthropic, agent.Vendor);
            Assert.Equal(TeamId, agent.OwnerTeamId);
            Assert.NotEmpty(agent.Instructions);
        });
    }

    [Theory]
    [InlineData(PhaseName.Triage, AgentRole.Triager, Gate.Auto, Sandbox.ReadOnly)]
    [InlineData(PhaseName.Spec, AgentRole.Lead, Gate.Approve, Sandbox.ReadOnly)]
    [InlineData(PhaseName.Plan, AgentRole.Lead, Gate.Approve, Sandbox.ReadOnly)]
    [InlineData(PhaseName.Implement, AgentRole.Builder, Gate.Auto, Sandbox.OwnBranch)]
    public void Each_Phase_casts_its_Agent_with_the_default_Flows_Gate_and_Sandbox(
        PhaseName name, AgentRole cast, Gate gate, Sandbox sandbox)
    {
        var phase = Default.Phases.Single(p => p.Name == name);

        Assert.Equal(cast, Default.Agents.Single(a => a.Id == phase.CastAgentId).Role);
        Assert.Equal(gate, phase.Gate);
        Assert.Equal(sandbox, phase.Sandbox);
    }

    [Fact]
    public void Each_default_Agent_is_registered_with_Identity_as_the_Teams()
    {
        var registrations = ProjectHandler.RegistrationsOf(Default.Agents).ToList();

        Assert.Equal(Default.Agents.Select(a => a.Id), registrations.Select(r => r.AgentId));
        Assert.All(registrations, registration =>
        {
            Assert.Equal(WorkspaceId, registration.WorkspaceId);
            Assert.Equal(AgentOwner.Team(TeamId), registration.Owner);
        });
    }
}
