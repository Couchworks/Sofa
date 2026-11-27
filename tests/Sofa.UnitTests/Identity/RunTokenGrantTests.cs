using OpenIddict.Abstractions;
using Sofa.Contracts.Identity;
using Sofa.Identity.Agents;
using Sofa.Identity.Tokens;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.UnitTests.Identity;

public class RunTokenGrantTests
{
    private static readonly Agent Builder = new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        Name = "Builder",
        OwnerUserId = Guid.NewGuid(),
    };

    [Fact]
    public void Reads_the_Run_and_its_Agent_from_the_token_request()
    {
        var runId = Guid.NewGuid();
        var request = new OpenIddictRequest
        {
            [RunTokens.RunIdParameter] = runId.ToString(),
            [RunTokens.AgentIdParameter] = Builder.Id.ToString(),
        };

        Assert.Equal(new RunTokenRequest(runId, Builder.Id), RunTokenGrant.Read(request));
    }

    [Theory]
    [InlineData(null, "8d0b2c0e-7a51-4bd4-9f39-0d7c6f1d3a10")]
    [InlineData("8d0b2c0e-7a51-4bd4-9f39-0d7c6f1d3a10", null)]
    [InlineData("not a guid", "8d0b2c0e-7a51-4bd4-9f39-0d7c6f1d3a10")]
    [InlineData("00000000-0000-0000-0000-000000000000", "8d0b2c0e-7a51-4bd4-9f39-0d7c6f1d3a10")]
    public void A_request_without_both_ids_is_no_Run_token_request(string? runId, string? agentId)
    {
        var request = new OpenIddictRequest();
        if (runId is not null) request[RunTokens.RunIdParameter] = runId;
        if (agentId is not null) request[RunTokens.AgentIdParameter] = agentId;

        Assert.Null(RunTokenGrant.Read(request));
    }

    [Fact]
    public void A_Run_token_names_its_Agent_its_Run_and_its_Workspace_for_the_Agent_Gateway_only()
    {
        var runId = Guid.NewGuid();

        var principal = RunTokenGrant.For(runId, Builder);

        Assert.Equal(Builder.Id.ToString(), principal.GetClaim(Claims.Subject));
        Assert.Equal("Builder", principal.GetClaim(Claims.Name));
        Assert.Equal(runId.ToString(), principal.GetClaim(SofaClaims.Run));
        Assert.Equal(Builder.WorkspaceId.ToString(), principal.GetClaim(SofaClaims.Workspace));
        Assert.Equal([RunTokens.Audience], principal.GetResources());
        Assert.Equal(RunTokenGrant.LongestExpectedRun, principal.GetAccessTokenLifetime());
    }

    [Fact]
    public void A_Run_tokens_claims_go_into_the_access_token_only() =>
        Assert.All(RunTokenGrant.For(Guid.NewGuid(), Builder).Claims,
            claim => Assert.Equal([Destinations.AccessToken], claim.GetDestinations()));
}
