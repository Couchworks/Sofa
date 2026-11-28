using Sofa.Contracts.Code;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using static Sofa.Flow.WorkItems.FlowStep;

namespace Sofa.Flow.WorkItems;

public sealed class WorkItem
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required int Number { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required Guid RequestId { get; init; }

    public required string RequestRepository { get; init; }

    public required long RequestNumber { get; init; }

    public required string RequestUrl { get; init; }

    public Guid? DelegatingUserId { get; set; }

    public required DateTimeOffset OpenedAt { get; init; }

    public WorkItemState State { get; private set; } = WorkItemState.AwaitsDelegation;

    public PhaseName? Phase { get; private set; }

    public Track? Track { get; private set; }

    public bool TrackChosenByAUser { get; private set; }

    public Guid? CurrentRunId { get; private set; }

    public Guid? WaitId { get; private set; }

    public WaitKind? WaitingFor { get; private set; }

    public string? WaitingBecause { get; private set; }

    public DateTimeOffset? WaitingSince { get; private set; }

    public string? PullRequestRepository { get; private set; }

    public long? PullRequestNumber { get; private set; }

    public string? PullRequestUrl { get; private set; }

    public PullRequestState? PullRequestState { get; private set; }

    public DateTimeOffset? DoneAt { get; private set; }

    public uint Version { get; private set; }

    public List<WorkItemRun> Runs { get; init; } = [];

    public List<PhaseEnd> PhaseEnds { get; init; } = [];

    public RequestRef Request => new(RequestRepository, RequestNumber, RequestUrl);

    public WaitDetails? Waiting =>
        WaitId is { } waitId ? new WaitDetails(waitId, WaitingFor!.Value, WaitingBecause!, WaitingSince!.Value) : null;

    public WorkItemPullRequest? PullRequest =>
        PullRequestUrl is { } url ? new WorkItemPullRequest(PullRequestRepository!, PullRequestNumber!.Value, url, PullRequestState!.Value) : null;

    public static string KeyOf(string projectKey, int number) => $"{projectKey}-{number}";

    public IReadOnlyList<FlowStep> Begin(FlowOfAProject flow, DateTimeOffset now) =>
        State == WorkItemState.AwaitsDelegation && Phase is null && DelegatingUserId is not null && flow.First is { } first
            ? Enter(first, now)
            : [];

    public IReadOnlyList<FlowStep> HearRunEnded(RunEnded ended, FlowOfAProject flow, DateTimeOffset now)
    {
        var steps = new List<FlowStep>();
        if (ended.RunId != CurrentRunId)
        {
            if (!IsAUsersRetryOfTheRunGoneWrong(ended))
            {
                return [];
            }

            steps.AddRange(StopAwaiting());
            State = WorkItemState.Running;
            CurrentRunId = ended.RunId;
            Runs.Add(new WorkItemRun { RunId = ended.RunId, WorkItemId = Id, Phase = ended.Phase, StartedAt = now, RetryOf = ended.RetryOf });
        }

        var phase = Phase!.Value;
        if (Runs.SingleOrDefault(r => r.RunId == ended.RunId) is { } run)
        {
            run.EndedAs = EndedAs(ended);
            run.EndedAt = now;
        }

        if (ended.State is RunState.Failed or RunState.Lost && ended.RetriedBy is { } retry)
        {
            CurrentRunId = retry;
            Runs.Add(new WorkItemRun { RunId = retry, WorkItemId = Id, Phase = phase, StartedAt = now, RetryOf = ended.RunId });
            return steps;
        }

        CurrentRunId = null;
        if (ended is { State: RunState.Finished, Question: { } question })
        {
            State = WorkItemState.AwaitsAnswer;
            steps.Add(Await(question.QuestionId, WaitKind.Question, $"Its {phase} Run asked: {question.Text}", RequestUrl, now));
            return steps;
        }

        if (ended.State == RunState.Finished && PhaseOutcomes.Named(phase, ended.Outcome) is { } outcome)
        {
            steps.AddRange(Finished(phase, outcome.Name, ended.RunId, flow, now));
            return steps;
        }

        State = WorkItemState.AwaitsUser;
        steps.Add(Await(ended.RunId, WaitKind.RunGoneWrong,
            $"Its {phase} Run ended {ended.State}{(ended.Why is { Length: > 0 } why ? $": {why}" : ".")}", RequestUrl, now));
        return steps;
    }

    public IReadOnlyList<FlowStep> HearRetriedByAUser(RunRetriedByAUser retried, DateTimeOffset now)
    {
        if (State != WorkItemState.AwaitsUser || WaitingFor != WaitKind.RunGoneWrong || WaitId != retried.RetryOf || retried.Phase != Phase)
        {
            return [];
        }

        var steps = StopAwaiting();
        State = WorkItemState.Running;
        CurrentRunId = retried.RunId;
        Runs.Add(new WorkItemRun { RunId = retried.RunId, WorkItemId = Id, Phase = retried.Phase, StartedAt = now, RetryOf = retried.RetryOf });
        return steps;
    }

    public UsersDecision ChangeTrack(Track track)
    {
        if (State == WorkItemState.Done)
        {
            return UsersDecision.Refused($"{Key} is done; its Track no longer changes anything.");
        }

        Track = track;
        TrackChosenByAUser = true;
        return UsersDecision.Taken([]);
    }

    public UsersDecision Approve(PhaseName phase, Guid byUserId, DateTimeOffset now, FlowOfAProject flow)
    {
        if (AwaitingApproval(phase) is { } end)
        {
            end.Decide(PhaseEndStanding.Approved, byUserId, null, now);
            return UsersDecision.Taken([.. StopAwaiting(), .. GoOnFrom(phase, flow, now)]);
        }

        if (PhaseEnds.LastOrDefault(e => e.Phase == phase) is { Standing: PhaseEndStanding.Unverified } unverified)
        {
            unverified.Decide(PhaseEndStanding.Verified, byUserId, null, now);
            return UsersDecision.Taken([]);
        }

        return UsersDecision.Refused($"{Key} has no {phase} waiting for a User's approval.");
    }

    public UsersDecision SendBack(PhaseName phase, Guid byUserId, string comment, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return UsersDecision.Refused("Sending a Phase back takes a comment saying what to change.");
        }

        if (AwaitingApproval(phase) is not { } end)
        {
            return UsersDecision.Refused($"{Key} has no {phase} waiting for a User's approval.");
        }

        end.Decide(PhaseEndStanding.SentBack, byUserId, comment.Trim(), now);
        return UsersDecision.Taken([.. StopAwaiting(), .. RunPhase(phase, now)]);
    }

    public IReadOnlyList<FlowStep> HearQuestionAnswered(Guid questionId, DateTimeOffset now) =>
        State == WorkItemState.AwaitsAnswer && WaitId == questionId && Phase is { } phase
            ? [.. StopAwaiting(), .. RunPhase(phase, now)]
            : [];

    public IReadOnlyList<FlowStep> HearPullRequest(WorkItemPullRequest pullRequest, DateTimeOffset now)
    {
        if (State == WorkItemState.Done)
        {
            return [];
        }

        PullRequestRepository = pullRequest.Repository;
        PullRequestNumber = pullRequest.Number;
        PullRequestUrl = pullRequest.Url;
        PullRequestState = pullRequest.State;
        var closedBefore = State == WorkItemState.AwaitsUser && WaitingFor == WaitKind.PullRequestClosedUnmerged;
        return pullRequest.State switch
        {
            Contracts.Code.PullRequestState.Open when closedBefore => [.. StopAwaiting(), .. AwaitThePullRequest()],
            Contracts.Code.PullRequestState.Merged when Phase == PhaseName.Implement && State != WorkItemState.Running =>
                Finish(pullRequest, now),
            Contracts.Code.PullRequestState.ClosedUnmerged when State == WorkItemState.AwaitsPullRequest => ClosedUnmerged(pullRequest, now),
            _ => [],
        };
    }

    private bool IsAUsersRetryOfTheRunGoneWrong(RunEnded ended) =>
        ended.RetryOf is { } retryOf
        && State == WorkItemState.AwaitsUser
        && WaitingFor == WaitKind.RunGoneWrong
        && WaitId == retryOf
        && ended.Phase == Phase;

    private PhaseEnd? AwaitingApproval(PhaseName phase) =>
        State == WorkItemState.AwaitsApproval && Phase == phase
            ? PhaseEnds.LastOrDefault(e => e.Phase == phase && e.Standing == PhaseEndStanding.AwaitsApproval)
            : null;

    private List<FlowStep> Finished(PhaseName phase, string outcome, Guid runId, FlowOfAProject flow, DateTimeOffset now)
    {
        if (phase == PhaseName.Triage && !TrackChosenByAUser)
        {
            Track = FlowOfAProject.TrackOf(outcome);
        }

        var gate = flow.GateOf(phase, Track);
        var end = new PhaseEnd
        {
            RunId = runId,
            WorkItemId = Id,
            Phase = phase,
            Outcome = outcome,
            Gate = gate,
            Standing = gate switch
            {
                Gate.Approve => PhaseEndStanding.AwaitsApproval,
                Gate.ReviewAfterwards => PhaseEndStanding.Unverified,
                _ => PhaseEndStanding.WentOn,
            },
            EndedAt = now,
        };
        PhaseEnds.Add(end);

        List<FlowStep> steps = FlowOfAProject.Reports(phase) ? [new ReportPhase(phase, runId)] : [];
        if (gate == Gate.Approve)
        {
            State = WorkItemState.AwaitsApproval;
            steps.Add(Await(runId, WaitKind.Approval, $"Its {phase} waits for a User's approval.", RequestUrl, now));
            return steps;
        }

        steps.AddRange(GoOnFrom(phase, flow, now));
        return steps;
    }

    private List<FlowStep> GoOnFrom(PhaseName phase, FlowOfAProject flow, DateTimeOffset now)
    {
        if (flow.After(phase, Track) is { } next)
        {
            return Enter(next, now);
        }

        return PullRequest switch
        {
            { State: Contracts.Code.PullRequestState.Merged } merged => Finish(merged, now),
            { State: Contracts.Code.PullRequestState.ClosedUnmerged } closed => ClosedUnmerged(closed, now),
            _ => AwaitThePullRequest(),
        };
    }

    private List<FlowStep> AwaitThePullRequest()
    {
        State = WorkItemState.AwaitsPullRequest;
        return [];
    }

    private List<FlowStep> ClosedUnmerged(WorkItemPullRequest pullRequest, DateTimeOffset now)
    {
        State = WorkItemState.AwaitsUser;
        return [Await(Guid.CreateVersion7(), WaitKind.PullRequestClosedUnmerged,
            $"Its pull request {pullRequest.Url} was closed without being merged.", pullRequest.Url, now)];
    }

    private List<FlowStep> Finish(WorkItemPullRequest pullRequest, DateTimeOffset now)
    {
        var steps = StopAwaiting();
        State = WorkItemState.Done;
        CurrentRunId = null;
        DoneAt = now;
        steps.Add(new ReachMainBranch());
        if (PhaseEnds.LastOrDefault(e => e.Phase == PhaseName.Implement) is { } implemented)
        {
            steps.Add(new ReportMerged(implemented.RunId, pullRequest));
        }

        return steps;
    }

    private List<FlowStep> Enter(PhaseName phase, DateTimeOffset now) => [new EnterPhase(phase), .. RunPhase(phase, now)];

    private List<FlowStep> RunPhase(PhaseName phase, DateTimeOffset now)
    {
        var runId = Guid.CreateVersion7();
        Phase = phase;
        State = WorkItemState.Running;
        CurrentRunId = runId;
        Runs.Add(new WorkItemRun { RunId = runId, WorkItemId = Id, Phase = phase, StartedAt = now });
        return [new StartRunOf(phase, runId)];
    }

    private AwaitUser Await(Guid waitId, WaitKind kind, string why, string url, DateTimeOffset now)
    {
        WaitId = waitId;
        WaitingFor = kind;
        WaitingBecause = WorkItemRules.WhyOf(why);
        WaitingSince = now;
        return new AwaitUser(waitId, kind, WaitingBecause, url);
    }

    private List<FlowStep> StopAwaiting()
    {
        if (WaitId is not { } waitId)
        {
            return [];
        }

        WaitId = null;
        WaitingFor = null;
        WaitingBecause = null;
        WaitingSince = null;
        return [new FlowStep.StopAwaiting(waitId)];
    }

    private static string EndedAs(RunEnded ended) => ended switch
    {
        { State: RunState.Finished, Question: not null } => "Finished, asking a Question",
        { State: RunState.Finished, Outcome: { } outcome } => $"Finished: {outcome}",
        _ => ended.State.ToString(),
    };
}
