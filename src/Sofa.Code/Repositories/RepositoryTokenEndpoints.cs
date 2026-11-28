using Microsoft.EntityFrameworkCore;
using Sofa.Code.Bindings;
using Sofa.Code.Data;
using Sofa.Code.GitHub;
using Sofa.Contracts.Code;

namespace Sofa.Code.Repositories;

public static class RepositoryTokenEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryTokens(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/runs/{runId:guid}/repository-token", IssueForARun);
        return endpoints;
    }

    private static async Task<IResult> IssueForARun(
        Guid runId,
        RepositoryTokenRequest request,
        CodeDb db,
        ConnectionsTokens connections,
        FlowBindings flow,
        TimeProvider time,
        ILogger<RepositoryTokenRequest> logger,
        CancellationToken cancellation)
    {
        if (SandboxAccess.For(request.Sandbox) is not { } access)
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                title: $"Code hands out no Repository token for the {request.Sandbox} Sandbox yet.");
        }

        if (SandboxAccess.ProblemWithTheBranch(request.OwnBranch) is { } branchProblem)
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: branchProblem);
        }

        var granted = await db.Grants.FindAsync([runId], cancellation);
        if (granted is { RunEndedAt: not null })
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The Run has ended, so Code hands it no Repository token.");
        }

        var binding = await db.Bindings.SingleOrDefaultAsync(
            b => b.ProjectId == request.ProjectId && b.WorkspaceId == request.WorkspaceId, cancellation);
        if (binding is null && await flow.CurrentAsync(request.WorkspaceId, request.ProjectId, cancellation) is { } bound)
        {
            binding = await ProjectBoundHandler.KeepAsync(bound, db, logger, cancellation);
        }

        if (binding is null || binding.Removed || binding.Repositories.Count == 0)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Code knows no Repositories of the Run's Project.");
        }

        var issued = await connections.IssueAsync(
            binding.ConnectionId, SandboxAccess.NamesInTheirAccount(binding.Repositories), access.Permissions, cancellation);
        if (issued.Token is not { } token)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Connections gave no token for {string.Join(", ", binding.Repositories)}: {issued.Problem}");
        }

        var pushesTo = access.Access == RepositoryAccess.PushItsOwnBranch ? request.OwnBranch : null;
        if (granted is not null)
        {
            granted.ExpiresAt = token.ExpiresAt;
            await db.SaveChangesAsync(cancellation);
        }
        else
        {
            db.Grants.Add(new RepositoryGrant
            {
                RunId = runId,
                WorkspaceId = request.WorkspaceId,
                ProjectId = request.ProjectId,
                Repositories = [.. binding.Repositories],
                Access = access.Access,
                OwnBranch = request.OwnBranch,
                IssuedAt = time.GetUtcNow(),
                ExpiresAt = token.ExpiresAt,
            });
            await db.SaveChangesAsync(cancellation);
        }

        logger.LogInformation("Run {RunId} may {Access} in {Repositories}{Branch}, until {ExpiresAt}",
            runId, access.Access, string.Join(", ", binding.Repositories), pushesTo is null ? "" : $", pushing {pushesTo}", token.ExpiresAt);
        return Results.Ok(new RepositoryToken(token.Token, token.ExpiresAt, binding.Repositories, access.Access, pushesTo));
    }
}
