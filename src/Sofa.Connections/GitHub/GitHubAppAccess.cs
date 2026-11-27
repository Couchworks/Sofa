using Microsoft.EntityFrameworkCore;
using Sofa.Connections.Credentials;
using Sofa.Connections.Persistence;

namespace Sofa.Connections.GitHub;

public sealed class GitHubAppAccess(ConnectionsDb db, ICredentialVault vault, TimeProvider time)
{
    public static CredentialPurpose PrivateKeyOf(long appId) => new($"github-app:{appId}:private-key");

    public static CredentialPurpose WebhookSecretOf(long appId) => new($"github-app:{appId}:webhook-secret");

    public static CredentialPurpose ClientSecretOf(long appId) => new($"github-app:{appId}:client-secret");

    public Task<GitHubApp?> FindApp(CancellationToken cancellation) =>
        db.GitHubApps.OrderBy(app => app.CreatedAt).FirstOrDefaultAsync(cancellation);

    public async Task<string> AppJwt(GitHubApp app, CancellationToken cancellation)
    {
        var privateKey = await vault.Open(app.SealedPrivateKey, PrivateKeyOf(app.Id), cancellation);
        return GitHub.AppJwt.Issue(app.ClientId, privateKey, time.GetUtcNow());
    }

    public ValueTask<string> WebhookSecret(GitHubApp app, CancellationToken cancellation) =>
        vault.Open(app.SealedWebhookSecret, WebhookSecretOf(app.Id), cancellation);

    public async Task<GitHubApp> Keep(ConvertedManifest converted, CancellationToken cancellation)
    {
        var app = new GitHubApp
        {
            Id = converted.Id,
            Slug = converted.Slug,
            Name = converted.Name,
            OwnerLogin = converted.Owner.Login,
            HtmlUrl = converted.HtmlUrl,
            ClientId = converted.ClientId,
            SealedPrivateKey = await vault.Seal(converted.Pem, PrivateKeyOf(converted.Id), cancellation),
            SealedWebhookSecret = await vault.Seal(converted.WebhookSecret ?? "", WebhookSecretOf(converted.Id), cancellation),
            SealedClientSecret = await vault.Seal(converted.ClientSecret, ClientSecretOf(converted.Id), cancellation),
            CreatedAt = time.GetUtcNow(),
        };
        db.GitHubApps.Add(app);
        await db.SaveChangesAsync(cancellation);
        return app;
    }
}
