using Microsoft.EntityFrameworkCore;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class ConnectionEndpoints
{
    public static IEndpointRouteBuilder MapConnections(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/github-app", GitHubAppStatus);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/connections", ConnectionsOf);
        endpoints.MapPost("/connections/{connectionId:guid}/installation-tokens", IssueInstallationToken);
        return endpoints;
    }

    private static async Task<GitHubAppStatus> GitHubAppStatus(
        GitHubAppAccess access,
        PublicAddress publicAddress,
        Microsoft.Extensions.Options.IOptions<GitHubOptions> github,
        CancellationToken cancellation)
    {
        var app = await access.FindApp(cancellation);
        return new GitHubAppStatus(
            Created: app is not null,
            Name: app?.Name,
            HtmlUrl: app?.HtmlUrl,
            InstallUrl: app is null ? null : new Uri(github.Value.WebAddress, $"apps/{app.Slug}/installations/new").ToString(),
            PublicAddress: publicAddress.Address.ToString(),
            GitHubCanReachPublicAddress: publicAddress.GitHubCanReachIt);
    }

    private static async Task<List<ConnectionSummary>> ConnectionsOf(
        Guid workspaceId, ConnectionsDb db, CancellationToken cancellation) =>
        await db.Connections
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderBy(c => c.AddedAt)
            .Select(c => new ConnectionSummary(c.Id, c.System, c.Account, c.AccountType, c.AddedAt, c.SuspendedAt != null))
            .ToListAsync(cancellation);

    private static async Task<IResult> IssueInstallationToken(
        Guid connectionId,
        InstallationTokenRequest? scope,
        ConnectionsDb db,
        GitHubAppAccess access,
        GitHubApi gitHub,
        CancellationToken cancellation)
    {
        var connection = await db.Connections.FindAsync([connectionId], cancellation);
        if (connection is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such Connection.");
        }

        if (connection.SuspendedAt is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The Connection's installation is suspended on GitHub.");
        }

        var app = await access.FindApp(cancellation);
        if (app is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Sofa's GitHub App hasn't been created yet.");
        }

        var created = await gitHub.CreateInstallationToken(
            await access.AppJwt(app, cancellation), connection.GitHubInstallationId, scope ?? new(), cancellation);
        return Results.Ok(new InstallationToken(created.Token, created.ExpiresAt));
    }
}
