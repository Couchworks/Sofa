using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Records;
using Sofa.Records.Contributions;

namespace Sofa.UnitTests.Records;

public class ContributionRulesTests
{
    private static ContributionToKeep Contribution(string markdown = "The spec.", string outcome = "specified") =>
        new(Guid.NewGuid(), PhaseName.Spec, Guid.NewGuid(), outcome, markdown);

    [Fact]
    public void A_Contribution_has_its_Markdown_its_Outcome_and_at_most_its_limit_of_attachments()
    {
        Assert.Null(ContributionRules.ProblemWith(Contribution(), RunLimits.MostAttachments));
        Assert.NotNull(ContributionRules.ProblemWith(Contribution(markdown: ""), 0));
        Assert.NotNull(ContributionRules.ProblemWith(Contribution(outcome: " "), 0));
        Assert.NotNull(ContributionRules.ProblemWith(Contribution(), RunLimits.MostAttachments + 1));
        Assert.NotNull(ContributionRules.ProblemWithAttachment(RunLimits.LargestAttachment + 1));
        Assert.Null(ContributionRules.ProblemWithAttachment(RunLimits.LargestAttachment));
    }

    [Theory]
    [InlineData("screenshot.png", "screenshot.png")]
    [InlineData("shots/dark.png", "dark.png")]
    [InlineData("..\\..\\evil.png", "evil.png")]
    [InlineData("say\"hi\".txt", "sayhi.txt")]
    [InlineData("..", "attachment")]
    [InlineData(null, "attachment")]
    public void An_attachment_is_named_by_its_file_name_alone(string? fileName, string name)
    {
        Assert.Equal(name, ContributionRules.NameOf(fileName));
    }

    [Theory]
    [InlineData("shot.PNG", "image/png", true)]
    [InlineData("shot.jpeg", "image/jpeg", true)]
    [InlineData("notes.md", "text/markdown", false)]
    [InlineData("page.html", "application/octet-stream", false)]
    [InlineData("drawing.svg", "application/octet-stream", false)]
    public void Only_images_are_shown_in_the_page_and_anything_unknown_is_downloaded(string name, string contentType, bool shown)
    {
        Assert.Equal(contentType, ContributionRules.ContentTypeOf(name));
        Assert.Equal(shown, ContributionRules.IsShownInThePage(contentType));
    }
}
