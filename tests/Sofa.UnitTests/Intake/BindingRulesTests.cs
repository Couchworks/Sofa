using Sofa.Contracts.Flow;
using Sofa.Intake.Bindings;

namespace Sofa.UnitTests.Intake;

public class BindingRulesTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Workspace = Guid.NewGuid();
    private static readonly Guid Connection = Guid.NewGuid();

    private static ProjectBound Bound(long version, string label = "sofa", bool keepsIssueState = false) =>
        new(Project, Workspace, "DEMO", new RequestProviderBinding(Connection, "acme/demo", label, keepsIssueState),
            new CodeProviderBinding(Connection, ["acme/demo"]), version);

    [Fact]
    public void The_first_binding_heard_of_is_kept()
    {
        var kept = BindingRules.Keep(null, Bound(1, keepsIssueState: true));

        Assert.True(BindingRules.IsNewerThanKept(null, Bound(1)));
        Assert.Equal((Project, Workspace, "DEMO", "acme/demo", "sofa", true, 1L, false),
            (kept.ProjectId, kept.WorkspaceId, kept.ProjectKey, kept.Repository, kept.Label, kept.KeepsIssueState, kept.Version, kept.Removed));
    }

    [Fact]
    public void A_newer_version_replaces_the_one_kept()
    {
        var kept = BindingRules.Keep(null, Bound(1));

        Assert.True(BindingRules.IsNewerThanKept(kept, Bound(2, "sofa-moved")));
        BindingRules.Keep(kept, Bound(2, "sofa-moved"));

        Assert.Equal(("sofa-moved", 2L), (kept.Label, kept.Version));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_version_arriving_late_or_twice_changes_nothing(long version)
    {
        var kept = BindingRules.Keep(null, Bound(2));

        Assert.False(BindingRules.IsNewerThanKept(kept, Bound(version)));
    }

    [Fact]
    public void A_binding_without_a_Request_Provider_removes_it_and_an_older_one_does_not_bring_it_back()
    {
        var kept = BindingRules.Keep(null, Bound(1));

        BindingRules.Keep(kept, Bound(2) with { Requests = null });

        Assert.True(kept.Removed);
        Assert.False(BindingRules.IsNewerThanKept(kept, Bound(1)));
        Assert.False(BindingRules.Marks(kept, Workspace, Connection, "acme/demo", "sofa"));
    }

    [Theory]
    [InlineData("acme/demo", "sofa")]
    [InlineData("ACME/Demo", "Sofa")]
    [InlineData("acme/demo", " sofa ")]
    public void The_bound_label_on_the_bound_Repository_marks_an_issue_for_Sofa_whatever_its_case(string repository, string label)
    {
        Assert.True(BindingRules.Marks(BindingRules.Keep(null, Bound(1)), Workspace, Connection, repository, label));
    }

    [Fact]
    public void Another_label_Repository_Connection_or_Workspace_marks_nothing()
    {
        var kept = BindingRules.Keep(null, Bound(1));

        Assert.False(BindingRules.Marks(kept, Workspace, Connection, "acme/demo", "bug"));
        Assert.False(BindingRules.Marks(kept, Workspace, Connection, "acme/web", "sofa"));
        Assert.False(BindingRules.Marks(kept, Workspace, Guid.NewGuid(), "acme/demo", "sofa"));
        Assert.False(BindingRules.Marks(kept, Guid.NewGuid(), Connection, "acme/demo", "sofa"));
    }
}
