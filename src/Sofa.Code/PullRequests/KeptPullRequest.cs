using Sofa.Contracts.Code;

namespace Sofa.Code.PullRequests;

public sealed class KeptPullRequest
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ProjectKey { get; init; }

    public required Guid ConnectionId { get; init; }

    public required string Repository { get; init; }

    public required long Number { get; init; }

    public required string WorkItemKey { get; set; }

    public required string Title { get; set; }

    public required string Url { get; set; }

    public required string HeadBranch { get; set; }

    public required string BaseBranch { get; set; }

    public PullRequestState State { get; set; }

    public required DateTimeOffset OpenedAt { get; init; }

    public DateTimeOffset? ClosedAt { get; set; }

    public PullRequestOfWorkItem AsRead => new(
        WorkspaceId, ProjectId, ProjectKey, WorkItemKey, Repository, Number, Title, Url, HeadBranch, BaseBranch, State, OpenedAt, ClosedAt);
}
