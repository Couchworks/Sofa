using System.Text.Json.Serialization;
using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Execution;

public sealed record StartRun(
    Guid RunId,
    Guid WorkspaceId,
    Guid ProjectId,
    Guid WorkItemId,
    PhaseName Phase,
    Guid AgentId,
    Vendor Vendor,
    string Model,
    Sandbox Sandbox,
    Guid DelegatingUserId,
    Priority Priority,
    string Brief,
    int RetryLimit);

public sealed record FinishRun(Guid RunId, Guid AgentId, string Outcome, string Contribution, IReadOnlyList<string> AttachmentPaths);

public sealed record EndRunWithQuestion(Guid RunId, Guid AgentId, Guid QuestionId, string Question);

public sealed record AskedQuestion(Guid QuestionId, string Text);

public sealed record StopRun(Guid WorkspaceId, Guid RunId, Guid ByUserId);

public sealed record RetryRun(Guid WorkspaceId, Guid RunId, Guid ByUserId);

public sealed record RunAnswer(Guid? RunId, RunRefusal? Refusal, string? Problem)
{
    public static RunAnswer Done(Guid runId) => new(runId, null, null);

    public static RunAnswer Refused(RunRefusal refusal, string problem) => new(null, refusal, problem);
}

[JsonConverter(typeof(JsonStringEnumConverter<RunRefusal>))]
public enum RunRefusal
{
    Invalid,
    NotFound,
    NotNow,
}

[JsonConverter(typeof(JsonStringEnumConverter<RunState>))]
public enum RunState
{
    Waiting,
    Running,
    Finished,
    Failed,
    Lost,
    Stopped,
    Refused,
}

[JsonConverter(typeof(JsonStringEnumConverter<RefusedAs>))]
public enum RefusedAs
{
    SignInAgain,
    AgentNotRegistered,
    ApiKeyUnavailable,
}

public sealed record RunDetails(
    Guid Id,
    Guid WorkspaceId,
    Guid ProjectId,
    Guid WorkItemId,
    PhaseName Phase,
    Guid AgentId,
    string Model,
    Sandbox Sandbox,
    Guid DelegatingUserId,
    Priority Priority,
    RunState State,
    string? Why,
    RefusedAs? RefusedAs,
    Guid? RunnerId,
    int Attempt,
    int RetriesLeft,
    Guid? RetryOf,
    Guid? RetriedBy,
    bool WaitsForRetry,
    string? Outcome,
    AskedQuestion? Question,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt);

public sealed record RunEnded(
    Guid RunId,
    Guid WorkspaceId,
    Guid WorkItemId,
    PhaseName Phase,
    RunState State,
    string? Why,
    RefusedAs? RefusedAs,
    Guid? RetriedBy,
    string? Outcome,
    AskedQuestion? Question);

public sealed record SubscriptionSignedOut(Guid WorkspaceId, Guid UserId, Guid RunnerId, string RunnerName, Guid RunId);

public static class RunLimits
{
    public const int DefaultRetryLimit = 2;
    public const int MostRetries = 10;
    public const int LongestBrief = 100_000;
    public const int LongestContribution = 200_000;
    public const int LongestQuestion = 4_000;
    public const int MostAttachments = 10;
    public const long LargestAttachment = 10 * 1024 * 1024;
}
