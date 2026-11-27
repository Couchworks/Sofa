using Sofa.Contracts.Connections;
using Sofa.Identity.Users;

namespace Sofa.Identity.GitHub;

public sealed record SignInMatch(User User, User? AddedAndNeverSignedInToMergeIn);

public enum LinkRefusal
{
    UserHasAnotherGitHubAccount,
    GitHubAccountHasAnotherUser,
}

public sealed record LinkMatch(User? AddedAndNeverSignedInToMergeIn, LinkRefusal? Refusal);

public static class GitHubLinkRules
{
    public static SignInMatch? UserSigningInAs(GitHubUserAccount account, IReadOnlyCollection<User> candidates)
    {
        var holder = candidates.FirstOrDefault(user => user.GitHubAccountId == account.Id);
        var byEmail = WithTheVerifiedEmail(account, candidates);

        if (holder is { IsAddedAndNeverSignedIn: true } && byEmail is not null)
        {
            return new SignInMatch(byEmail, holder);
        }

        return holder is not null ? new SignInMatch(holder, null)
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

        return candidates.FirstOrDefault(user => user.Id != linking.Id && user.GitHubAccountId == account.Id) switch
        {
            null => new LinkMatch(null, null),
            { IsAddedAndNeverSignedIn: true } added => new LinkMatch(added, null),
            _ => new LinkMatch(null, LinkRefusal.GitHubAccountHasAnotherUser),
        };
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
