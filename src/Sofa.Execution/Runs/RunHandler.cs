using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Data;
using Wolverine;

namespace Sofa.Execution.Runs;

public static class RunHandler
{
    public static async Task<OutgoingMessages> Handle(
        StartRun command, ExecutionDb db, TimeProvider time, RunQueueSignal queue, ILogger<StartRun> logger, CancellationToken cancellation)
    {
        if (await db.Runs.FindAsync([command.RunId], cancellation) is not null)
        {
            return [];
        }

        if (RunRules.ProblemWith(command) is { } problem)
        {
            logger.LogError("Execution can't queue Run {RunId}: {Problem}", command.RunId, problem);
            return [];
        }

        var run = Run.From(command, time.GetUtcNow());
        db.Runs.Add(run);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Run {RunId} of Agent {AgentId} on {Model} waits for a Runner of User {UserId}, at Priority {Priority}",
            run.Id, run.AgentId, run.Model, run.DelegatingUserId, run.Priority);
        queue.Wake();
        return [run.Changed];
    }

    public static async Task<RunAnswer> Handle(
        FinishRun command,
        ExecutionDb db,
        RunChanges changes,
        RunsInContainers containers,
        RecordsContributions records,
        ILogger<FinishRun> logger,
        CancellationToken cancellation)
    {
        if (await RunOfItsAgentAsync(db, command.RunId, command.AgentId, cancellation) is not { } run)
        {
            return NoSuchRun;
        }

        if (run.State != RunState.Running)
        {
            return RunAnswer.Refused(RunRefusal.NotNow, RunRules.WhyItCannotEnd(run));
        }

        if (RunRules.ProblemWith(command, run.Phase) is { } problem)
        {
            return RunAnswer.Refused(RunRefusal.Invalid, problem);
        }

        var attachments = new List<AttachmentCopied>();
        foreach (var path in command.AttachmentPaths)
        {
            switch (await containers.CopyOutAsync(run.Id, path, cancellation))
            {
                case CopiedOut.Copied copied:
                    attachments.Add(copied.Attachment);
                    break;
                case CopiedOut.Refused refused:
                    return RunAnswer.Refused(RunRefusal.Invalid, refused.Why);
            }
        }

        var outcome = PhaseOutcomes.Named(run.Phase, command.Outcome)!.Name;
        await records.KeepAsync(run, outcome, command.Contribution, attachments, cancellation);
        var (ended, finished) = await changes.ChangeAsync(run.Id, (r, now) => r.Finish(outcome, now), cancellation);
        if (!finished)
        {
            logger.LogWarning("Run {RunId}'s Contribution is kept, but the Run ended otherwise before it finished: it is {State}",
                run.Id, ended?.State);
            return RunAnswer.Refused(RunRefusal.NotNow, RunRules.WhyItCannotEnd(ended!));
        }

        containers.EndedByItsAgent(run.Id);
        return RunAnswer.Done(run.Id);
    }

    public static async Task<RunAnswer> Handle(
        EndRunWithQuestion command, ExecutionDb db, RunChanges changes, RunsInContainers containers, CancellationToken cancellation)
    {
        if (await RunOfItsAgentAsync(db, command.RunId, command.AgentId, cancellation) is null)
        {
            return NoSuchRun;
        }

        if (RunRules.ProblemWith(command) is { } problem)
        {
            return RunAnswer.Refused(RunRefusal.Invalid, problem);
        }

        var (run, asked) = await changes.ChangeAsync(command.RunId,
            (r, now) => r.EndWithQuestion(command.QuestionId, command.Question.Trim(), now), cancellation);
        if (!asked)
        {
            return RunAnswer.Refused(RunRefusal.NotNow, RunRules.WhyItCannotEnd(run!));
        }

        containers.EndedByItsAgent(command.RunId);
        return RunAnswer.Done(command.RunId);
    }

    private static Task<Run?> RunOfItsAgentAsync(ExecutionDb db, Guid runId, Guid agentId, CancellationToken cancellation) =>
        db.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId && r.AgentId == agentId, cancellation);

    private static RunAnswer NoSuchRun => RunAnswer.Refused(RunRefusal.NotFound, "Sofa has no such Run.");

    public static async Task<RunAnswer> Handle(
        StopRun command, ExecutionDb db, RunChanges changes, RunsInContainers containers, CancellationToken cancellation)
    {
        if (await db.Runs.FindAsync([command.RunId], cancellation) is not { } found || found.WorkspaceId != command.WorkspaceId)
        {
            return RunAnswer.Refused(RunRefusal.NotFound, "The Workspace has no such Run.");
        }

        var (run, stopped) = await changes.ChangeAsync(command.RunId,
            (r, now) => r.Stop("Stopped by a User.", now), cancellation);
        if (!stopped)
        {
            return RunAnswer.Refused(RunRefusal.NotNow, RunRules.WhyItCannotEnd(run!));
        }

        await containers.StopAsync(command.RunId, cancellation);
        return RunAnswer.Done(command.RunId);
    }

    public static async Task<(RunAnswer, OutgoingMessages)> Handle(
        RetryRun command, ExecutionDb db, TimeProvider time, RunQueueSignal queue, ILogger<RetryRun> logger, CancellationToken cancellation)
    {
        if (await db.Runs.FindAsync([command.RunId], cancellation) is not { } run || run.WorkspaceId != command.WorkspaceId)
        {
            return (RunAnswer.Refused(RunRefusal.NotFound, "The Workspace has no such Run."), []);
        }

        var now = time.GetUtcNow();
        if (run.RetryForAUser(Guid.CreateVersion7(), now) is not { } retry)
        {
            return (RunAnswer.Refused(RunRefusal.NotNow, run.RetriedBy is { } retriedBy
                ? $"The Run was already retried, as Run {retriedBy}."
                : $"Only a Run that failed, was lost, was refused or was stopped is retried; this one is {run.State}."), []);
        }

        db.Runs.Add(retry);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("User {UserId} retried Run {RunId} as Run {RetryId}", command.ByUserId, run.Id, retry.Id);
        queue.Wake();
        return (RunAnswer.Done(retry.Id),
        [
            new RunRetriedByAUser(retry.Id, run.Id, run.WorkspaceId, run.WorkItemId, run.Phase, command.ByUserId, now),
            run.Changed,
            retry.Changed,
        ]);
    }
}
