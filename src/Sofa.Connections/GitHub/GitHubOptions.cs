namespace Sofa.Connections.GitHub;

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    public Uri ApiAddress { get; set; } = new("https://api.github.com/");

    public Uri WebAddress { get; set; } = new("https://github.com/");

    public TimeSpan DeliverySweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan DeliveriesKeptByGitHub { get; set; } = TimeSpan.FromDays(3);
}
