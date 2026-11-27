using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Runners;

namespace Sofa.UnitTests.Execution;

public class RunnerRulesTests
{
    private static readonly Guid Ada = Guid.NewGuid();
    private static readonly Guid Bea = Guid.NewGuid();
    private static readonly Guid Engines = Guid.NewGuid();

    private static CreateRunner Runner(RunnerKind kind, RunnerOwner owner, Guid? apiKeyId = null, int runsAtOnce = 2, string name = "Mine") =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, kind, Vendor.Anthropic, owner, runsAtOnce, apiKeyId);

    [Fact]
    public void A_personal_subscription_Runner_is_fine()
    {
        Assert.Null(RunnerRules.ProblemWith(Runner(RunnerKind.Subscription, RunnerOwner.User(Ada))));
    }

    [Fact]
    public void A_subscription_Runner_for_a_Team_is_refused()
    {
        Assert.NotNull(RunnerRules.ProblemWith(Runner(RunnerKind.Subscription, RunnerOwner.Team(Engines))));
    }

    [Fact]
    public void An_API_key_Runner_needs_its_key_and_a_subscription_Runner_holds_none()
    {
        Assert.NotNull(RunnerRules.ProblemWith(Runner(RunnerKind.ApiKey, RunnerOwner.Team(Engines))));
        Assert.Null(RunnerRules.ProblemWith(Runner(RunnerKind.ApiKey, RunnerOwner.Team(Engines), Guid.NewGuid())));
        Assert.NotNull(RunnerRules.ProblemWith(Runner(RunnerKind.Subscription, RunnerOwner.User(Ada), Guid.NewGuid())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(RunnerLimits.MostRunsAtOnce + 1)]
    public void A_limit_outside_its_range_is_refused(int runsAtOnce)
    {
        Assert.NotNull(RunnerRules.ProblemWith(Runner(RunnerKind.Subscription, RunnerOwner.User(Ada), runsAtOnce: runsAtOnce)));
    }

    [Fact]
    public void A_Runner_needs_a_name()
    {
        Assert.NotNull(RunnerRules.ProblemWith(Runner(RunnerKind.Subscription, RunnerOwner.User(Ada), name: " ")));
    }

    [Fact]
    public void A_Worker_volume_is_named_after_its_Runner()
    {
        var runnerId = Guid.NewGuid();

        Assert.Equal($"sofa-worker-{runnerId}", RunnerRules.WorkerVolumeOf(runnerId));
    }

    [Fact]
    public void A_personal_Runner_is_seen_and_changed_by_its_own_User_only()
    {
        var adas = Stored(RunnerOwner.User(Ada));

        Assert.True(RunnerRules.IsSeenBy(adas, Ada, [], isAdmin: false));
        Assert.False(RunnerRules.IsSeenBy(adas, Bea, [Engines], isAdmin: true));
        Assert.Null(RunnerRules.WhyTheyMayNotChange(adas, Ada, isAdmin: false));
        Assert.NotNull(RunnerRules.WhyTheyMayNotChange(adas, Bea, isAdmin: true));
    }

    [Fact]
    public void A_team_Runner_is_seen_by_its_Team_and_the_Admins_and_changed_by_Admins()
    {
        var teams = Stored(RunnerOwner.Team(Engines));

        Assert.True(RunnerRules.IsSeenBy(teams, Bea, [Engines], isAdmin: false));
        Assert.False(RunnerRules.IsSeenBy(teams, Bea, [], isAdmin: false));
        Assert.True(RunnerRules.IsSeenBy(teams, Ada, [], isAdmin: true));
        Assert.NotNull(RunnerRules.WhyTheyMayNotChange(teams, Bea, isAdmin: false));
        Assert.Null(RunnerRules.WhyTheyMayNotChange(teams, Ada, isAdmin: true));
    }

    [Fact]
    public void Only_a_subscription_Runner_shows_its_sign_in()
    {
        var apiKey = Stored(RunnerOwner.Team(Engines));
        var subscription = new Runner
        {
            Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), Name = "Mine", Kind = RunnerKind.Subscription, Vendor = Vendor.Anthropic,
            OwnerUserId = Ada, RunsAtOnce = 2, WorkerVolume = "sofa-worker-x", SignedIn = true, SignInEndsAt = DateTimeOffset.UnixEpoch,
        };

        Assert.Null(apiKey.Details.SignIn);
        Assert.True(apiKey.Details.HasApiKey);
        Assert.Equal(new SubscriptionSignIn(true, DateTimeOffset.UnixEpoch, null), subscription.Details.SignIn);
        Assert.False(subscription.Details.HasApiKey);
    }

    private static Runner Stored(RunnerOwner owner) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        Name = "Runner",
        Kind = RunnerKind.ApiKey,
        Vendor = Vendor.Anthropic,
        OwnerUserId = owner.Kind == RunnerOwnerKind.User ? owner.Id : null,
        OwnerTeamId = owner.Kind == RunnerOwnerKind.Team ? owner.Id : null,
        RunsAtOnce = 2,
        ApiKeyId = Guid.NewGuid(),
    };
}
