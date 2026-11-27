using System.Security.Claims;
using Sofa.Contracts.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.AgentGateway.Runs;

public static class RunTokenEndpoints
{
    public static IEndpointRouteBuilder MapRunTokenCheck(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/diagnostics/run", (ClaimsPrincipal run) => HolderOf(run))
            .RequireAuthorization(RunTokenPolicy.Name);
        return endpoints;
    }

    public static RunTokenHolder HolderOf(ClaimsPrincipal run) => new(
        Guid.Parse(run.FindFirstValue(SofaClaims.Run)!),
        Guid.Parse(run.FindFirstValue(Claims.Subject)!),
        run.FindFirstValue(Claims.Name) ?? "",
        Guid.Parse(run.FindFirstValue(SofaClaims.Workspace)!));
}

public sealed record RunTokenHolder(Guid RunId, Guid AgentId, string AgentName, Guid WorkspaceId);
