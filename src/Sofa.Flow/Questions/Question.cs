using Sofa.Contracts.Flow;

namespace Sofa.Flow.Questions;

public sealed class Question
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid WorkItemId { get; init; }

    public required Guid RunId { get; init; }

    public required PhaseName Phase { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset AskedAt { get; init; }

    public string? Answer { get; private set; }

    public Guid? AnsweredByUserId { get; private set; }

    public DateTimeOffset? AnsweredAt { get; private set; }

    public bool IsOpen => Answer is null;

    public QuestionDetails Details => new(Id, RunId, Phase, Text, AskedAt, Answer, AnsweredByUserId, AnsweredAt);

    public bool AnswerWith(string answer, Guid byUserId, DateTimeOffset at)
    {
        if (!IsOpen || string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        Answer = answer.Trim();
        AnsweredByUserId = byUserId;
        AnsweredAt = at;
        return true;
    }
}
