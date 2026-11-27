using Sofa.Contracts.Code;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;

namespace Sofa.Execution.Runs;

public sealed class Run
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid WorkItemId { get; init; }

    public string? WorkItemKey { get; init; }

    public required PhaseName Phase { get; init; }

    public required Guid AgentId { get; init; }

    public required Vendor Vendor { get; init; }

    public required string Model { get; init; }

    public required Sandbox Sandbox { get; init; }

    public required Guid DelegatingUserId { get; init; }

    public required Priority Priority { get; init; }

    public required string Brief { get; init; }

    public required int RetryLimit { get; init; }

    public required int RetriesLeft { get; init; }

    public int Attempt { get; init; } = 1;

    public Guid? RetryOf { get; init; }

    public Guid? RetriedBy { get; private set; }

    public RunState State { get; private set; } = RunState.Waiting;

    public string? Why { get; private set; }

    public RefusedAs? RefusedAs { get; private set; }

    public bool WaitsForRetry { get; private set; }

    public Guid? RunnerId { get; private set; }

    public string? Summary { get; private set; }

    public DateTimeOffset QueuedAt { get; init; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public uint Version { get; private set; }

    public string OwnBranch => OwnBranches.Of(WorkItemKey, Id);

    public bool HasEnded => State is not (RunState.Waiting or RunState.Running);

    public bool IsRetriedOnItsOwn => State is (RunState.Failed or RunState.Lost) && RetriesLeft > 0;

    public bool MayBeRetriedByAUser => State is (RunState.Failed or RunState.Lost or RunState.Refused) && RetriedBy is null;

    public static Run From(StartRun start, DateTimeOffset now) => new()
    {
        Id = start.RunId,
        WorkspaceId = start.WorkspaceId,
        ProjectId = start.ProjectId,
        WorkItemId = start.WorkItemId,
        WorkItemKey = start.WorkItemKey,
        Phase = start.Phase,
        AgentId = start.AgentId,
        Vendor = start.Vendor,
        Model = start.Model,
        Sandbox = start.Sandbox,
        DelegatingUserId = start.DelegatingUserId,
        Priority = start.Priority,
        Brief = start.Brief,
        RetryLimit = start.RetryLimit,
        RetriesLeft = start.RetryLimit,
        QueuedAt = now,
    };

    public bool WaitBecause(string why)
    {
        if (State != RunState.Waiting || Why == why)
        {
            return false;
        }

        Why = why;
        return true;
    }

    public bool StartOn(Guid runnerId, DateTimeOffset now)
    {
        if (State != RunState.Waiting)
        {
            return false;
        }

        State = RunState.Running;
        RunnerId = runnerId;
        Why = null;
        StartedAt = now;
        return true;
    }

    public bool Finish(string summary, DateTimeOffset now) => EndWhileRunning(RunState.Finished, why: null, now, summary);

    public bool Fail(string why, DateTimeOffset now) => EndWhileRunning(RunState.Failed, why, now);

    public bool Lose(string why, DateTimeOffset now) => EndWhileRunning(RunState.Lost, why, now);

    public bool Refuse(RefusedAs refusedAs, string why, DateTimeOffset now)
    {
        if (HasEnded)
        {
            return false;
        }

        State = RunState.Refused;
        RefusedAs = refusedAs;
        Why = why;
        WaitsForRetry = true;
        EndedAt = now;
        return true;
    }

    public bool Stop(string why, DateTimeOffset now)
    {
        if (HasEnded)
        {
            return false;
        }

        State = RunState.Stopped;
        Why = why;
        EndedAt = now;
        return true;
    }

    public Run? RetryOnItsOwn(Guid retryId, DateTimeOffset now) =>
        IsRetriedOnItsOwn && RetriedBy is null ? RetriedAs(retryId, RetriesLeft - 1, now) : null;

    public Run? RetryForAUser(Guid retryId, DateTimeOffset now) =>
        MayBeRetriedByAUser ? RetriedAs(retryId, RetryLimit, now) : null;

    public RunEnded Ended => new(Id, WorkspaceId, WorkItemId, State, Why, RefusedAs, RetriedBy);

    public RunDetails Details => new(
        Id, WorkspaceId, ProjectId, WorkItemId, Phase, AgentId, Model, Sandbox, DelegatingUserId, Priority, State, Why, RefusedAs,
        RunnerId, Attempt, RetriesLeft, RetryOf, RetriedBy, WaitsForRetry, Summary, QueuedAt, StartedAt, EndedAt);

    private bool EndWhileRunning(RunState state, string? why, DateTimeOffset now, string? summary = null)
    {
        if (State != RunState.Running)
        {
            return false;
        }

        State = state;
        Why = why;
        Summary = summary;
        EndedAt = now;
        WaitsForRetry = state is (RunState.Failed or RunState.Lost) && RetriesLeft == 0;
        return true;
    }

    private Run RetriedAs(Guid retryId, int retriesLeft, DateTimeOffset now)
    {
        RetriedBy = retryId;
        WaitsForRetry = false;
        return new Run
        {
            Id = retryId,
            WorkspaceId = WorkspaceId,
            ProjectId = ProjectId,
            WorkItemId = WorkItemId,
            WorkItemKey = WorkItemKey,
            Phase = Phase,
            AgentId = AgentId,
            Vendor = Vendor,
            Model = Model,
            Sandbox = Sandbox,
            DelegatingUserId = DelegatingUserId,
            Priority = Priority,
            Brief = Brief,
            RetryLimit = RetryLimit,
            RetriesLeft = retriesLeft,
            Attempt = Attempt + 1,
            RetryOf = Id,
            QueuedAt = now,
        };
    }
}
