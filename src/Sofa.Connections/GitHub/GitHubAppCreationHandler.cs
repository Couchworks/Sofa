using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class GitHubAppCreationHandler
{
    public static async Task<GitHubAppCreationStarted> Handle(
        StartGitHubAppCreation command,
        GitHubAppAccess access,
        GitHubAppManifests manifests,
        CancellationToken cancellation)
    {
        if (await access.FindApp(cancellation) is not null)
        {
            return new GitHubAppCreationStarted(null, null, "Sofa's GitHub App has already been created.");
        }

        var organisation = string.IsNullOrWhiteSpace(command.Organisation) ? null : command.Organisation.Trim();
        if (organisation is not null && !GitHubAppManifests.IsAccountLogin(organisation))
        {
            return new GitHubAppCreationStarted(null, null, $"'{organisation}' is not a GitHub organisation's name.");
        }

        return new GitHubAppCreationStarted(
            manifests.FormAction(command.State, organisation).ToString(),
            manifests.Manifest(organisation, command.Name),
            Problem: null);
    }

    public static async Task<GitHubAppCreationFinished> Handle(
        FinishGitHubAppCreation command,
        ConnectionsDb db,
        GitHubAppAccess access,
        GitHubApi gitHub,
        TimeProvider time,
        ILogger<FinishGitHubAppCreation> logger,
        CancellationToken cancellation)
    {
        if (!await GitHubReturns.UseOnce(command.StartedBy, db, time, cancellation))
        {
            return new GitHubAppCreationFinished(GitHubReturns.AlreadyUsed);
        }

        if (await access.FindApp(cancellation) is not null)
        {
            return new GitHubAppCreationFinished("Sofa's GitHub App has already been created.");
        }

        ConvertedManifest converted;
        try
        {
            converted = await gitHub.ConvertManifest(command.Code, cancellation);
        }
        catch (GitHubRefused refused)
        {
            logger.LogWarning(refused, "GitHub refused to turn the manifest into an App");
            return new GitHubAppCreationFinished("GitHub refused to hand over the new App's credentials.");
        }

        var app = await access.Keep(converted, cancellation);
        logger.LogInformation("Created GitHub App {Slug} ({AppId}) owned by {Owner}, for Admin {AdminUserId} of Workspace {WorkspaceId}",
            app.Slug, app.Id, app.OwnerLogin, command.StartedBy.AdminUserId, command.StartedBy.WorkspaceId);
        return new GitHubAppCreationFinished(Problem: null);
    }
}
