using System.Text.RegularExpressions;

namespace Sofa.Identity.GitHub;

public static partial class GitHubLogins
{
    public const int Longest = 39;

    public static bool IsLogin(string login) => Login().IsMatch(login);

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")]
    private static partial Regex Login();
}
