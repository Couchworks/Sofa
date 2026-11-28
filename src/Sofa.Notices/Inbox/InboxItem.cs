namespace Sofa.Notices.Inbox;

public enum InboxItemKind
{
    RequestAwaitingDelegation,
    Question,
    Approval,
    RunGoneWrong,
    PullRequestClosedUnmerged,
}

public enum InboxAudience
{
    TheWorkspacesAdmins,
    TheUser,
}

public sealed class InboxItem
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required InboxItemKind Kind { get; init; }

    public required InboxAudience For { get; init; }

    public Guid? UserId { get; init; }

    public required Guid SubjectId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ProjectKey { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }

    public required string Url { get; init; }

    public required DateTimeOffset RaisedAt { get; init; }

    public DateTimeOffset? SettledAt { get; set; }
}
