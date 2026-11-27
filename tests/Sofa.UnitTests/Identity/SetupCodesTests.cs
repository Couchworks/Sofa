using Sofa.Identity.Setup;

namespace Sofa.UnitTests.Identity;

public class SetupCodesTests
{
    [Fact]
    public void A_setup_code_says_what_it_is_and_is_new_each_time()
    {
        var first = SetupCodes.Issue();
        var second = SetupCodes.Issue();

        Assert.StartsWith(SetupCodes.Prefix, first);
        Assert.Matches("^sofa_setup_[A-Za-z0-9_-]{32}$", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_setup_code_matches_its_own_hash_even_with_whitespace_pasted_around_it()
    {
        var code = SetupCodes.Issue();

        Assert.True(SetupCodes.Matches($"  {code}\n", SetupCodes.Hash(code)));
    }

    [Fact]
    public void Another_code_does_not_match()
    {
        var hash = SetupCodes.Hash(SetupCodes.Issue());

        Assert.False(SetupCodes.Matches(SetupCodes.Issue(), hash));
    }

    [Fact]
    public void Only_the_hash_is_kept_never_the_code()
    {
        var code = SetupCodes.Issue();

        Assert.DoesNotContain(code, SetupCodes.Hash(code));
    }
}
