using System.Text.Json.Serialization;
using Sofa.Contracts.Code;

namespace Sofa.Contracts.Flow;

public sealed record RequestRef(string Repository, long Number, string Url);

public sealed record OpenWorkItem(
    Guid RequestId, Guid WorkspaceId, Guid ProjectId, string Title, RequestRef Request, Guid? DelegatingUserId);

public sealed record WorkItemSummary(
    Guid Id,
    string Key,
    string Title,
    Guid RequestId,
    RequestRef Request,
    Guid? DelegatingUserId,
    DateTimeOffset OpenedAt,
    WorkItemState State,
    PhaseName? Phase,
    Track? Track);

public sealed record WorkItemEnteredPhase(Guid WorkItemId, Guid WorkspaceId, Guid RequestId, string Key, PhaseName Phase);

public sealed record WorkItemReachedMainBranch(Guid WorkItemId, Guid WorkspaceId, Guid RequestId, string Key);

public sealed record WorkItemDetails(
    Guid Id,
    string Key,
    string Title,
    Guid ProjectId,
    Guid RequestId,
    RequestRef Request,
    Guid? DelegatingUserId,
    DateTimeOffset OpenedAt,
    IReadOnlyList<QuestionDetails> Questions,
    WorkItemState State,
    PhaseName? Phase,
    Track? Track,
    WaitDetails? Waiting,
    IReadOnlyList<WorkItemRunDetails> Runs,
    IReadOnlyList<PhaseEndDetails> PhaseEnds,
    WorkItemPullRequest? PullRequest,
    DateTimeOffset? DoneAt);

public sealed record QuestionDetails(
    Guid Id,
    Guid RunId,
    PhaseName Phase,
    string Text,
    DateTimeOffset AskedAt,
    string? Answer,
    Guid? AnsweredByUserId,
    DateTimeOffset? AnsweredAt);

public sealed record WaitDetails(Guid WaitId, WaitKind Kind, string Why, DateTimeOffset Since);

public sealed record WorkItemRunDetails(Guid RunId, PhaseName Phase, DateTimeOffset StartedAt, Guid? RetryOf, string? EndedAs, DateTimeOffset? EndedAt);

public sealed record PhaseEndDetails(
    Guid RunId,
    PhaseName Phase,
    string Outcome,
    Gate Gate,
    PhaseEndStanding Standing,
    DateTimeOffset EndedAt,
    Guid? DecidedByUserId,
    string? Comment,
    DateTimeOffset? DecidedAt);

public sealed record WorkItemPullRequest(string Repository, long Number, string Url, PullRequestState State);

public sealed record ApprovePhase(Guid WorkspaceId, string ProjectKey, string WorkItemKey, PhaseName Phase, Guid ByUserId);

public sealed record SendPhaseBack(Guid WorkspaceId, string ProjectKey, string WorkItemKey, PhaseName Phase, Guid ByUserId, string Comment);

public sealed record AnswerQuestion(
    Guid WorkspaceId, string ProjectKey, string WorkItemKey, Guid QuestionId, Guid ByUserId, string Answer);

public sealed record WorkItemAwaitsUser(
    Guid WaitId,
    Guid WorkspaceId,
    Guid ProjectId,
    string ProjectKey,
    Guid WorkItemId,
    string WorkItemKey,
    string Title,
    Guid UserId,
    WaitKind Kind,
    string Why,
    string Url,
    DateTimeOffset Since);

public sealed record WorkItemNoLongerAwaitsUser(Guid WaitId, Guid WorkspaceId, Guid WorkItemId, DateTimeOffset At);

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemState>))]
public enum WorkItemState
{
    AwaitsDelegation,
    Running,
    AwaitsAnswer,
    AwaitsApproval,
    AwaitsPullRequest,
    AwaitsUser,
    Done,
}

[JsonConverter(typeof(JsonStringEnumConverter<Track>))]
public enum Track
{
    Small,
    Full,
}

[JsonConverter(typeof(JsonStringEnumConverter<WaitKind>))]
public enum WaitKind
{
    Question,
    Approval,
    RunGoneWrong,
    PullRequestClosedUnmerged,
}

[JsonConverter(typeof(JsonStringEnumConverter<PhaseEndStanding>))]
public enum PhaseEndStanding
{
    WentOn,
    AwaitsApproval,
    Approved,
    SentBack,
    Unverified,
    Verified,
}
