using Sofa.Contracts.Connections;
using Sofa.Identity.GitHub;
using Sofa.Identity.Users;

namespace Sofa.UnitTests.Identity;

public class GitHubLinkRulesTests
{
    private static readonly GitHubUserAccount BeaOnGitHub = new(1002, "bea-gh", "Bea", "bea@example.com");

    [Fact]
    public void An_account_signs_in_as_the_User_who_holds_its_id()
    {
        var bea = WithPassword("bea@example.com");
        bea.GitHubAccountId = BeaOnGitHub.Id;

        Assert.Same(bea, GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [bea])!.User);
    }

    [Fact]
    public void An_account_first_signs_in_as_the_User_an_Admin_added_for_it()
    {
        var added = AddedFor(BeaOnGitHub.Id);

        var match = GitHubLinkRules.UserSigningInAs(BeaOnGitHub with { VerifiedPrimaryEmail = null }, [added]);

        Assert.Same(added, match!.User);
        Assert.Null(match.AddedAndNeverSignedInToMergeIn);
    }

    [Fact]
    public void A_renamed_account_still_signs_in_as_the_User_added_for_it()
    {
        var added = AddedFor(BeaOnGitHub.Id);
        added.GitHubLogin = "bea-gh";

        Assert.Same(added, GitHubLinkRules.UserSigningInAs(BeaOnGitHub with { Login = "bea-renamed" }, [added])!.User);
    }

    [Fact]
    public void Else_it_signs_in_as_the_User_with_its_verified_primary_email()
    {
        var bea = WithPassword("Bea@Example.com");

        Assert.Same(bea, GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [bea])!.User);
    }

    [Fact]
    public void An_account_no_User_has_signs_in_as_no_one()
    {
        Assert.Null(GitHubLinkRules.UserSigningInAs(BeaOnGitHub, []));
        Assert.Null(GitHubLinkRules.UserSigningInAs(BeaOnGitHub with { VerifiedPrimaryEmail = null }, [WithPassword("bea@example.com")]));
    }

    [Fact]
    public void A_User_whose_email_has_another_GitHub_account_is_not_taken_over()
    {
        var bea = WithPassword("bea@example.com");
        bea.GitHubAccountId = 9999;

        Assert.Null(GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [bea]));
    }

    [Fact]
    public void Someone_added_for_an_account_who_already_has_a_User_by_email_is_one_person()
    {
        var added = AddedFor(BeaOnGitHub.Id);
        var bea = WithPassword("bea@example.com");

        var match = GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [added, bea]);

        Assert.Same(bea, match!.User);
        Assert.Same(added, match.AddedAndNeverSignedInToMergeIn);
    }

    [Fact]
    public void A_User_added_for_an_account_who_has_signed_in_stays_their_own()
    {
        var added = AddedFor(BeaOnGitHub.Id);
        added.SignedIn(DateTimeOffset.UnixEpoch);
        var someoneWithTheEmail = WithPassword("bea@example.com");

        var match = GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [added, someoneWithTheEmail]);

        Assert.Same(added, match!.User);
        Assert.Null(match.AddedAndNeverSignedInToMergeIn);
    }

    [Fact]
    public void Linking_takes_over_the_User_an_Admin_added_for_that_account()
    {
        var ada = WithPassword("ada@example.com");
        var added = AddedFor(BeaOnGitHub.Id);

        var match = GitHubLinkRules.Linking(ada, BeaOnGitHub, [ada, added]);

        Assert.Null(match.Refusal);
        Assert.Same(added, match.AddedAndNeverSignedInToMergeIn);
    }

    [Fact]
    public void Linking_an_account_another_User_has_signed_in_with_is_refused()
    {
        var ada = WithPassword("ada@example.com");
        var bea = AddedFor(BeaOnGitHub.Id);
        bea.SignedIn(DateTimeOffset.UnixEpoch);

        Assert.Equal(LinkRefusal.GitHubAccountHasAnotherUser, GitHubLinkRules.Linking(ada, BeaOnGitHub, [ada, bea]).Refusal);
    }

    [Fact]
    public void Linking_a_second_account_is_refused()
    {
        var ada = WithPassword("ada@example.com");
        ada.GitHubAccountId = 1001;

        Assert.Equal(LinkRefusal.UserHasAnotherGitHubAccount, GitHubLinkRules.Linking(ada, BeaOnGitHub, [ada]).Refusal);
    }

    private static User WithPassword(string email) => new()
    {
        Name = email, Email = email, NormalizedEmail = User.Normalize(email), PasswordHash = "hashed", CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static User AddedFor(long accountId) => new()
    {
        Name = "added", GitHubAccountId = accountId, GitHubLogin = "added", CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
