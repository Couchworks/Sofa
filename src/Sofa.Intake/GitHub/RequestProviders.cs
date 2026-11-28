namespace Sofa.Intake.GitHub;

public sealed record IssueRef(Guid ConnectionId, string Repository, long Number);

public sealed record CommentRef(IssueRef Issue, long CommentId);

public sealed record WantedIssueState(string? PhaseLabel, string? PreviousPhaseLabel, bool Closed);

public sealed record IssueNow(string Title, string Body, string State);

public interface IRequestSource
{
    Task<IssueNow?> ReadAsync(IssueRef issue, CancellationToken cancellation);
}

public interface IRequestReporter
{
    Task<CommentRef> PostAsync(IssueRef issue, string body, CancellationToken cancellation);

    Task<bool> UpdateAsync(CommentRef existing, string body, CancellationToken cancellation);
}

public interface IRequestState
{
    Task SetAsync(IssueRef issue, WantedIssueState state, CancellationToken cancellation);
}
