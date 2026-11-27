using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Sofa.Contracts.Identity;
using Sofa.Identity.Agents;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.Identity.Tokens;

public sealed record RunTokenRequest(Guid RunId, Guid AgentId);

public static class RunTokenGrant
{
    public static readonly TimeSpan LongestExpectedRun = TimeSpan.FromHours(12);

    public static RunTokenRequest? Read(OpenIddictRequest request) =>
        Guid.TryParse((string?)request[RunTokens.RunIdParameter], out var runId)
        && Guid.TryParse((string?)request[RunTokens.AgentIdParameter], out var agentId)
        && runId != Guid.Empty && agentId != Guid.Empty
            ? new RunTokenRequest(runId, agentId)
            : null;

    public static ClaimsPrincipal For(Guid runId, Agent agent)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, agent.Id.ToString())
            .SetClaim(Claims.Name, agent.Name)
            .SetClaim(SofaClaims.Run, runId.ToString())
            .SetClaim(SofaClaims.Workspace, agent.WorkspaceId.ToString());

        identity.SetScopes(RunTokens.Scope);
        identity.SetResources(RunTokens.Audience);
        identity.SetAccessTokenLifetime(LongestExpectedRun);
        identity.SetDestinations(_ => [Destinations.AccessToken]);

        return new ClaimsPrincipal(identity);
    }
}
