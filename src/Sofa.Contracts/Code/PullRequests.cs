using System.Text.Json.Serialization;

namespace Sofa.Contracts.Code;

public sealed record PullRequestOfWorkItem(
    Guid WorkspaceId,
    Guid ProjectId,
    string ProjectKey,
    string WorkItemKey,
    string Repository,
    long Number,
    string Title,
    string Url,
    string HeadBranch,
    string BaseBranch,
    PullRequestState State,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

[JsonConverter(typeof(JsonStringEnumConverter<PullRequestState>))]
public enum PullRequestState
{
    Open,
    Merged,
    ClosedUnmerged,
}

public interface IPullRequestEvent
{
    PullRequestOfWorkItem PullRequest { get; }
}

public sealed record PullRequestOpened(PullRequestOfWorkItem PullRequest) : IPullRequestEvent;

public sealed record PullRequestMerged(PullRequestOfWorkItem PullRequest) : IPullRequestEvent;

public sealed record PullRequestClosedUnmerged(PullRequestOfWorkItem PullRequest) : IPullRequestEvent;
