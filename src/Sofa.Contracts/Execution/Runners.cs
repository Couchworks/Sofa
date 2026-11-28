using System.Text.Json.Serialization;
using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Execution;

public sealed record RunnerOwner(RunnerOwnerKind Kind, Guid Id)
{
    public static RunnerOwner User(Guid userId) => new(RunnerOwnerKind.User, userId);

    public static RunnerOwner Team(Guid teamId) => new(RunnerOwnerKind.Team, teamId);
}

[JsonConverter(typeof(JsonStringEnumConverter<RunnerOwnerKind>))]
public enum RunnerOwnerKind
{
    User,
    Team,
}

[JsonConverter(typeof(JsonStringEnumConverter<RunnerKind>))]
public enum RunnerKind
{
    Subscription,
    ApiKey,
}

public sealed record CreateRunner(
    Guid RunnerId,
    Guid WorkspaceId,
    string Name,
    RunnerKind Kind,
    Vendor Vendor,
    RunnerOwner Owner,
    int RunsAtOnce,
    Guid? ApiKeyId);

public sealed record LimitRunsAtOnce(Guid WorkspaceId, Guid RunnerId, int RunsAtOnce, Guid ByUserId, bool ByAnAdmin);

public sealed record RunnerAnswer(Guid? RunnerId, RunnerRefusal? Refusal, string? Problem)
{
    public static RunnerAnswer Done(Guid runnerId) => new(runnerId, null, null);

    public static RunnerAnswer Refused(RunnerRefusal refusal, string problem) => new(null, refusal, problem);
}

[JsonConverter(typeof(JsonStringEnumConverter<RunnerRefusal>))]
public enum RunnerRefusal
{
    Invalid,
    NotFound,
    NotTheirs,
}

public sealed record RunnerDetails(
    Guid Id,
    string Name,
    RunnerKind Kind,
    Vendor Vendor,
    RunnerOwner Owner,
    int RunsAtOnce,
    bool HasApiKey,
    string? WorkerVolume,
    SubscriptionSignIn? SignIn);

public sealed record SubscriptionSignIn(bool SignedIn, DateTimeOffset? EndsAt, DateTimeOffset? CheckedAt);

public sealed record RunnerToSignIn(Guid Id, Guid WorkspaceId, string Name, RunnerKind Kind, string? WorkerVolume);

public sealed record SignInSeen(bool SignedIn, DateTimeOffset? EndsAt);

public sealed record SubscriptionSignInSeen(
    Guid WorkspaceId, Guid UserId, Guid RunnerId, string RunnerName, bool SignedIn, DateTimeOffset? EndsAt, DateTimeOffset At);

public static class RunnerLimits
{
    public const int DefaultRunsAtOnce = 2;
    public const int MostRunsAtOnce = 20;
    public const int LongestName = 100;
}
