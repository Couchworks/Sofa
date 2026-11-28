using Sofa.Contracts.Flow;
using Sofa.Intake.Questions;

namespace Sofa.UnitTests.Intake;

public class AnswerRulesTests
{
    private static QuestionComment AskedIn(long commentId, long? answeredBy = null) => new()
    {
        QuestionId = Guid.NewGuid(),
        RequestId = Guid.NewGuid(),
        Phase = PhaseName.Spec,
        WorkItemKey = "DEMO-1",
        CommentId = commentId,
        AskedAt = DateTimeOffset.UnixEpoch,
        AnsweredByCommentId = answeredBy,
    };

    [Fact]
    public void A_comment_after_an_open_Question_answers_it()
    {
        var question = AskedIn(100);

        Assert.Equal(new Answering(question, AlreadyAnsweredByThisComment: false), AnswerRules.WhatTheCommentAnswers([question], 101));
    }

    [Fact]
    public void A_comment_answers_the_newest_open_Question_asked_before_it()
    {
        var older = AskedIn(100);
        var newer = AskedIn(105);
        var later = AskedIn(120);

        Assert.Same(newer, AnswerRules.WhatTheCommentAnswers([older, newer, later], 110)!.Question);
    }

    [Fact]
    public void A_comment_after_a_Question_answered_in_Sofa_answers_nothing()
    {
        var question = AskedIn(100);
        question.AnsweredInSofa = true;

        Assert.Null(AnswerRules.WhatTheCommentAnswers([question], 101));
    }

    [Fact]
    public void A_comment_once_every_Question_is_answered_answers_nothing()
    {
        Assert.Null(AnswerRules.WhatTheCommentAnswers([AskedIn(100, answeredBy: 101)], 102));
    }

    [Fact]
    public void A_comment_before_any_Question_answers_nothing()
    {
        Assert.Null(AnswerRules.WhatTheCommentAnswers([AskedIn(100)], 99));
    }

    [Fact]
    public void The_same_comment_delivered_again_is_recognised_as_the_answer_it_gave()
    {
        var question = AskedIn(100, answeredBy: 101);

        Assert.Equal(new Answering(question, AlreadyAnsweredByThisComment: true), AnswerRules.WhatTheCommentAnswers([question], 101));
    }

    [Fact]
    public void Sofas_own_Question_comment_answers_nothing()
    {
        Assert.Null(AnswerRules.WhatTheCommentAnswers([AskedIn(100)], 100));
    }
}
