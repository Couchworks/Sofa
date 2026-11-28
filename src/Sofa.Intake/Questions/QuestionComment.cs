using Sofa.Contracts.Flow;

namespace Sofa.Intake.Questions;

public sealed class QuestionComment
{
    public required Guid QuestionId { get; init; }

    public required Guid RequestId { get; init; }

    public required PhaseName Phase { get; init; }

    public required string WorkItemKey { get; init; }

    public required long CommentId { get; init; }

    public required DateTimeOffset AskedAt { get; init; }

    public long? AnsweredByCommentId { get; set; }

    public Guid? AnsweredByUserId { get; set; }

    public string? Answer { get; set; }

    public DateTimeOffset? AnsweredAt { get; set; }

    public bool AnsweredInSofa { get; set; }

    public bool IsOpen => AnsweredByCommentId is null && !AnsweredInSofa;
}
