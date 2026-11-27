using Microsoft.EntityFrameworkCore;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class GitHubInstallationHandler
{
    public static async Task<GitHubInstallationAdded> Handle(
        AddGitHubInstallation command,
        ConnectionsDb db,
        GitHubAppAccess access,
        GitHubApi gitHub,
        TimeProvider time,
        ILogger<AddGitHubInstallation> logger,
        CancellationToken cancellation)
    {
        var app = await access.FindApp(cancellation);
        if (app is null)
        {
            return new GitHubInstallationAdded(null, "Sofa's GitHub App hasn't been created yet.");
        }

        GitHubInstallation installation;
        try
        {
            installation = await gitHub.Installation(await access.AppJwt(app, cancellation), command.InstallationId, cancellation);
        }
        catch (GitHubRefused refused)
        {
            logger.LogWarning(refused, "GitHub doesn't know installation {InstallationId} of the App", command.InstallationId);
            return new GitHubInstallationAdded(null, "GitHub doesn't know that installation of Sofa's App.");
        }

        var connection = await db.Connections.SingleOrDefaultAsync(
            c => c.GitHubInstallationId == installation.Id, cancellation);
        if (connection is not null && connection.WorkspaceId != command.WorkspaceId)
        {
            return new GitHubInstallationAdded(null, "That installation is already a Connection of another Workspace.");
        }

        if (connection is null)
        {
            connection = new Connection
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = command.WorkspaceId,
                GitHubInstallationId = installation.Id,
                Account = installation.Account.Login,
                AccountType = installation.Account.Type,
                AddedAt = time.GetUtcNow(),
            };
            db.Connections.Add(connection);
            logger.LogInformation(
                "Installation {InstallationId} on {Account} is now Connection {ConnectionId} of Workspace {WorkspaceId}",
                installation.Id, installation.Account.Login, connection.Id, command.WorkspaceId);
        }

        connection.Account = installation.Account.Login;
        connection.AccountType = installation.Account.Type;
        connection.SuspendedAt = installation.SuspendedAt;
        await db.SaveChangesAsync(cancellation);

        return new GitHubInstallationAdded(connection.Id, Problem: null);
    }
}
