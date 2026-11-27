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

    public static RunTokenHolder? TryHolderOf(ClaimsPrincipal run) =>
        Guid.TryParse(run.FindFirstValue(SofaClaims.Run), out var runId)
        && Guid.TryParse(run.FindFirstValue(Claims.Subject), out var agentId)
        && Guid.TryParse(run.FindFirstValue(SofaClaims.Workspace), out var workspaceId)
            ? new RunTokenHolder(runId, agentId, run.FindFirstValue(Claims.Name) ?? "", workspaceId)
            : null;

    public static RunTokenHolder HolderOf(ClaimsPrincipal run) => new(
        Guid.Parse(run.FindFirstValue(SofaClaims.Run)!),
        Guid.Parse(run.FindFirstValue(Claims.Subject)!),
        run.FindFirstValue(Claims.Name) ?? "",
        Guid.Parse(run.FindFirstValue(SofaClaims.Workspace)!));
}

public sealed record RunTokenHolder(Guid RunId, Guid AgentId, string AgentName, Guid WorkspaceId);
