namespace Sofa.Code.GitHub;

public sealed class GitHubApiOptions
{
    public const string Section = "GitHub";

    public Uri ApiAddress { get; set; } = new("https://api.github.com/");
}
