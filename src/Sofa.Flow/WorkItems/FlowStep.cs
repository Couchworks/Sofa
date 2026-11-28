using Sofa.Contracts.Flow;

namespace Sofa.Flow.WorkItems;

public abstract record FlowStep
{
    public sealed record EnterPhase(PhaseName Phase) : FlowStep;

    public sealed record StartRunOf(PhaseName Phase, Guid RunId) : FlowStep;

    public sealed record ReportPhase(PhaseName Phase, Guid RunId) : FlowStep;

    public sealed record ReportMerged(Guid ImplementedByRunId, WorkItemPullRequest PullRequest) : FlowStep;

    public sealed record AwaitUser(Guid WaitId, WaitKind Kind, string Why, string Url) : FlowStep;

    public sealed record StopAwaiting(Guid WaitId) : FlowStep;

    public sealed record ReachMainBranch : FlowStep;
}

public sealed record UsersDecision(string? Refusal, IReadOnlyList<FlowStep> Steps)
{
    public static UsersDecision Refused(string why) => new(why, []);

    public static UsersDecision Taken(IReadOnlyList<FlowStep> steps) => new(null, steps);
}
