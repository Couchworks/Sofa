using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class GitHubAppCreationHandler
{
    private const int StateBytes = 32;

    public static async Task<GitHubAppCreationStarted> Handle(
        StartGitHubAppCreation command,
        ConnectionsDb db,
        GitHubAppAccess access,
        GitHubAppManifests manifests,
        IOptions<GitHubOptions> github,
        TimeProvider time,
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

        var now = time.GetUtcNow();
        await db.GitHubAppCreations.Where(c => c.ExpiresAt < now).ExecuteDeleteAsync(cancellation);

        var state = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(StateBytes));
        db.GitHubAppCreations.Add(new GitHubAppCreation
        {
            State = state,
            WorkspaceId = command.WorkspaceId,
            ExpiresAt = now + github.Value.GitHubAppCreationLifetime,
        });
        await db.SaveChangesAsync(cancellation);

        return new GitHubAppCreationStarted(
            manifests.FormAction(state, organisation).ToString(),
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
        var started = await db.GitHubAppCreations.FindAsync([command.State], cancellation);
        if (started is null || started.ExpiresAt < time.GetUtcNow())
        {
            return new GitHubAppCreationFinished("GitHub came back with a creation Sofa didn't start, or one that has expired.");
        }

        db.GitHubAppCreations.Remove(started);
        await db.SaveChangesAsync(cancellation);

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
        logger.LogInformation("Created GitHub App {Slug} ({AppId}) owned by {Owner}", app.Slug, app.Id, app.OwnerLogin);
        return new GitHubAppCreationFinished(Problem: null);
    }
}
