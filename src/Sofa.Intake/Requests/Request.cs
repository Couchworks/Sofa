using Sofa.Contracts.Flow;
using Sofa.Intake.GitHub;

namespace Sofa.Intake.Requests;

public sealed class Request
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ProjectKey { get; init; }

    public required Guid ConnectionId { get; init; }

    public required string Repository { get; init; }

    public required long Number { get; init; }

    public required string Title { get; init; }

    public required string Url { get; init; }

    public required long MarkedByGitHubAccountId { get; init; }

    public required string MarkedByGitHubLogin { get; init; }

    public Guid? DelegatingUserId { get; init; }

    public required DateTimeOffset MarkedAt { get; init; }

    public string? PhaseLabel { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public IssueRef Issue => new(ConnectionId, Repository, Number);

    public RequestRef Ref => new(Repository, Number, Url);
}
