using Sofa.Contracts.Connections;
using Sofa.Identity.Users;

namespace Sofa.Identity.GitHub;

public sealed record SignInMatch(User User, User? AddedByLoginToMergeIn);

public enum LinkRefusal
{
    UserHasAnotherGitHubAccount,
    GitHubAccountHasAnotherUser,
}

public sealed record LinkMatch(User? AddedByLoginToMergeIn, LinkRefusal? Refusal);

public static class GitHubLinkRules
{
    public static SignInMatch? UserSigningInAs(GitHubUserAccount account, IReadOnlyCollection<User> candidates)
    {
        if (candidates.FirstOrDefault(user => user.GitHubAccountId == account.Id) is { } linked)
        {
            return new SignInMatch(linked, null);
        }

        var addedByLogin = AddedByLogin(account, candidates);
        var byEmail = WithTheVerifiedEmail(account, candidates);

        if (addedByLogin is { IsOnlyAddedByGitHubLogin: true } && byEmail is not null && byEmail != addedByLogin)
        {
            return new SignInMatch(byEmail, addedByLogin);
        }

        return addedByLogin is not null ? new SignInMatch(addedByLogin, null)
            : byEmail is not null ? new SignInMatch(byEmail, null)
            : null;
    }

    public static LinkMatch Linking(User linking, GitHubUserAccount account, IReadOnlyCollection<User> candidates)
    {
        if (linking.GitHubAccountId == account.Id)
        {
            return new LinkMatch(null, null);
        }

        if (linking.GitHubAccountId is not null)
        {
            return new LinkMatch(null, LinkRefusal.UserHasAnotherGitHubAccount);
        }

        if (candidates.Any(user => user.Id != linking.Id && user.GitHubAccountId == account.Id))
        {
            return new LinkMatch(null, LinkRefusal.GitHubAccountHasAnotherUser);
        }

        var addedByLogin = AddedByLogin(account, candidates.Where(user => user.Id != linking.Id).ToList());
        return addedByLogin switch
        {
            null => new LinkMatch(null, null),
            { IsOnlyAddedByGitHubLogin: true } => new LinkMatch(addedByLogin, null),
            _ => new LinkMatch(null, LinkRefusal.GitHubAccountHasAnotherUser),
        };
    }

    private static User? AddedByLogin(GitHubUserAccount account, IReadOnlyCollection<User> candidates)
    {
        var login = User.NormalizeGitHubLogin(account.Login);
        return candidates
            .Where(user => user.GitHubAccountId is null && user.NormalizedGitHubLogin == login)
            .OrderBy(user => user.CreatedAt)
            .FirstOrDefault();
    }

    private static User? WithTheVerifiedEmail(GitHubUserAccount account, IReadOnlyCollection<User> candidates)
    {
        if (account.VerifiedPrimaryEmail is not { } email)
        {
            return null;
        }

        var normalized = User.Normalize(email);
        return candidates.FirstOrDefault(user => user.GitHubAccountId is null && user.NormalizedEmail == normalized);
    }
}
