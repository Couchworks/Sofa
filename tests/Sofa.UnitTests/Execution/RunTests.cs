using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Runs;

namespace Sofa.UnitTests.Execution;

public class RunTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    public static Run Waiting(
        int retryLimit = 2, Priority priority = Priority.None, DateTimeOffset? queuedAt = null, Guid? delegatingUserId = null, Guid? workspaceId = null) =>
        Run.From(new StartRun(
            Guid.NewGuid(), workspaceId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PhaseName.Triage, Guid.NewGuid(), Vendor.Anthropic, "haiku",
            Sandbox.ReadOnly, delegatingUserId ?? Guid.NewGuid(), priority, "Write a haiku.", retryLimit), queuedAt ?? Now);

    private static Run Running(int retryLimit = 2)
    {
        var run = Waiting(retryLimit);
        run.StartOn(Guid.NewGuid(), Now);
        return run;
    }

    [Fact]
    public void A_Run_finishes_only_while_it_runs()
    {
        var waiting = Waiting();
        var running = Running();

        Assert.False(waiting.Finish("small", Now));
        Assert.True(running.Finish("small", Now));
        Assert.Equal((RunState.Finished, "small"), (running.State, running.Outcome));
        Assert.False(running.Fail("too late", Now));
        Assert.Equal(RunState.Finished, running.State);
    }

    [Fact]
    public void A_Run_ends_with_a_Question_only_while_it_runs_and_says_so_when_it_ends()
    {
        var running = Running();
        var questionId = Guid.NewGuid();

        Assert.False(Waiting().EndWithQuestion(questionId, "Follow the system?", Now));
        Assert.True(running.EndWithQuestion(questionId, "Follow the system?", Now));
        Assert.False(running.Finish("small", Now));
        Assert.Equal((RunState.Finished, null, new AskedQuestion(questionId, "Follow the system?")),
            (running.State, running.Outcome, running.Asked));
        Assert.Equal(new AskedQuestion(questionId, "Follow the system?"), running.Ended.Question);
        Assert.Equal(new AskedQuestion(questionId, "Follow the system?"), running.Details.Question);
    }

    [Fact]
    public void A_finished_Run_says_its_Outcome_and_Phase_when_it_ends()
    {
        var running = Running();
        running.Finish("full", Now);

        Assert.Equal((PhaseName.Triage, "full", (AskedQuestion?)null), (running.Ended.Phase, running.Ended.Outcome, running.Ended.Question));
        Assert.Equal("full", running.Details.Outcome);
    }

    [Fact]
    public void A_failed_Run_with_retries_left_is_retried_as_a_new_Run_with_one_fewer()
    {
        var failed = Running(retryLimit: 2);
        failed.Fail("killed", Now);

        var retry = failed.RetryOnItsOwn(Guid.NewGuid(), Now)!;

        Assert.Equal((RunState.Waiting, 2, 1, failed.Id), (retry.State, retry.Attempt, retry.RetriesLeft, retry.RetryOf));
        Assert.Equal((retry.Id, false), (failed.RetriedBy, failed.WaitsForRetry));
        Assert.Equal((failed.Brief, failed.AgentId, failed.Model, failed.WorkItemId), (retry.Brief, retry.AgentId, retry.Model, retry.WorkItemId));
        Assert.Null(failed.RetryOnItsOwn(Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_lost_Run_is_retried_like_a_failed_one()
    {
        var lost = Running(retryLimit: 1);
        lost.Lose("Execution restarted", Now);

        Assert.NotNull(lost.RetryOnItsOwn(Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_failed_Run_past_its_limit_waits_for_a_Users_Retry_which_starts_afresh()
    {
        var failed = Running(retryLimit: 0);
        failed.Fail("killed", Now);

        Assert.True(failed.WaitsForRetry);
        Assert.Null(failed.RetryOnItsOwn(Guid.NewGuid(), Now));
        var retry = failed.RetryForAUser(Guid.NewGuid(), Now)!;
        Assert.Equal((2, failed.RetryLimit), (retry.Attempt, retry.RetriesLeft));
        Assert.False(failed.WaitsForRetry);
        Assert.Null(failed.RetryForAUser(Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_refused_Run_is_never_retried_on_its_own_but_a_User_may_retry_it()
    {
        var refused = Running();
        refused.Refuse(RefusedAs.SignInAgain, "Sign in again", Now);

        Assert.Equal((RunState.Refused, RefusedAs.SignInAgain, true), (refused.State, refused.RefusedAs, refused.WaitsForRetry));
        Assert.Null(refused.RetryOnItsOwn(Guid.NewGuid(), Now));
        Assert.NotNull(refused.RetryForAUser(Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_stopped_or_finished_Run_is_not_retried()
    {
        var stopped = Waiting();
        stopped.Stop("Stopped by a User", Now);
        var finished = Running();
        finished.Finish("done", Now);

        Assert.Null(stopped.RetryForAUser(Guid.NewGuid(), Now));
        Assert.Null(finished.RetryForAUser(Guid.NewGuid(), Now));
        Assert.False(stopped.Stop("again", Now));
    }

    [Fact]
    public void A_waiting_Run_says_why_and_forgets_it_once_it_starts()
    {
        var run = Waiting();

        Assert.True(run.WaitBecause("no Runner"));
        Assert.False(run.WaitBecause("no Runner"));
        Assert.True(run.StartOn(Guid.NewGuid(), Now));
        Assert.Null(run.Why);
        Assert.False(run.StartOn(Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_Run_names_the_Run_retrying_it_when_it_ends()
    {
        var failed = Running();
        failed.Fail("killed", Now);
        var retry = failed.RetryOnItsOwn(Guid.NewGuid(), Now)!;

        Assert.Equal(new RunEnded(failed.Id, failed.WorkspaceId, failed.WorkItemId, PhaseName.Triage, RunState.Failed, "killed", null, retry.Id, null, null), failed.Ended);
    }

    [Theory]
    [InlineData("", 2)]
    [InlineData("Write a haiku.", -1)]
    [InlineData("Write a haiku.", RunLimits.MostRetries + 1)]
    public void A_Run_with_no_Brief_or_a_retry_limit_out_of_range_is_not_queued(string brief, int retryLimit)
    {
        var start = new StartRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PhaseName.Triage, Guid.NewGuid(),
            Vendor.Anthropic, "haiku", Sandbox.ReadOnly, Guid.NewGuid(), Priority.None, brief, retryLimit);

        Assert.NotNull(RunRules.ProblemWith(start));
    }
}
