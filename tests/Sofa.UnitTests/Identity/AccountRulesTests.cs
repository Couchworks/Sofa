using Sofa.Identity.SignIn;

namespace Sofa.UnitTests.Identity;

public class AccountRulesTests
{
    [Fact]
    public void A_complete_setup_has_no_problems() =>
        Assert.Empty(AccountRules.ProblemsWithSetup("Ada", "ada@example.com", "long enough!", "Ada's", "Engines"));

    [Fact]
    public void Every_missing_field_is_named()
    {
        var problems = AccountRules.ProblemsWithSetup(" ", null, null, "", null);

        Assert.Equal(["email", "name", "password", "teamName", "workspaceName"], problems.Keys.Order());
    }

    [Fact]
    public void An_address_without_an_at_sign_is_not_an_email() =>
        Assert.Contains("email", AccountRules.ProblemsWithSetup("Ada", "ada.example.com", "long enough!", "W", "T").Keys);

    [Fact]
    public void A_short_password_is_refused() =>
        Assert.Contains("password", AccountRules.ProblemsWithSetup("Ada", "ada@example.com", "short", "W", "T").Keys);
}
