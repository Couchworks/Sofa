using System.Security.Claims;
using Sofa.AgentGateway.Runs;
using Sofa.Contracts.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.UnitTests.AgentGateway;

public class RunTokenEndpointsTests
{
    [Fact]
    public void The_holder_of_a_Run_token_is_its_Agent_in_its_Run()
    {
        var (runId, agentId, workspaceId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var run = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(Claims.Subject, agentId.ToString()),
            new Claim(Claims.Name, "Builder"),
            new Claim(SofaClaims.Run, runId.ToString()),
            new Claim(SofaClaims.Workspace, workspaceId.ToString()),
        ], "test"));

        Assert.Equal(new RunTokenHolder(runId, agentId, "Builder", workspaceId), RunTokenEndpoints.HolderOf(run));
    }
}
