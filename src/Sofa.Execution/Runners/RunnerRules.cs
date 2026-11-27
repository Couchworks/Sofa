using Sofa.Contracts.Execution;

namespace Sofa.Execution.Runners;

public static class RunnerRules
{
    public const string WorkerVolumePrefix = "sofa-worker-";

    public static string WorkerVolumeOf(Guid runnerId) => $"{WorkerVolumePrefix}{runnerId}";

    public static string? ProblemWith(CreateRunner command) =>
        NameProblem(command.Name)
        ?? RunsAtOnceProblem(command.RunsAtOnce)
        ?? command.Kind switch
        {
            RunnerKind.Subscription when command.Owner.Kind != RunnerOwnerKind.User =>
                "A subscription is only ever its own User's, so a subscription Runner is personal.",
            RunnerKind.Subscription when command.ApiKeyId is not null =>
                "A subscription Runner signs in on its Worker and holds no API key.",
            RunnerKind.ApiKey when command.ApiKeyId is null =>
                "An API key Runner needs its API key.",
            _ => null,
        };

    public static string? NameProblem(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "A Runner needs a name."
        : name.Trim().Length > RunnerLimits.LongestName ? $"A Runner's name has at most {RunnerLimits.LongestName} characters."
        : null;

    public static string? RunsAtOnceProblem(int runsAtOnce) =>
        runsAtOnce is >= 1 and <= RunnerLimits.MostRunsAtOnce
            ? null
            : $"A Runner runs 1 to {RunnerLimits.MostRunsAtOnce} Runs at once.";

    public static string? WhyTheyMayNotChange(Runner runner, Guid userId, bool isAdmin) =>
        runner.Owner.Kind switch
        {
            RunnerOwnerKind.User when runner.OwnerUserId != userId => "Only its own User changes a personal Runner.",
            RunnerOwnerKind.Team when !isAdmin => "Only an Admin changes a team Runner.",
            _ => null,
        };

    public static bool IsSeenBy(Runner runner, Guid userId, IReadOnlyCollection<Guid> teamIds, bool isAdmin) =>
        runner.Owner.Kind == RunnerOwnerKind.User
            ? runner.OwnerUserId == userId
            : isAdmin || teamIds.Contains(runner.OwnerTeamId!.Value);
}
