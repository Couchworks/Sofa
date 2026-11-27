using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Connections;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;
using Sofa.Identity.Users;
using Sofa.Identity.Workspaces;

namespace Sofa.Identity.GitHub;

public enum AddByLoginRefusal
{
    NotAGitHubLogin,
    NoSuchWorkspace,
    AlreadyInTheWorkspace,
}

public sealed class GitHubAccountLinks(IdentityDb db, TimeProvider time)
{
    public async Task<User?> SignInAsAsync(GitHubUserAccount account, CancellationToken cancellation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var match = GitHubLinkRules.UserSigningInAs(account, await CandidatesForAsync(account, cancellation));
        if (match is null)
        {
            return null;
        }

        if (match.AddedByLoginToMergeIn is { } addedByLogin)
        {
            await MergeAsync(addedByLogin, match.User, cancellation);
        }

        await LinkAsync(match.User, account, cancellation);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return match.User;
    }

    public async Task<LinkRefusal?> LinkAsync(Guid userId, GitHubUserAccount account, CancellationToken cancellation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var linking = await db.Users.SingleAsync(user => user.Id == userId, cancellation);
        var match = GitHubLinkRules.Linking(linking, account, await CandidatesForAsync(account, cancellation));
        if (match.Refusal is { } refusal)
        {
            return refusal;
        }

        if (match.AddedByLoginToMergeIn is { } addedByLogin)
        {
            await MergeAsync(addedByLogin, linking, cancellation);
        }

        await LinkAsync(linking, account, cancellation);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return null;
    }

    public async Task<(UserAdded? Added, AddByLoginRefusal? Refusal)> AddByLoginAsync(
        Guid workspaceId, string login, CancellationToken cancellation)
    {
        login = login.Trim().TrimStart('@');
        if (!GitHubLogins.IsLogin(login))
        {
            return (null, AddByLoginRefusal.NotAGitHubLogin);
        }

        if (!await db.Workspaces.AnyAsync(workspace => workspace.Id == workspaceId, cancellation))
        {
            return (null, AddByLoginRefusal.NoSuchWorkspace);
        }

        var normalized = User.NormalizeGitHubLogin(login);
        var user = await db.Users
            .Where(u => u.NormalizedGitHubLogin == normalized)
            .OrderByDescending(u => u.GitHubAccountId != null)
            .ThenBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(cancellation);
        var now = time.GetUtcNow();
        if (user is null)
        {
            user = new User { Name = login, GitHubLogin = login, NormalizedGitHubLogin = normalized, CreatedAt = now };
            db.Users.Add(user);
        }
        else if (await db.WorkspaceUsers.AnyAsync(w => w.WorkspaceId == workspaceId && w.UserId == user.Id, cancellation))
        {
            return (null, AddByLoginRefusal.AlreadyInTheWorkspace);
        }

        db.WorkspaceUsers.Add(new WorkspaceUser { WorkspaceId = workspaceId, UserId = user.Id, IsAdmin = false, JoinedAt = now });
        await db.SaveChangesAsync(cancellation);
        return (new UserAdded(user.Id, user.GitHubLogin!), null);
    }

    private async Task<List<User>> CandidatesForAsync(GitHubUserAccount account, CancellationToken cancellation)
    {
        var login = User.NormalizeGitHubLogin(account.Login);
        var email = account.VerifiedPrimaryEmail is { } verified ? User.Normalize(verified) : null;
        return await db.Users
            .Where(user => user.GitHubAccountId == account.Id
                || user.NormalizedGitHubLogin == login
                || (email != null && user.NormalizedEmail == email))
            .ToListAsync(cancellation);
    }

    private async Task LinkAsync(User user, GitHubUserAccount account, CancellationToken cancellation)
    {
        if (user.IsOnlyAddedByGitHubLogin && !string.IsNullOrWhiteSpace(account.Name))
        {
            user.Name = account.Name.Trim();
        }

        if (user.Email is null && account.VerifiedPrimaryEmail is { } email)
        {
            var normalized = User.Normalize(email);
            if (!await db.Users.AnyAsync(other => other.NormalizedEmail == normalized, cancellation))
            {
                user.Email = email.Trim();
                user.NormalizedEmail = normalized;
            }
        }

        user.GitHubAccountId = account.Id;
        user.GitHubLogin = account.Login;
        user.NormalizedGitHubLogin = User.NormalizeGitHubLogin(account.Login);
    }

    private async Task MergeAsync(User addedByLogin, User into, CancellationToken cancellation)
    {
        var addedTo = await db.WorkspaceUsers.Where(w => w.UserId == addedByLogin.Id).ToListAsync(cancellation);
        var alreadyIn = await db.WorkspaceUsers.Where(w => w.UserId == into.Id).Select(w => w.WorkspaceId).ToListAsync(cancellation);
        var now = time.GetUtcNow();

        db.TeamUsers.RemoveRange(await db.TeamUsers.Where(t => t.UserId == addedByLogin.Id).ToListAsync(cancellation));
        db.WorkspaceUsers.RemoveRange(addedTo);
        foreach (var membership in addedTo.Where(m => !alreadyIn.Contains(m.WorkspaceId)))
        {
            db.WorkspaceUsers.Add(new WorkspaceUser
            {
                WorkspaceId = membership.WorkspaceId, UserId = into.Id, IsAdmin = membership.IsAdmin, JoinedAt = now,
            });
        }

        db.Users.Remove(addedByLogin);
        await db.SaveChangesAsync(cancellation);
    }
}
