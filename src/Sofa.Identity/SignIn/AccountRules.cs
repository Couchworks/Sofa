namespace Sofa.Identity.SignIn;

public static class AccountRules
{
    public const int ShortestPassword = 10;
    public const int LongestName = 200;
    public const int LongestEmail = 320;

    public static Dictionary<string, string[]> ProblemsWithSetup(
        string? name, string? email, string? password, string? workspaceName, string? teamName)
    {
        var problems = new Dictionary<string, string[]>();
        RequireName(problems, "name", name, "Your name");
        RequireName(problems, "workspaceName", workspaceName, "The Workspace's name");
        RequireName(problems, "teamName", teamName, "The Team's name");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Trim().Length > LongestEmail)
        {
            problems["email"] = ["Enter an email address."];
        }

        if (password is null || password.Length < ShortestPassword)
        {
            problems["password"] = [$"Choose a password of at least {ShortestPassword} characters."];
        }

        return problems;
    }

    private static void RequireName(Dictionary<string, string[]> problems, string field, string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems[field] = [$"{what} is needed."];
        }
        else if (value.Trim().Length > LongestName)
        {
            problems[field] = [$"{what} can be at most {LongestName} characters."];
        }
    }
}
