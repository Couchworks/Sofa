using Sofa.Contracts.Flow;
using Sofa.Flow.Projects;

namespace Sofa.UnitTests.Flow;

public class ProjectRulesTests
{
    private static readonly Guid Connection = Guid.NewGuid();

    [Theory]
    [InlineData("DEMO")]
    [InlineData("WEB2")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJ")]
    public void A_key_is_letters_and_digits_starting_with_a_letter(string key)
    {
        Assert.Null(ProjectRules.KeyProblem(ProjectRules.NormalizeKey(key)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("D")]
    [InlineData("2FA")]
    [InlineData("DE-MO")]
    [InlineData("ABCDEFGHIJK")]
    public void A_key_that_breaks_the_rules_is_refused(string key)
    {
        Assert.NotNull(ProjectRules.KeyProblem(ProjectRules.NormalizeKey(key)));
    }

    [Fact]
    public void A_key_is_kept_in_capitals_without_surrounding_spaces()
    {
        Assert.Equal("DEMO", ProjectRules.NormalizeKey(" demo "));
    }

    [Fact]
    public void A_binding_names_a_Repository_and_a_label_for_Requests_and_Repositories_for_code()
    {
        Assert.Null(ProjectRules.BindingShapeProblem(
            new RequestProviderBinding(Connection, "acme/demo", "sofa"),
            new CodeProviderBinding(Connection, ["acme/demo", "acme/api"])));
    }

    [Fact]
    public void A_binding_without_code_Repositories_is_refused()
    {
        Assert.NotNull(ProjectRules.BindingShapeProblem(
            new RequestProviderBinding(Connection, "acme/demo", "sofa"), new CodeProviderBinding(Connection, [])));
    }

    [Fact]
    public void A_binding_naming_a_code_Repository_twice_is_refused()
    {
        Assert.NotNull(ProjectRules.BindingShapeProblem(
            new RequestProviderBinding(Connection, "acme/demo", "sofa"), new CodeProviderBinding(Connection, ["acme/demo", "ACME/demo"])));
    }

    [Theory]
    [InlineData("demo", "sofa")]
    [InlineData("acme/demo", "")]
    [InlineData("acme/demo/extra", "sofa")]
    public void A_binding_with_a_malformed_Repository_or_no_label_is_refused(string repository, string label)
    {
        Assert.NotNull(ProjectRules.BindingShapeProblem(
            new RequestProviderBinding(Connection, repository, label), new CodeProviderBinding(Connection, ["acme/demo"])));
    }

    [Fact]
    public void Repositories_are_kept_as_the_Connection_names_them()
    {
        var (requests, code, problem) = ProjectRules.AsTheConnectionsNameThem(
            new RequestProviderBinding(Connection, "ACME/Demo", " sofa "),
            new CodeProviderBinding(Connection, ["acme/DEMO"]),
            new Dictionary<Guid, IReadOnlyList<string>> { [Connection] = ["acme/demo", "acme/api"] });

        Assert.Null(problem);
        Assert.Equal(new RequestProviderBinding(Connection, "acme/demo", "sofa"), requests);
        Assert.Equal(["acme/demo"], code!.Repositories);
    }

    [Fact]
    public void A_Repository_the_Connection_does_not_reach_is_refused()
    {
        var (_, _, problem) = ProjectRules.AsTheConnectionsNameThem(
            new RequestProviderBinding(Connection, "acme/demo", "sofa"),
            new CodeProviderBinding(Connection, ["someone-else/secret"]),
            new Dictionary<Guid, IReadOnlyList<string>> { [Connection] = ["acme/demo"] });

        Assert.Equal("The Connection doesn't reach someone-else/secret.", problem);
    }

    [Theory]
    [InlineData("opus")]
    [InlineData("sonnet")]
    [InlineData("haiku")]
    [InlineData("claude-opus-5-5")]
    public void A_model_is_an_alias_or_a_full_model_name(string model)
    {
        Assert.Null(AgentModels.Problem(model));
    }

    [Theory]
    [InlineData("")]
    [InlineData("gpt-5")]
    [InlineData("opus --dangerously-skip-permissions")]
    public void Anything_else_is_no_model(string model)
    {
        Assert.NotNull(AgentModels.Problem(model));
    }
}
