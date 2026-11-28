using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Runs;

namespace Sofa.UnitTests.Execution;

public class RunRulesTests
{
    private static FinishRun Finishing(string outcome = "specified", string contribution = "The spec.", params string[] attachments) =>
        new(Guid.NewGuid(), Guid.NewGuid(), outcome, contribution, attachments);

    [Fact]
    public void A_Run_finishes_with_an_Outcome_of_its_Phase_and_a_Contribution()
    {
        Assert.Null(RunRules.ProblemWith(Finishing(), PhaseName.Spec));
        Assert.Contains("`small`, `full`", RunRules.ProblemWith(Finishing("specified"), PhaseName.Triage));
        Assert.NotNull(RunRules.ProblemWith(Finishing(contribution: " "), PhaseName.Spec));
        Assert.NotNull(RunRules.ProblemWith(Finishing(contribution: new string('x', RunLimits.LongestContribution + 1)), PhaseName.Spec));
    }

    [Fact]
    public void A_Contribution_has_at_most_its_limit_of_attachments()
    {
        var tooMany = Enumerable.Range(0, RunLimits.MostAttachments + 1).Select(n => $"shot-{n}.png").ToArray();

        Assert.Contains("at most", RunRules.ProblemWith(Finishing(attachments: tooMany), PhaseName.Spec));
        Assert.Null(RunRules.ProblemWith(Finishing(attachments: tooMany[..RunLimits.MostAttachments]), PhaseName.Spec));
    }

    [Theory]
    [InlineData("screenshot.png", "/work/screenshot.png")]
    [InlineData("./shots/../screenshot.png", "/work/screenshot.png")]
    [InlineData("/work/shots/dark.png", "/work/shots/dark.png")]
    public void An_attachment_is_a_file_in_the_working_directory(string path, string inside)
    {
        Assert.Equal(inside, RunRules.InTheWorkingDirectory(path));
        Assert.Null(RunRules.ProblemWithAttachmentPath(path));
    }

    [Theory]
    [InlineData("/claude/.credentials.json")]
    [InlineData("../claude/.credentials.json")]
    [InlineData("/proc/self/environ")]
    [InlineData("/work")]
    [InlineData("/workspace/x.png")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("")]
    [InlineData("shot\n.png")]
    public void Nothing_outside_the_working_directory_is_attached(string path)
    {
        Assert.NotNull(RunRules.ProblemWithAttachmentPath(path));
    }

    [Fact]
    public void A_Question_has_its_text_and_an_id()
    {
        Assert.Null(RunRules.ProblemWith(new EndRunWithQuestion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Follow the system?")));
        Assert.NotNull(RunRules.ProblemWith(new EndRunWithQuestion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " ")));
        Assert.NotNull(RunRules.ProblemWith(new EndRunWithQuestion(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Follow the system?")));
        Assert.NotNull(RunRules.ProblemWith(new EndRunWithQuestion(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('?', RunLimits.LongestQuestion + 1))));
    }
}
