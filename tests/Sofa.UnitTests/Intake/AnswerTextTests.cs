using Sofa.Intake.Questions;

namespace Sofa.UnitTests.Intake;

public class AnswerTextTests
{
    [Theory]
    [InlineData("Yes, follow the system setting.", "Yes, follow the system setting.")]
    [InlineData("  Yes.  ", "Yes.")]
    [InlineData("@sofa-cash-foods yes follow the system setting", "yes follow the system setting")]
    [InlineData("@sofa-acme[bot] Yes.", "Yes.")]
    [InlineData("@sofa-acme, @ada-gh: Yes.", "Yes.")]
    public void A_leading_mention_is_no_part_of_the_answer(string comment, string answer)
    {
        Assert.Equal(answer, AnswerText.Of(comment));
    }

    [Fact]
    public void A_quote_of_the_Question_above_the_reply_is_no_part_of_the_answer()
    {
        var comment = "> ### Question from Spec · DEMO-1\r\n>\r\n> Should the toggle follow the system setting?\r\n\r\nYes, by default.\r\nAnd remember a choice.";

        Assert.Equal("Yes, by default.\nAnd remember a choice.", AnswerText.Of(comment));
    }

    [Fact]
    public void A_quote_then_a_mention_leaves_only_the_answer()
    {
        Assert.Equal("Yes.", AnswerText.Of("> Should it?\n\n@sofa-acme Yes."));
    }

    [Fact]
    public void Mentions_and_quotes_after_the_answer_starts_are_kept()
    {
        Assert.Equal("Yes, as @ada-gh said:\n> follow it", AnswerText.Of("Yes, as @ada-gh said:\n> follow it"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@sofa-acme")]
    [InlineData("> Should it?\n\n@sofa-acme[bot]")]
    public void A_comment_that_only_quotes_or_mentions_answers_nothing(string comment)
    {
        Assert.Null(AnswerText.Of(comment));
    }
}
