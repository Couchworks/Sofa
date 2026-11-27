using Microsoft.EntityFrameworkCore;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class GitHubReturns
{
    public const string AlreadyUsed =
        "GitHub came back to Sofa with a return that was already used, or one that has expired. Start again from Sofa.";

    public static async Task<bool> UseOnce(GitHubReturn started, ConnectionsDb db, TimeProvider time, CancellationToken cancellation)
    {
        var now = time.GetUtcNow();
        if (started.ExpiresAt <= now)
        {
            return false;
        }

        await db.UsedGitHubReturns.Where(used => used.ExpiresAt < now).ExecuteDeleteAsync(cancellation);
        var claimed = await db.Database.ExecuteSqlAsync(
            $"""
             insert into connections.used_github_returns (nonce, expires_at) values ({started.Nonce}, {started.ExpiresAt})
             on conflict (nonce) do nothing
             """,
            cancellation);
        return claimed == 1;
    }
}
