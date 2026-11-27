using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Intake;

public sealed record PostReport(Guid RequestId, string WorkItemKey, PhaseName Phase, string Summary, string Detail);

public sealed record AskOnRequest(Guid QuestionId, Guid RequestId, string WorkItemKey, PhaseName Phase, string Question);

public sealed record RequestAwaitingDelegation(
    Guid RequestId,
    Guid WorkspaceId,
    Guid ProjectId,
    string ProjectKey,
    string Title,
    RequestRef Request,
    string MarkedByGitHubLogin,
    DateTimeOffset MarkedAt);

public sealed record QuestionAnsweredOnRequest(
    Guid QuestionId, Guid RequestId, Guid WorkspaceId, Guid AnsweredByUserId, string Answer, DateTimeOffset AnsweredAt);
