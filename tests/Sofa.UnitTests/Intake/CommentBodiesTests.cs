using Sofa.Contracts.Flow;
using Sofa.Intake;
using Sofa.Intake.IssueState;
using Sofa.Intake.Reports;

namespace Sofa.UnitTests.Intake;

public class CommentBodiesTests
{
    private static readonly Uri InSofa = new SofaAddress(new Uri("https://sofa.example.com/")).OfWorkItem("DEMO", "DEMO-1");

    [Fact]
    public void A_Report_is_its_summary_the_detail_collapsed_beneath_and_a_link_into_Sofa()
    {
        var body = CommentBodies.Report(PhaseName.Spec, "DEMO-1", " The toggle follows the system setting. ", "## Acceptance\n\n- it toggles", InSofa);

        Assert.Equal("""
            ### Spec · DEMO-1

            The toggle follows the system setting.

            <details>
            <summary>Detail</summary>

            ## Acceptance

            - it toggles

            </details>

            [See DEMO-1 in Sofa](https://sofa.example.com/projects/DEMO/work-items/DEMO-1)
            """, body);
    }

    [Fact]
    public void A_Report_without_detail_says_so_beneath_the_summary()
    {
        Assert.Contains($"<summary>Detail</summary>\n\n{CommentBodies.NoDetail}\n\n</details>",
            CommentBodies.Report(PhaseName.Plan, "DEMO-1", "Three steps.", "  ", InSofa));
    }

    [Fact]
    public void A_Question_says_how_to_answer_it()
    {
        var body = CommentBodies.Question(PhaseName.Spec, "DEMO-1", "Should the toggle follow the system setting by default?", InSofa);

        Assert.StartsWith("### Question from Spec · DEMO-1\n\nShould the toggle follow the system setting by default?", body);
        Assert.Contains("Reply on this issue to answer it.", body);
        Assert.EndsWith("[See DEMO-1 in Sofa](https://sofa.example.com/projects/DEMO/work-items/DEMO-1)", body);
    }

    [Fact]
    public void A_Question_answered_in_Sofa_quotes_the_answer_and_says_a_reply_no_longer_answers_it()
    {
        var body = CommentBodies.QuestionAnsweredInSofa(PhaseName.Spec, "DEMO-1", "Should the toggle follow the system setting by default?",
            "Yes.\n\nAnd remember the choice.", InSofa);

        Assert.StartsWith("### Question from Spec · DEMO-1\n\nShould the toggle follow the system setting by default?", body);
        Assert.Contains("**Answered in Sofa:**\n\n> Yes.\n>\n> And remember the choice.", body);
        Assert.Contains("A reply here no longer answers it.", body);
        Assert.DoesNotContain("Reply on this issue to answer it.", body);
    }

    [Theory]
    [InlineData(PhaseName.Triage, "sofa: triage")]
    [InlineData(PhaseName.Spec, "sofa: spec")]
    [InlineData(PhaseName.Plan, "sofa: plan")]
    [InlineData(PhaseName.Implement, "sofa: implement")]
    public void Each_Phase_has_its_label(PhaseName phase, string label)
    {
        Assert.Equal(label, PhaseLabels.For(phase));
    }
}
