using System.Text.Json;

namespace Sofa.Code.GitHub;

public sealed record PullRequestChanged(
    string Action, string Repository, long Number, string Title, string Url, string HeadBranch, string BaseBranch,
    bool Open, bool Merged, DateTimeOffset At);

public sealed record Pushed(string Repository, string Ref, string Before, string After, bool Created, bool Deleted, string? By);

public static class GitHubPayloads
{
    public const string PullRequest = "pull_request";
    public const string Push = "push";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static PullRequestChanged? ReadPullRequest(string payload, DateTimeOffset receivedAt) =>
        Read<PullRequestPayload>(payload) is { Action: { } action, PullRequest: { } pull, Repository.FullName: { } repository }
            ? new PullRequestChanged(
                action,
                repository,
                pull.Number,
                pull.Title ?? "",
                pull.HtmlUrl ?? "",
                pull.Head?.Ref ?? "",
                pull.Base?.Ref ?? "",
                Open: pull.State == "open",
                Merged: pull.Merged,
                At: pull.MergedAt ?? pull.ClosedAt ?? pull.UpdatedAt ?? pull.CreatedAt ?? receivedAt)
            : null;

    public static Pushed? ReadPush(string payload) =>
        Read<PushPayload>(payload) is { Ref: { } gitRef, Before: { } before, After: { } after, Repository.FullName: { } repository } push
            ? new Pushed(repository, gitRef, before, after, push.Created, push.Deleted, push.Sender?.Login)
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

    private sealed record PullRequestPayload(string? Action, PullInPayload? PullRequest, RepositoryInPayload? Repository);

    private sealed record PullInPayload(
        long Number, string? Title, string? HtmlUrl, string? State, bool Merged, RefInPayload? Head, RefInPayload? Base,
        DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset? ClosedAt, DateTimeOffset? MergedAt);

    private sealed record RefInPayload(string? Ref);

    private sealed record PushPayload(
        string? Ref, string? Before, string? After, bool Created, bool Deleted, RepositoryInPayload? Repository, SenderInPayload? Sender);

    private sealed record RepositoryInPayload(string? FullName);

    private sealed record SenderInPayload(string? Login);
}
