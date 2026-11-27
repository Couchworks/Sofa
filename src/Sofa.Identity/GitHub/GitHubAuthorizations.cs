using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;

namespace Sofa.Identity.GitHub;

public sealed class GitHubAuthorization
{
    public required string StateHash { get; init; }

    public Guid? LinkingUserId { get; init; }

    public string? ReturnUrl { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record GitHubAuthorizationStarted(string AuthorizeUrl, string State);

public sealed class GitHubAuthorizations(IdentityDb db, ConnectionsGitHub connections, TimeProvider time)
{
    private const int StateBytes = 32;

    public async Task<GitHubAuthorizationStarted?> StartAsync(Guid? linkingUserId, string? returnUrl, CancellationToken cancellation)
    {
        if (await connections.SignInAppAsync(cancellation) is not { } app)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(StateBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        await db.GitHubAuthorizations.Where(a => a.ExpiresAt < now).ExecuteDeleteAsync(cancellation);
        db.GitHubAuthorizations.Add(new GitHubAuthorization
        {
            StateHash = Hash(state),
            LinkingUserId = linkingUserId,
            ReturnUrl = returnUrl,
            ExpiresAt = now + GitHubSignInState.Lifetime,
        });
        await db.SaveChangesAsync(cancellation);

        var authorizeUrl = $"{app.AuthorizeAddress}?client_id={Uri.EscapeDataString(app.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(app.CallbackAddress)}&state={Uri.EscapeDataString(state)}";
        return new GitHubAuthorizationStarted(authorizeUrl, state);
    }

    public async Task<GitHubAuthorization?> TakeOnceAsync(string state, CancellationToken cancellation)
    {
        var stateHash = Hash(state);
        var started = await db.GitHubAuthorizations.AsNoTracking()
            .SingleOrDefaultAsync(a => a.StateHash == stateHash, cancellation);
        if (started is null)
        {
            return null;
        }

        var taken = await db.GitHubAuthorizations.Where(a => a.StateHash == stateHash).ExecuteDeleteAsync(cancellation);
        return taken == 1 && started.ExpiresAt > time.GetUtcNow() ? started : null;
    }

    private static string Hash(string state) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
}
