namespace Sofa.Intake;

public sealed class GitHubApiOptions
{
    public const string Section = "GitHub";

    public Uri ApiAddress { get; set; } = new("https://api.github.com/");
}

public sealed class PublicAddressOptions
{
    public const string Section = "Sofa";

    public Uri? PublicAddress { get; set; }
}

public sealed record SofaAddress(Uri Address)
{
    public Uri OfWorkItem(string projectKey, string workItemKey) =>
        new(Address, $"projects/{Uri.EscapeDataString(projectKey)}/work-items/{Uri.EscapeDataString(workItemKey)}");
}
