namespace Sofa.Intake.Questions;

public sealed record Answering(QuestionComment Question, bool AlreadyAnsweredByThisComment);

public static class AnswerRules
{
    public static Answering? WhatTheCommentAnswers(IReadOnlyCollection<QuestionComment> questionsOnTheIssue, long commentId)
    {
        if (questionsOnTheIssue.Any(q => q.CommentId == commentId))
        {
            return null;
        }

        if (questionsOnTheIssue.FirstOrDefault(q => q.AnsweredByCommentId == commentId) is { } answered)
        {
            return new Answering(answered, AlreadyAnsweredByThisComment: true);
        }

        var newestOpenAskedBefore = questionsOnTheIssue
            .Where(q => q.IsOpen && q.CommentId < commentId)
            .MaxBy(q => q.CommentId);
        return newestOpenAskedBefore is null ? null : new Answering(newestOpenAskedBefore, AlreadyAnsweredByThisComment: false);
    }
}
