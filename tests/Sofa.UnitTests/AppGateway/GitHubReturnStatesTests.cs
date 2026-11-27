using Microsoft.Extensions.Options;
using Sofa.AppGateway.Admins;
using Sofa.AppGateway.GitHubReturns;

namespace Sofa.UnitTests.AppGateway;

public class GitHubReturnStatesTests
{
    private static readonly SignedInAdmin Ada = new(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void A_state_Sofa_signed_names_the_Admin_and_Workspace_who_started_it()
    {
        var states = StatesAt(new Clock());

        var (state, minted) = states.Mint(GitHubReturnTo.Created, Ada);
        var read = states.Read(state, GitHubReturnTo.Created);

        Assert.Equal(minted, read);
        Assert.Equal((Ada.UserId, Ada.WorkspaceId), (read!.AdminUserId, read.WorkspaceId));
    }

    [Fact]
    public void A_state_changed_on_its_way_back_is_refused()
    {
        var states = StatesAt(new Clock());
        var (state, _) = states.Mint(GitHubReturnTo.Installed, Ada);
        var otherwise = states.Mint(GitHubReturnTo.Installed, Ada with { WorkspaceId = Guid.NewGuid() }).State;

        var payloadOfAnother = otherwise.Split('.')[0] + "." + state.Split('.')[1];

        Assert.Null(states.Read(payloadOfAnother, GitHubReturnTo.Installed));
        Assert.Null(states.Read("made-up", GitHubReturnTo.Installed));
        Assert.Null(states.Read(null, GitHubReturnTo.Installed));
    }

    [Fact]
    public void A_state_signed_with_another_key_is_refused()
    {
        var (state, _) = StatesAt(new Clock(), "another key of at least thirty-two characters").Mint(GitHubReturnTo.Created, Ada);

        Assert.Null(StatesAt(new Clock()).Read(state, GitHubReturnTo.Created));
    }

    [Fact]
    public void A_state_for_creating_the_App_does_not_finish_an_installation()
    {
        var states = StatesAt(new Clock());
        var (state, _) = states.Mint(GitHubReturnTo.Created, Ada);

        Assert.Null(states.Read(state, GitHubReturnTo.Installed));
    }

    [Fact]
    public void A_state_is_refused_once_it_has_expired()
    {
        var clock = new Clock();
        var states = StatesAt(clock);
        var (state, _) = states.Mint(GitHubReturnTo.Created, Ada);

        clock.Now += new GitHubReturnOptions().Lifetime + TimeSpan.FromSeconds(1);

        Assert.Null(states.Read(state, GitHubReturnTo.Created));
    }

    [Fact]
    public void Refuses_to_sign_without_a_long_enough_key()
    {
        var states = StatesAt(new Clock(), "short");

        Assert.Throws<InvalidOperationException>(() => states.Mint(GitHubReturnTo.Created, Ada));
    }

    private static GitHubReturnStates StatesAt(Clock clock, string key = "a signing key of at least thirty-two characters") =>
        new(Options.Create(new GitHubReturnOptions { SigningKey = key }), clock);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
