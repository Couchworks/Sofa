using Sofa.Contracts.Flow;

namespace Sofa.Flow.WorkItems;

public sealed class WorkItemRun
{
    public required Guid RunId { get; init; }

    public required Guid WorkItemId { get; init; }

    public required PhaseName Phase { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public Guid? RetryOf { get; init; }

    public string? EndedAs { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public WorkItemRunDetails Details => new(RunId, Phase, StartedAt, RetryOf, EndedAs, EndedAt);
}

public sealed class PhaseEnd
{
    public required Guid RunId { get; init; }

    public required Guid WorkItemId { get; init; }

    public required PhaseName Phase { get; init; }

    public required string Outcome { get; init; }

    public required Gate Gate { get; init; }

    public required PhaseEndStanding Standing { get; set; }

    public required DateTimeOffset EndedAt { get; init; }

    public Guid? DecidedByUserId { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public void Decide(PhaseEndStanding standing, Guid byUserId, string? comment, DateTimeOffset at)
    {
        Standing = standing;
        DecidedByUserId = byUserId;
        Comment = comment;
        DecidedAt = at;
    }

    public PhaseEndDetails Details => new(RunId, Phase, Outcome, Gate, Standing, EndedAt, DecidedByUserId, Comment, DecidedAt);
}
