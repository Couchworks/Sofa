using Sofa.Flow.WorkItems;

namespace Sofa.UnitTests.Flow;

public class WorkItemRulesTests
{
    [Fact]
    public void A_Work_Item_is_named_by_its_Projects_key_and_its_number()
    {
        Assert.Equal("DEMO-12", WorkItem.KeyOf("DEMO", 12));
    }

    [Theory]
    [InlineData("  Add a dark mode toggle ", "Add a dark mode toggle")]
    [InlineData("", "(untitled)")]
    [InlineData("   ", "(untitled)")]
    public void A_title_is_kept_trimmed_and_never_empty(string title, string kept)
    {
        Assert.Equal(kept, WorkItemRules.TitleOf(title));
    }

    [Fact]
    public void A_title_too_long_is_cut_short()
    {
        var kept = WorkItemRules.TitleOf(new string('a', 1000));

        Assert.Equal(WorkItemRules.LongestTitle, kept.Length);
        Assert.EndsWith("…", kept);
    }
}
