using Sofa.AgentGateway.Mcp;
using Sofa.AgentGateway.Runs;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;

namespace Sofa.UnitTests.AgentGateway;

public class OnlyWhileTheRunRunsTests
{
    private static readonly RunTokenHolder Holder = new(Guid.NewGuid(), Guid.NewGuid(), "Lead", Guid.NewGuid());

    private static RunDetails RunIn(RunState state, Guid? agentId = null) => new(
        Holder.RunId, Holder.WorkspaceId, Guid.NewGuid(), Guid.NewGuid(), PhaseName.Spec, agentId ?? Holder.AgentId, "opus", Sandbox.ReadOnly,
        Guid.NewGuid(), Priority.None, state, null, null, Guid.NewGuid(), 1, 2, null, null, false, null, null, DateTimeOffset.UnixEpoch, null, null);

    [Fact]
    public void A_running_Run_is_served()
    {
        Assert.Null(OnlyWhileTheRunRuns.WhyItIsNotServed(RunIn(RunState.Running), Holder));
    }

    [Theory]
    [InlineData(RunState.Finished)]
    [InlineData(RunState.Failed)]
    [InlineData(RunState.Stopped)]
    [InlineData(RunState.Waiting)]
    public void A_Run_that_is_not_running_is_not_served_whatever_its_token(RunState state)
    {
        Assert.Contains($"it is {state}", OnlyWhileTheRunRuns.WhyItIsNotServed(RunIn(state), Holder));
    }

    [Fact]
    public void A_token_naming_another_Agent_or_no_Run_Sofa_has_is_not_served()
    {
        Assert.NotNull(OnlyWhileTheRunRuns.WhyItIsNotServed(RunIn(RunState.Running, Guid.NewGuid()), Holder));
        Assert.NotNull(OnlyWhileTheRunRuns.WhyItIsNotServed(null, Holder));
    }
}
