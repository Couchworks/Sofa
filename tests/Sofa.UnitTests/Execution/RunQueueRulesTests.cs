using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;

namespace Sofa.UnitTests.Execution;

public class RunQueueRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ada = Guid.NewGuid();
    private static readonly Guid Engines = Guid.NewGuid();

    private static Runner RunnerOf(Run run, RunnerKind kind, Guid? userId = null, Guid? teamId = null, int runsAtOnce = 1, int createdMinutesAgo = 0) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = run.WorkspaceId,
        Name = $"{kind} of {(userId is null ? "the Team" : "Ada")}",
        Kind = kind,
        Vendor = Vendor.Anthropic,
        OwnerUserId = userId,
        OwnerTeamId = teamId,
        RunsAtOnce = runsAtOnce,
        ApiKeyId = kind == RunnerKind.ApiKey ? Guid.NewGuid() : null,
        WorkerVolume = kind == RunnerKind.Subscription ? "sofa-worker-x" : null,
        CreatedAt = Now.AddMinutes(-createdMinutesAgo),
    };

    private static RunnerChoice Choose(Run run, IEnumerable<Runner> runners, Dictionary<Guid, int>? running = null) =>
        RunQueueRules.ChooseFor(run, runners, [Engines], running ?? []);

    [Fact]
    public void Runs_wait_highest_Priority_first_then_oldest_first()
    {
        var lowOld = RunTests.Waiting(priority: Priority.Low, queuedAt: Now.AddMinutes(-10));
        var urgent = RunTests.Waiting(priority: Priority.Urgent, queuedAt: Now);
        var noneOldest = RunTests.Waiting(priority: Priority.None, queuedAt: Now.AddHours(-1));
        var lowNew = RunTests.Waiting(priority: Priority.Low, queuedAt: Now);

        Assert.Equal([urgent, lowOld, lowNew, noneOldest], RunQueueRules.InTurn([lowNew, noneOldest, urgent, lowOld]));
    }

    [Fact]
    public void A_Run_spends_its_delegating_Users_personal_Runner_before_their_Teams()
    {
        var run = RunTests.Waiting(delegatingUserId: Ada);
        var team = RunnerOf(run, RunnerKind.ApiKey, teamId: Engines, createdMinutesAgo: 60);
        var personal = RunnerOf(run, RunnerKind.ApiKey, userId: Ada);

        Assert.Equal(new RunnerChoice.Spend(personal), Choose(run, [team, personal]));
    }

    [Fact]
    public void A_Run_waits_for_its_Users_own_Runner_at_its_limit_rather_than_spend_the_Teams()
    {
        var run = RunTests.Waiting(delegatingUserId: Ada);
        var personal = RunnerOf(run, RunnerKind.ApiKey, userId: Ada);
        var team = RunnerOf(run, RunnerKind.ApiKey, teamId: Engines);

        var choice = Choose(run, [team, personal], new() { [personal.Id] = 1 });

        Assert.Contains("at its limit", Assert.IsType<RunnerChoice.Wait>(choice).Why);
    }

    [Fact]
    public void A_subscription_comes_before_an_API_key_of_the_same_User()
    {
        var run = RunTests.Waiting(delegatingUserId: Ada);
        var apiKey = RunnerOf(run, RunnerKind.ApiKey, userId: Ada, createdMinutesAgo: 60);
        var subscription = RunnerOf(run, RunnerKind.Subscription, userId: Ada);

        Assert.Equal(new RunnerChoice.Spend(subscription), Choose(run, [apiKey, subscription]));
        Assert.Equal(new RunnerChoice.Spend(apiKey), Choose(run, [apiKey, subscription], new() { [subscription.Id] = 1 }));
    }

    [Fact]
    public void A_User_with_no_Runner_spends_their_Teams_and_never_another_Users()
    {
        var run = RunTests.Waiting(delegatingUserId: Ada);
        var someoneElses = RunnerOf(run, RunnerKind.ApiKey, userId: Guid.NewGuid());
        var anotherTeams = RunnerOf(run, RunnerKind.ApiKey, teamId: Guid.NewGuid());
        var theirTeams = RunnerOf(run, RunnerKind.ApiKey, teamId: Engines);

        Assert.Equal(new RunnerChoice.Spend(theirTeams), Choose(run, [someoneElses, anotherTeams, theirTeams]));
    }

    [Fact]
    public void A_Run_with_no_Runner_it_may_spend_waits_saying_so()
    {
        var run = RunTests.Waiting(delegatingUserId: Ada);
        var someoneElses = RunnerOf(run, RunnerKind.ApiKey, userId: Guid.NewGuid());

        var choice = Choose(run, [someoneElses]);

        Assert.Contains("no Anthropic Runner of their own", Assert.IsType<RunnerChoice.Wait>(choice).Why);
    }

    [Fact]
    public void A_plan_gives_a_Runner_no_more_Runs_than_its_limit_and_the_most_urgent_go_first()
    {
        var workspace = Guid.NewGuid();
        var low = RunTests.Waiting(priority: Priority.Low, delegatingUserId: Ada, workspaceId: workspace);
        var high = RunTests.Waiting(priority: Priority.High, delegatingUserId: Ada, workspaceId: workspace);
        var runner = RunnerOf(low, RunnerKind.ApiKey, userId: Ada, runsAtOnce: 1);

        var plan = RunQueueRules.Plan([low, high], [runner], _ => [], new Dictionary<Guid, int>());

        Assert.Equal([high.Id], plan.ToStart.Select(start => start.Run.Id));
        Assert.Contains("at its limit", plan.WaitingBecause[low.Id]);
    }

    [Fact]
    public void A_Runner_of_another_Workspace_is_never_spent()
    {
        var run = RunTests.Waiting(delegatingUserId: Ada);
        var elsewhere = RunnerOf(RunTests.Waiting(), RunnerKind.ApiKey, userId: Ada);

        Assert.IsType<RunnerChoice.Wait>(Choose(run, [elsewhere]));
    }
}
