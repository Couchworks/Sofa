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
    NoSuchPersonOnGitHub,
    GitHubDidNotAnswer,
    AlreadyInTheWorkspace,
}

public sealed class GitHubAccountLinks(IdentityDb db, ConnectionsGitHub connections, TimeProvider time)
{
    public async Task<User?> SignInAsAsync(GitHubUserAccount account, CancellationToken cancellation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var match = GitHubLinkRules.UserSigningInAs(account, await CandidatesForAsync(account, cancellation));
        if (match is null)
        {
            return null;
        }

        if (match.AddedAndNeverSignedInToMergeIn is { } added)
        {
            await MergeAsync(added, match.User, cancellation);
        }

        LinkAndRefresh(match.User, account, await EmailIsFreeAsync(account, cancellation));
        match.User.SignedIn(time.GetUtcNow());
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

        if (match.AddedAndNeverSignedInToMergeIn is { } added)
        {
            await MergeAsync(added, linking, cancellation);
        }

        LinkAndRefresh(linking, account, await EmailIsFreeAsync(account, cancellation));
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

        var (account, gitHubAnswered) = await connections.AccountOfLoginAsync(login, cancellation);
        if (account is null)
        {
            return (null, gitHubAnswered ? AddByLoginRefusal.NoSuchPersonOnGitHub : AddByLoginRefusal.GitHubDidNotAnswer);
        }

        var now = time.GetUtcNow();
        var user = await db.Users.SingleOrDefaultAsync(u => u.GitHubAccountId == account.Id, cancellation);
        if (user is null)
        {
            user = new User { Name = account.Login, GitHubAccountId = account.Id, GitHubLogin = account.Login, CreatedAt = now };
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
        var email = account.VerifiedPrimaryEmail is { } verified ? User.Normalize(verified) : null;
        return await db.Users
            .Where(user => user.GitHubAccountId == account.Id || (email != null && user.NormalizedEmail == email))
            .ToListAsync(cancellation);
    }

    private async Task<bool> EmailIsFreeAsync(GitHubUserAccount account, CancellationToken cancellation)
    {
        if (account.VerifiedPrimaryEmail is not { } email)
        {
            return false;
        }

        var normalized = User.Normalize(email);
        return !await db.Users.AnyAsync(other => other.NormalizedEmail == normalized, cancellation);
    }

    private static void LinkAndRefresh(User user, GitHubUserAccount account, bool emailIsFree)
    {
        if (user.IsAddedAndNeverSignedIn && !string.IsNullOrWhiteSpace(account.Name))
        {
            user.Name = account.Name.Trim();
        }

        if (user.Email is null && emailIsFree)
        {
            user.Email = account.VerifiedPrimaryEmail!.Trim();
            user.NormalizedEmail = User.Normalize(user.Email);
        }

        user.GitHubAccountId = account.Id;
        user.GitHubLogin = account.Login;
    }

    private async Task MergeAsync(User added, User into, CancellationToken cancellation)
    {
        var addedTo = await db.WorkspaceUsers.Where(w => w.UserId == added.Id).ToListAsync(cancellation);
        var alreadyIn = await db.WorkspaceUsers.Where(w => w.UserId == into.Id).Select(w => w.WorkspaceId).ToListAsync(cancellation);
        var now = time.GetUtcNow();

        db.TeamUsers.RemoveRange(await db.TeamUsers.Where(t => t.UserId == added.Id).ToListAsync(cancellation));
        db.WorkspaceUsers.RemoveRange(addedTo);
        foreach (var membership in addedTo.Where(m => !alreadyIn.Contains(m.WorkspaceId)))
        {
            db.WorkspaceUsers.Add(new WorkspaceUser
            {
                WorkspaceId = membership.WorkspaceId, UserId = into.Id, IsAdmin = membership.IsAdmin, JoinedAt = now,
            });
        }

        db.Users.Remove(added);
        await db.SaveChangesAsync(cancellation);
    }
}
