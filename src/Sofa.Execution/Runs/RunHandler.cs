using Sofa.Contracts.Execution;
using Sofa.Execution.Data;

namespace Sofa.Execution.Runs;

public static class RunHandler
{
    public static async Task Handle(
        StartRun command, ExecutionDb db, TimeProvider time, RunQueueSignal queue, ILogger<StartRun> logger, CancellationToken cancellation)
    {
        if (await db.Runs.FindAsync([command.RunId], cancellation) is not null)
        {
            return;
        }

        if (RunRules.ProblemWith(command) is { } problem)
        {
            logger.LogError("Execution can't queue Run {RunId}: {Problem}", command.RunId, problem);
            return;
        }

        var run = Run.From(command, time.GetUtcNow());
        db.Runs.Add(run);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Run {RunId} of Agent {AgentId} on {Model} waits for a Runner of User {UserId}, at Priority {Priority}",
            run.Id, run.AgentId, run.Model, run.DelegatingUserId, run.Priority);
        queue.Wake();
    }

    public static async Task<RunAnswer> Handle(
        EndTestRun command, RunChanges changes, RunsInContainers containers, CancellationToken cancellation)
    {
        var (run, ended) = await changes.ChangeAsync(command.RunId, (r, now) => r.Finish(command.Summary.Trim(), now), cancellation);
        if (ended)
        {
            containers.EndedByItsAgent(command.RunId);
        }

        return run is null ? RunAnswer.Refused(RunRefusal.NotFound, "Sofa has no such Run.")
            : ended ? RunAnswer.Done(run.Id)
            : RunAnswer.Refused(RunRefusal.NotNow, RunRules.WhyItCannotEnd(run));
    }

    public static async Task<RunAnswer> Handle(
        StopRun command, ExecutionDb db, RunChanges changes, RunsInContainers containers, CancellationToken cancellation)
    {
        if (await db.Runs.FindAsync([command.RunId], cancellation) is not { } found || found.WorkspaceId != command.WorkspaceId)
        {
            return RunAnswer.Refused(RunRefusal.NotFound, "The Workspace has no such Run.");
        }

        var (run, stopped) = await changes.ChangeAsync(command.RunId,
            (r, now) => r.Stop($"Stopped by User {command.ByUserId}.", now), cancellation);
        if (!stopped)
        {
            return RunAnswer.Refused(RunRefusal.NotNow, RunRules.WhyItCannotEnd(run!));
        }

        await containers.StopAsync(command.RunId, cancellation);
        return RunAnswer.Done(command.RunId);
    }

    public static async Task<RunAnswer> Handle(
        RetryRun command, ExecutionDb db, TimeProvider time, RunQueueSignal queue, ILogger<RetryRun> logger, CancellationToken cancellation)
    {
        if (await db.Runs.FindAsync([command.RunId], cancellation) is not { } run || run.WorkspaceId != command.WorkspaceId)
        {
            return RunAnswer.Refused(RunRefusal.NotFound, "The Workspace has no such Run.");
        }

        if (run.RetryForAUser(Guid.CreateVersion7(), time.GetUtcNow()) is not { } retry)
        {
            return RunAnswer.Refused(RunRefusal.NotNow, run.RetriedBy is { } retriedBy
                ? $"The Run was already retried, as Run {retriedBy}."
                : $"Only a Run that failed, was lost or was refused is retried; this one is {run.State}.");
        }

        db.Runs.Add(retry);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("User {UserId} retried Run {RunId} as Run {RetryId}", command.ByUserId, run.Id, retry.Id);
        queue.Wake();
        return RunAnswer.Done(retry.Id);
    }
}
