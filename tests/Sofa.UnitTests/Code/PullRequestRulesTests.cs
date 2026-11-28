using Sofa.Code.PullRequests;
using Sofa.Contracts.Code;

namespace Sofa.UnitTests.Code;

public class PullRequestRulesTests
{
    [Theory]
    [InlineData("DEMO-1: Add a dark mode toggle", "feature/x", "DEMO-1")]
    [InlineData("Add a dark mode toggle (demo-12)", "feature/x", "DEMO-12")]
    [InlineData("Add a dark mode toggle", "sofa/DEMO-3", "DEMO-3")]
    [InlineData("DEMO-4 and DEMO-5", "sofa/DEMO-6", "DEMO-4")]
    [InlineData("Add a toggle", "feature/x", null)]
    [InlineData("XDEMO-1 is another Project's", "main", null)]
    [InlineData("DEMO-10x", "main", null)]
    [InlineData("DEMO-0", "main", null)]
    public void A_pull_request_names_the_Work_Item_whose_key_is_in_its_title_or_else_its_branch(
        string title, string branch, string? key)
    {
        Assert.Equal(key, PullRequestRules.WorkItemKeyIn("DEMO", title, branch));
    }

    [Theory]
    [InlineData(true, false, PullRequestState.Open)]
    [InlineData(false, true, PullRequestState.Merged)]
    [InlineData(false, false, PullRequestState.ClosedUnmerged)]
    public void A_pull_request_is_open_merged_or_closed_unmerged(bool open, bool merged, PullRequestState state)
    {
        Assert.Equal(state, PullRequestRules.StateOf(open, merged));
    }

    [Fact]
    public void Only_a_change_of_state_becomes_an_event_so_a_delivery_heard_twice_sends_one()
    {
        var pull = Kept(PullRequestState.Merged);

        Assert.IsType<PullRequestMerged>(PullRequestRules.EventForChange(PullRequestState.Open, pull));
        Assert.IsType<PullRequestMerged>(PullRequestRules.EventForChange(null, pull));
        Assert.Null(PullRequestRules.EventForChange(PullRequestState.Merged, pull));
        Assert.IsType<PullRequestOpened>(PullRequestRules.EventForChange(null, Kept(PullRequestState.Open)));
        Assert.IsType<PullRequestOpened>(PullRequestRules.EventForChange(PullRequestState.ClosedUnmerged, Kept(PullRequestState.Open)));
        Assert.IsType<PullRequestClosedUnmerged>(PullRequestRules.EventForChange(PullRequestState.Open, Kept(PullRequestState.ClosedUnmerged)));
    }

    [Fact]
    public void A_pull_request_reopened_is_no_longer_closed()
    {
        var pull = Kept(PullRequestState.ClosedUnmerged);
        pull.ClosedAt = DateTimeOffset.UnixEpoch;

        PullRequestRules.Apply(pull, new SeenPullRequest("DEMO-1", "u", "sofa/DEMO-1", "main", PullRequestState.Open, DateTimeOffset.UnixEpoch), "DEMO-1");

        Assert.Equal((PullRequestState.Open, (DateTimeOffset?)null), (pull.State, pull.ClosedAt));
    }

    private static KeptPullRequest Kept(PullRequestState state) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        ProjectKey = "DEMO",
        ConnectionId = Guid.NewGuid(),
        Repository = "acme/demo",
        Number = 7,
        WorkItemKey = "DEMO-1",
        Title = "DEMO-1: Add a dark mode toggle",
        Url = "https://github.com/acme/demo/pull/7",
        HeadBranch = "sofa/DEMO-1",
        BaseBranch = "main",
        State = state,
        OpenedAt = DateTimeOffset.UnixEpoch,
    };
}
