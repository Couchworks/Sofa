using Sofa.Contracts.Flow;
using Sofa.Contracts.Records;
using Sofa.Flow.Reports;

namespace Sofa.UnitTests.Flow;

public class ReportRulesTests
{
    [Fact]
    public void A_Reports_summary_is_the_Contributions_first_paragraph_that_is_no_heading()
    {
        var summary = ReportRules.SummaryOf("## The spec\n\nThe toggle follows the system setting,\nand remembers a choice.\n\n## Acceptance criteria\n\n1. …");

        Assert.Equal("The toggle follows the system setting,\nand remembers a choice.", summary);
    }

    [Fact]
    public void A_long_summary_is_cut()
    {
        var summary = ReportRules.SummaryOf(new string('a', ReportRules.LongestSummary + 50));

        Assert.Equal(ReportRules.LongestSummary, summary.Length);
        Assert.EndsWith("…", summary);
    }

    [Fact]
    public void The_detail_is_the_whole_Contribution_naming_its_attachments()
    {
        var contribution = new ContributionDetails(Guid.NewGuid(), Guid.NewGuid(), PhaseName.Implement, Guid.NewGuid(), "implemented",
            "Built the toggle.\n", [new AttachmentDetails(Guid.NewGuid(), "toggle.png", "image/png", 812)], DateTimeOffset.UnixEpoch);

        Assert.Equal("Built the toggle.\n\nAttached in Sofa: toggle.png", ReportRules.DetailOf(contribution));
    }

    [Fact]
    public void The_last_Report_says_the_pull_request_is_merged_above_what_Implement_said() =>
        Assert.Equal("https://github.com/acme/demo/pull/7 is merged, so DEMO-1 is done.\n\nBuilt the toggle.",
            ReportRules.MergedSummary("DEMO-1", "https://github.com/acme/demo/pull/7", "Built the toggle."));
}
