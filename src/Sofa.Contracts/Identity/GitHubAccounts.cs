namespace Sofa.Contracts.Identity;

public static class GitHubSignInState
{
    public const string Cookie = "sofa_github_state";
    public const string CookiePath = "/account/github";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
}

public sealed record GitHubLinkStarted(string AuthorizeUrl, string State);

public sealed record AddUserByGitHubLogin(string GitHubLogin);

public sealed record UserAdded(Guid UserId, string GitHubLogin);

public sealed record GitHubAccountHolder(Guid UserId, string Name);
