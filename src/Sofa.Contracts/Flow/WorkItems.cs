namespace Sofa.Contracts.Flow;

public sealed record RequestRef(string Repository, long Number, string Url);

public sealed record OpenWorkItem(
    Guid RequestId, Guid WorkspaceId, Guid ProjectId, string Title, RequestRef Request, Guid? DelegatingUserId);

public sealed record WorkItemSummary(
    Guid Id, string Key, string Title, Guid RequestId, RequestRef Request, Guid? DelegatingUserId, DateTimeOffset OpenedAt);

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
    IReadOnlyList<QuestionDetails> Questions);

public sealed record QuestionDetails(
    Guid Id,
    Guid RunId,
    PhaseName Phase,
    string Text,
    DateTimeOffset AskedAt,
    string? Answer,
    Guid? AnsweredByUserId,
    DateTimeOffset? AnsweredAt);
