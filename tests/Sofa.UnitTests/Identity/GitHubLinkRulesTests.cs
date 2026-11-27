using Sofa.Contracts.Connections;
using Sofa.Identity.GitHub;
using Sofa.Identity.Users;

namespace Sofa.UnitTests.Identity;

public class GitHubLinkRulesTests
{
    private static readonly GitHubUserAccount BeaOnGitHub = new(1002, "Bea-GH", "Bea", "bea@example.com");

    [Fact]
    public void A_linked_account_signs_in_as_its_User()
    {
        var bea = WithPassword("bea@example.com");
        bea.GitHubAccountId = BeaOnGitHub.Id;

        Assert.Same(bea, GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [bea])!.User);
    }

    [Fact]
    public void An_account_first_signs_in_as_the_User_an_Admin_added_by_its_login()
    {
        var added = AddedByLogin("bea-gh");
        var someoneElseWithTheEmail = WithPassword("bea@example.com");
        someoneElseWithTheEmail.GitHubLogin = "not-bea";

        var match = GitHubLinkRules.UserSigningInAs(BeaOnGitHub with { VerifiedPrimaryEmail = null }, [added, someoneElseWithTheEmail]);

        Assert.Same(added, match!.User);
        Assert.Null(match.AddedByLoginToMergeIn);
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
    public void Someone_added_by_login_who_already_has_a_User_by_email_is_one_person()
    {
        var added = AddedByLogin("bea-gh");
        var bea = WithPassword("bea@example.com");

        var match = GitHubLinkRules.UserSigningInAs(BeaOnGitHub, [added, bea]);

        Assert.Same(bea, match!.User);
        Assert.Same(added, match.AddedByLoginToMergeIn);
    }

    [Fact]
    public void Linking_takes_over_the_User_an_Admin_added_by_that_login()
    {
        var ada = WithPassword("ada@example.com");
        var added = AddedByLogin("bea-gh");

        var match = GitHubLinkRules.Linking(ada, BeaOnGitHub, [ada, added]);

        Assert.Null(match.Refusal);
        Assert.Same(added, match.AddedByLoginToMergeIn);
    }

    [Fact]
    public void Linking_an_account_another_User_has_is_refused()
    {
        var ada = WithPassword("ada@example.com");
        var bea = WithPassword("bea@example.com");
        bea.GitHubAccountId = BeaOnGitHub.Id;

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

    private static User AddedByLogin(string login) => new()
    {
        Name = login, GitHubLogin = login, NormalizedGitHubLogin = User.NormalizeGitHubLogin(login), CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
