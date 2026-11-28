using Sofa.Contracts.Execution;
using Sofa.Notices.Inbox;
using Sofa.Notices.SignIns;

namespace Sofa.UnitTests.Notices;

public class SignInWarningsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid RunnerId = Guid.NewGuid();

    private static RunnerSignIn SignedInUntil(DateTimeOffset endsAt)
    {
        var signIn = new RunnerSignIn { RunnerId = RunnerId, WorkspaceId = Guid.NewGuid(), UserId = Guid.NewGuid(), RunnerName = "Ada's Max" };
        signIn.Hear(new SubscriptionSignInSeen(signIn.WorkspaceId, signIn.UserId, RunnerId, "Ada's Max", SignedIn: true, endsAt, Now));
        return signIn;
    }

    [Fact]
    public void A_sign_in_ending_within_three_days_is_in_its_Users_Inbox_with_the_command_to_sign_in_again()
    {
        var item = InboxEndpoints.SignInItem(SignedInUntil(Now.AddDays(2)), Now);

        Assert.NotNull(item);
        Assert.Equal(("SubscriptionSignInEnding", "Ada's Max's sign-in ends soon", "/runners"), (item.Kind, item.Title, item.Url));
        Assert.Contains($"sofa runner login {RunnerId}", item.Detail);
        Assert.Equal(Now.AddDays(-1), item.RaisedAt);
    }

    [Fact]
    public void A_sign_in_ending_later_is_in_no_Inbox()
    {
        Assert.Null(InboxEndpoints.SignInItem(SignedInUntil(Now.AddDays(20)), Now));
    }

    [Fact]
    public void A_sign_in_that_refused_a_Run_says_its_Runner_is_signed_out_until_a_later_sign_in_is_seen()
    {
        var signIn = SignedInUntil(Now.AddDays(20));

        signIn.HearSignedOut(new SubscriptionSignedOut(signIn.WorkspaceId, signIn.UserId, RunnerId, "Ada's Max", Guid.NewGuid()), Now);
        var signedOut = InboxEndpoints.SignInItem(signIn, Now);
        signIn.Hear(new SubscriptionSignInSeen(signIn.WorkspaceId, signIn.UserId, RunnerId, "Ada's Max", SignedIn: true, Now.AddDays(30), Now.AddHours(1)));

        Assert.Equal("SubscriptionSignedOut", signedOut?.Kind);
        Assert.Null(InboxEndpoints.SignInItem(signIn, Now.AddHours(1)));
    }

    [Fact]
    public void A_sign_in_seen_before_the_one_already_heard_is_ignored()
    {
        var signIn = SignedInUntil(Now.AddDays(20));

        var heard = signIn.Hear(new SubscriptionSignInSeen(signIn.WorkspaceId, signIn.UserId, RunnerId, "Ada's Max", SignedIn: false, null, Now.AddHours(-1)));

        Assert.False(heard);
        Assert.True(signIn.SignedIn);
    }
}
