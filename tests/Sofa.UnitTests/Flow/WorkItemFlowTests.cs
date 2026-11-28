using Sofa.Contracts.Code;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Flow.WorkItems;
using static Sofa.Flow.WorkItems.FlowStep;

namespace Sofa.UnitTests.Flow;

public class WorkItemFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ada = Guid.NewGuid();

    private static readonly FlowOfAProject TheDefaultFlow = new(
    [
        new PhaseInFlow(PhaseName.Triage, 0, Gate.Auto),
        new PhaseInFlow(PhaseName.Spec, 1, Gate.Approve),
        new PhaseInFlow(PhaseName.Plan, 2, Gate.Approve),
        new PhaseInFlow(PhaseName.Implement, 3, Gate.Auto),
    ]);

    private static readonly WorkItemPullRequest OpenPullRequest = new("acme/demo", 7, "https://github.com/acme/demo/pull/7", PullRequestState.Open);

    private static WorkItem Opened(Guid? delegatingUserId) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Number = 1,
        Key = "DEMO-1",
        Title = "Add a dark mode toggle",
        RequestId = Guid.NewGuid(),
        RequestRepository = "acme/demo",
        RequestNumber = 12,
        RequestUrl = "https://github.com/acme/demo/issues/12",
        DelegatingUserId = delegatingUserId,
        OpenedAt = Now,
    };

    private static RunEnded Finishing(WorkItem workItem, string outcome) =>
        new(workItem.CurrentRunId!.Value, workItem.WorkspaceId, workItem.Id, workItem.Phase!.Value, RunState.Finished, null, null, null, outcome, null);

    private static RunEnded Asking(WorkItem workItem, Guid questionId) =>
        new(workItem.CurrentRunId!.Value, workItem.WorkspaceId, workItem.Id, workItem.Phase!.Value, RunState.Finished, null, null, null, null,
            new AskedQuestion(questionId, "Should the toggle follow the system setting by default?"));

    private static RunEnded GoneWrong(WorkItem workItem, RunState state, Guid? retriedBy = null, Guid? retryOf = null, Guid? runId = null) =>
        new(runId ?? workItem.CurrentRunId!.Value, workItem.WorkspaceId, workItem.Id, workItem.Phase!.Value, state, "Its container ended.",
            state == RunState.Refused ? RefusedAs.AgentNotRegistered : null, retriedBy, null, null, retryOf);

    private static WorkItem StartedOnTheFullTrack()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "full"), TheDefaultFlow, Now);
        return workItem;
    }

    private static WorkItem ImplementedOnTheSmallTrack()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "small"), TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "planned"), TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "implemented"), TheDefaultFlow, Now);
        return workItem;
    }

    [Fact]
    public void A_delegated_Work_Item_enters_Triage_and_starts_its_Run()
    {
        var workItem = Opened(Ada);

        var steps = workItem.Begin(TheDefaultFlow, Now);

        Assert.Equal([new EnterPhase(PhaseName.Triage), new StartRunOf(PhaseName.Triage, workItem.CurrentRunId!.Value)], steps);
        Assert.Equal((WorkItemState.Running, PhaseName.Triage), (workItem.State, workItem.Phase));
        Assert.Equal(workItem.CurrentRunId, Assert.Single(workItem.Runs).RunId);
    }

    [Fact]
    public void A_Work_Item_no_User_delegated_waits_and_starts_nothing()
    {
        var workItem = Opened(delegatingUserId: null);

        Assert.Empty(workItem.Begin(TheDefaultFlow, Now));
        Assert.Equal((WorkItemState.AwaitsDelegation, (PhaseName?)null), (workItem.State, workItem.Phase));
    }

    [Fact]
    public void Triage_picking_Full_goes_on_to_Spec_and_reports_nothing()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);

        var steps = workItem.HearRunEnded(Finishing(workItem, "full"), TheDefaultFlow, Now);

        Assert.Equal([new EnterPhase(PhaseName.Spec), new StartRunOf(PhaseName.Spec, workItem.CurrentRunId!.Value)], steps);
        Assert.Equal((Track.Full, PhaseName.Spec), (workItem.Track, workItem.Phase));
        Assert.Equal(PhaseEndStanding.WentOn, Assert.Single(workItem.PhaseEnds).Standing);
    }

    [Fact]
    public void Triage_picking_Small_skips_Spec_and_leaves_Plan_Unverified_instead_of_waiting_for_approval()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);

        var toPlan = workItem.HearRunEnded(Finishing(workItem, "small"), TheDefaultFlow, Now);
        var planRun = workItem.CurrentRunId!.Value;
        var toImplement = workItem.HearRunEnded(Finishing(workItem, "planned"), TheDefaultFlow, Now);

        Assert.Equal([new EnterPhase(PhaseName.Plan), new StartRunOf(PhaseName.Plan, planRun)], toPlan);
        Assert.Equal(
            [new ReportPhase(PhaseName.Plan, planRun), new EnterPhase(PhaseName.Implement), new StartRunOf(PhaseName.Implement, workItem.CurrentRunId!.Value)],
            toImplement);
        var plan = workItem.PhaseEnds.Single(e => e.Phase == PhaseName.Plan);
        Assert.Equal((Gate.ReviewAfterwards, PhaseEndStanding.Unverified), (plan.Gate, plan.Standing));
        Assert.Equal(Track.Small, workItem.Track);
    }

    [Fact]
    public void A_specified_Spec_is_reported_and_waits_for_a_Users_approval_in_their_Inbox()
    {
        var workItem = StartedOnTheFullTrack();
        var specRun = workItem.CurrentRunId!.Value;

        var steps = workItem.HearRunEnded(Finishing(workItem, "specified"), TheDefaultFlow, Now);

        Assert.Equal(
            [new ReportPhase(PhaseName.Spec, specRun), new AwaitUser(specRun, WaitKind.Approval, "Its Spec waits for a User's approval.", workItem.RequestUrl)],
            steps);
        Assert.Equal((WorkItemState.AwaitsApproval, (Guid?)null), (workItem.State, workItem.CurrentRunId));
    }

    [Fact]
    public void Approving_the_Spec_settles_the_Inbox_item_and_goes_on_to_Plan()
    {
        var workItem = StartedOnTheFullTrack();
        var specRun = workItem.CurrentRunId!.Value;
        workItem.HearRunEnded(Finishing(workItem, "specified"), TheDefaultFlow, Now);

        var decision = workItem.Approve(PhaseName.Spec, Ada, Now, TheDefaultFlow);

        Assert.Null(decision.Refusal);
        Assert.Equal(
            [new FlowStep.StopAwaiting(specRun), new EnterPhase(PhaseName.Plan), new StartRunOf(PhaseName.Plan, workItem.CurrentRunId!.Value)],
            decision.Steps);
        var spec = workItem.PhaseEnds.Single(e => e.Phase == PhaseName.Spec);
        Assert.Equal((PhaseEndStanding.Approved, Ada), (spec.Standing, spec.DecidedByUserId));
        Assert.Null(workItem.Waiting);
    }

    [Fact]
    public void Sending_the_Spec_back_keeps_the_comment_and_starts_a_new_Run_of_Spec()
    {
        var workItem = StartedOnTheFullTrack();
        var specRun = workItem.CurrentRunId!.Value;
        workItem.HearRunEnded(Finishing(workItem, "specified"), TheDefaultFlow, Now);

        var decision = workItem.SendBack(PhaseName.Spec, Ada, " Say what the toggle remembers. ", Now);

        Assert.Equal([new FlowStep.StopAwaiting(specRun), new StartRunOf(PhaseName.Spec, workItem.CurrentRunId!.Value)], decision.Steps);
        Assert.NotEqual(specRun, workItem.CurrentRunId);
        var spec = workItem.PhaseEnds.Single(e => e.Phase == PhaseName.Spec);
        Assert.Equal((PhaseEndStanding.SentBack, "Say what the toggle remembers."), (spec.Standing, spec.Comment));
    }

    [Fact]
    public void Sending_back_takes_a_comment_and_only_a_Phase_waiting_for_approval()
    {
        var workItem = StartedOnTheFullTrack();

        var whileRunning = workItem.SendBack(PhaseName.Spec, Ada, "Change it.", Now);
        workItem.HearRunEnded(Finishing(workItem, "specified"), TheDefaultFlow, Now);
        var withoutAComment = workItem.SendBack(PhaseName.Spec, Ada, " ", Now);
        var anotherPhase = workItem.Approve(PhaseName.Plan, Ada, Now, TheDefaultFlow);

        Assert.Equal("DEMO-1 has no Spec waiting for a User's approval.", whileRunning.Refusal);
        Assert.Equal("Sending a Phase back takes a comment saying what to change.", withoutAComment.Refusal);
        Assert.Equal("DEMO-1 has no Plan waiting for a User's approval.", anotherPhase.Refusal);
        Assert.Equal(WorkItemState.AwaitsApproval, workItem.State);
    }

    [Fact]
    public void Approving_a_Plan_left_Unverified_marks_it_looked_at_and_moves_nothing()
    {
        var workItem = ImplementedOnTheSmallTrack();

        var decision = workItem.Approve(PhaseName.Plan, Ada, Now, TheDefaultFlow);

        Assert.Equal((null, 0), (decision.Refusal, decision.Steps.Count));
        Assert.Equal(PhaseEndStanding.Verified, workItem.PhaseEnds.Single(e => e.Phase == PhaseName.Plan).Standing);
        Assert.Equal(WorkItemState.AwaitsPullRequest, workItem.State);
    }

    [Fact]
    public void A_Question_waits_for_its_answer_and_the_answer_starts_a_new_Run_of_the_same_Phase()
    {
        var workItem = StartedOnTheFullTrack();
        var asking = workItem.CurrentRunId!.Value;
        var questionId = Guid.NewGuid();

        var asked = workItem.HearRunEnded(Asking(workItem, questionId), TheDefaultFlow, Now);
        var otherAnswer = workItem.HearQuestionAnswered(Guid.NewGuid(), Now);
        var answered = workItem.HearQuestionAnswered(questionId, Now);

        Assert.Equal(WaitKind.Question, Assert.IsType<AwaitUser>(Assert.Single(asked)).Kind);
        Assert.Empty(otherAnswer);
        Assert.Equal([new FlowStep.StopAwaiting(questionId), new StartRunOf(PhaseName.Spec, workItem.CurrentRunId!.Value)], answered);
        Assert.NotEqual(asking, workItem.CurrentRunId);
        Assert.Equal((WorkItemState.Running, PhaseName.Spec), (workItem.State, workItem.Phase));
        Assert.Equal("Finished, asking a Question", workItem.Runs.Single(r => r.RunId == asking).EndedAs);
    }

    [Fact]
    public void A_failed_Run_retried_by_Execution_is_followed_by_its_retry()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);
        var failed = workItem.CurrentRunId!.Value;
        var retry = Guid.NewGuid();

        var steps = workItem.HearRunEnded(GoneWrong(workItem, RunState.Failed, retriedBy: retry), TheDefaultFlow, Now);

        Assert.Empty(steps);
        Assert.Equal((WorkItemState.Running, retry), (workItem.State, workItem.CurrentRunId!.Value));
        Assert.Equal(failed, workItem.Runs.Single(r => r.RunId == retry).RetryOf);
    }

    [Fact]
    public void A_Run_gone_wrong_for_good_sends_the_Work_Item_to_its_delegating_Users_Inbox_until_a_Users_retry_ends()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);
        var refused = workItem.CurrentRunId!.Value;

        var goneWrong = workItem.HearRunEnded(GoneWrong(workItem, RunState.Refused), TheDefaultFlow, Now);
        var retry = Guid.NewGuid();
        var retried = workItem.HearRunEnded(
            new RunEnded(retry, workItem.WorkspaceId, workItem.Id, PhaseName.Triage, RunState.Finished, null, null, null, "full", null, RetryOf: refused),
            TheDefaultFlow, Now);

        var waiting = Assert.IsType<AwaitUser>(Assert.Single(goneWrong));
        Assert.Equal((refused, WaitKind.RunGoneWrong, "Its Triage Run ended Refused: Its container ended."), (waiting.WaitId, waiting.Kind, waiting.Why));
        Assert.Equal(new FlowStep.StopAwaiting(refused), retried[0]);
        Assert.Equal((Track.Full, PhaseName.Spec), (workItem.Track, workItem.Phase));
    }

    [Fact]
    public void The_end_of_a_Run_Flow_didnt_start_such_as_a_test_Run_moves_nothing()
    {
        var workItem = StartedOnTheFullTrack();
        var specRun = workItem.CurrentRunId;

        var steps = workItem.HearRunEnded(GoneWrong(workItem, RunState.Failed, runId: Guid.NewGuid()), TheDefaultFlow, Now);

        Assert.Empty(steps);
        Assert.Equal((WorkItemState.Running, specRun), (workItem.State, workItem.CurrentRunId));
    }

    [Fact]
    public void Implement_is_reported_then_waits_for_its_pull_request_and_its_merge_finishes_the_Work_Item()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "small"), TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "planned"), TheDefaultFlow, Now);
        var implementRun = workItem.CurrentRunId!.Value;

        var implemented = workItem.HearRunEnded(Finishing(workItem, "implemented"), TheDefaultFlow, Now);
        var opened = workItem.HearPullRequest(OpenPullRequest, Now);
        var merged = workItem.HearPullRequest(OpenPullRequest with { State = PullRequestState.Merged }, Now.AddHours(1));

        Assert.Equal([new ReportPhase(PhaseName.Implement, implementRun)], implemented);
        Assert.Empty(opened);
        Assert.Equal([new ReachMainBranch(), new ReportMerged(implementRun, OpenPullRequest with { State = PullRequestState.Merged })], merged);
        Assert.Equal((WorkItemState.Done, Now.AddHours(1)), (workItem.State, workItem.DoneAt));
        Assert.Empty(workItem.HearPullRequest(OpenPullRequest with { State = PullRequestState.ClosedUnmerged }, Now));
    }

    [Fact]
    public void A_pull_request_merged_while_Implement_still_runs_finishes_the_Work_Item_once_it_ends()
    {
        var workItem = Opened(Ada);
        workItem.Begin(TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "small"), TheDefaultFlow, Now);
        workItem.HearRunEnded(Finishing(workItem, "planned"), TheDefaultFlow, Now);

        var whileRunning = workItem.HearPullRequest(OpenPullRequest with { State = PullRequestState.Merged }, Now);
        var implemented = workItem.HearRunEnded(Finishing(workItem, "implemented"), TheDefaultFlow, Now);

        Assert.Empty(whileRunning);
        Assert.Contains(new ReachMainBranch(), implemented);
        Assert.Equal(WorkItemState.Done, workItem.State);
    }

    [Fact]
    public void A_pull_request_closed_unmerged_sends_the_Work_Item_to_its_delegating_Users_Inbox_and_reopening_it_takes_it_back()
    {
        var workItem = ImplementedOnTheSmallTrack();

        var closed = workItem.HearPullRequest(OpenPullRequest with { State = PullRequestState.ClosedUnmerged }, Now);
        var waitId = workItem.Waiting!.WaitId;
        var reopened = workItem.HearPullRequest(OpenPullRequest, Now);

        var waiting = Assert.IsType<AwaitUser>(Assert.Single(closed));
        Assert.Equal((WaitKind.PullRequestClosedUnmerged, OpenPullRequest.Url), (waiting.Kind, waiting.Url));
        Assert.Equal([new FlowStep.StopAwaiting(waitId)], reopened);
        Assert.Equal(WorkItemState.AwaitsPullRequest, workItem.State);
    }

    [Theory]
    [InlineData(null, PhaseName.Implement, Gate.Auto)]
    [InlineData(Track.Small, PhaseName.Plan, Gate.ReviewAfterwards)]
    [InlineData(Track.Full, PhaseName.Plan, Gate.Approve)]
    [InlineData(Track.Small, PhaseName.Spec, Gate.Approve)]
    public void The_Small_Track_turns_only_Plans_Approve_Gate_to_Review_afterwards(Track? track, PhaseName phase, Gate gate) =>
        Assert.Equal(gate, TheDefaultFlow.GateOf(phase, track));

    [Theory]
    [InlineData(PhaseName.Triage, Track.Full, PhaseName.Spec)]
    [InlineData(PhaseName.Triage, Track.Small, PhaseName.Plan)]
    [InlineData(PhaseName.Plan, Track.Small, PhaseName.Implement)]
    [InlineData(PhaseName.Implement, Track.Full, null)]
    public void The_next_Phase_follows_the_Flows_order_skipping_Spec_on_the_Small_Track(PhaseName from, Track track, PhaseName? next) =>
        Assert.Equal(next, TheDefaultFlow.After(from, track));
}
