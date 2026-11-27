using System.Text.Json;

namespace Sofa.Intake.GitHub;

public sealed record GitHubAccountInPayload(long Id, string Login, string? Type)
{
    public bool IsBot => Type == "Bot";
}

public sealed record IssueLabeled(string Repository, long Number, string Title, string Url, string Label, GitHubAccountInPayload By);

public sealed record IssueCommented(string Repository, long Number, long CommentId, string Body, GitHubAccountInPayload By);

public static class GitHubPayloads
{
    public const string Labeled = "issues.labeled";
    public const string Commented = "issue_comment.created";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IssueLabeled? ReadLabeled(string payload) =>
        Read<LabeledPayload>(payload) is { Issue: { } issue, Label.Name: { } label, Repository.FullName: { } repository, Sender: { } sender }
            && issue.PullRequest is null
            ? new IssueLabeled(repository, issue.Number, issue.Title ?? "", issue.HtmlUrl ?? "", label, sender.AsAccount())
            : null;

    public static IssueCommented? ReadCommented(string payload) =>
        Read<CommentedPayload>(payload) is { Issue: { } issue, Comment: { User: { } author } comment, Repository.FullName: { } repository }
            && issue.PullRequest is null
            ? new IssueCommented(repository, issue.Number, comment.Id, comment.Body ?? "", author.AsAccount())
            : null;

    private static T? Read<T>(string payload) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record LabeledPayload(IssueInPayload? Issue, LabelInPayload? Label, RepositoryInPayload? Repository, UserInPayload? Sender);

    private sealed record CommentedPayload(IssueInPayload? Issue, CommentInPayload? Comment, RepositoryInPayload? Repository);

    private sealed record IssueInPayload(long Number, string? Title, string? HtmlUrl, JsonElement? PullRequest);

    private sealed record LabelInPayload(string? Name);

    private sealed record RepositoryInPayload(string? FullName);

    private sealed record CommentInPayload(long Id, string? Body, UserInPayload? User);

    private sealed record UserInPayload(long Id, string? Login, string? Type)
    {
        public GitHubAccountInPayload AsAccount() => new(Id, Login ?? "", Type);
    }
}
