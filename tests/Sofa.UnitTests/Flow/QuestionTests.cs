using Sofa.Contracts.Flow;
using Sofa.Flow.Questions;

namespace Sofa.UnitTests.Flow;

public class QuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static Question Asked() => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        WorkItemId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        Phase = PhaseName.Spec,
        Text = "Follow the system?",
        AskedAt = Now,
    };

    [Fact]
    public void A_Question_takes_its_first_answer_only()
    {
        var question = Asked();
        var ada = Guid.NewGuid();

        Assert.True(question.AnswerWith(" Yes. ", ada, Now));
        Assert.False(question.AnswerWith("No.", Guid.NewGuid(), Now.AddMinutes(1)));
        Assert.Equal(("Yes.", ada, Now, false), (question.Answer, question.AnsweredByUserId, question.AnsweredAt, question.IsOpen));
    }

    [Fact]
    public void An_empty_answer_leaves_it_open()
    {
        var question = Asked();

        Assert.False(question.AnswerWith("  ", Guid.NewGuid(), Now));
        Assert.True(question.IsOpen);
        Assert.Null(question.Details.Answer);
    }
}
