using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sofa.Contracts.Execution;
using Sofa.Execution.Data;
using Sofa.Execution.Workers;

namespace Sofa.Execution.Runs;

public sealed class RunQueue(
    IServiceScopeFactory scopes,
    RunsInContainers runs,
    RunChanges changes,
    RunQueueSignal signal,
    IWorkerContainers containers,
    IOptions<WorkerOptions> options,
    ILogger<RunQueue> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        await LoseTheRunsLeftRunningAsync(stopping);
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await LookAsync(stopping);
            }
            catch (Exception failed) when (failed is not OperationCanceledException)
            {
                logger.LogError(failed, "Execution couldn't look at the Runs waiting; it looks again shortly");
            }

            await signal.WaitAsync(options.Value.QueueLooksAgainEvery, stopping);
        }
    }

    public override async Task StopAsync(CancellationToken cancellation)
    {
        await base.StopAsync(cancellation);
        await runs.StopEveryRunAsync();
    }

    private async Task LoseTheRunsLeftRunningAsync(CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var leftRunning = await scope.ServiceProvider.GetRequiredService<ExecutionDb>().Runs
            .Where(run => run.State == RunState.Running)
            .Select(run => run.Id)
            .ToListAsync(cancellation);
        foreach (var runId in leftRunning)
        {
            await containers.RemoveAsync(WorkerCommand.ContainerNameOf(runId), cancellation);
            await changes.ChangeAsync(runId,
                (run, now) => run.Lose("Execution restarted while it ran, and no longer watched its container.", now), cancellation);
        }
    }

    private async Task LookAsync(CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDb>();
        var waiting = await db.Runs.AsNoTracking().Where(run => run.State == RunState.Waiting).ToListAsync(cancellation);
        if (waiting.Count == 0)
        {
            return;
        }

        var workspaces = waiting.Select(run => run.WorkspaceId).Distinct().ToList();
        var runners = await db.Runners.AsNoTracking().Where(runner => workspaces.Contains(runner.WorkspaceId)).ToListAsync(cancellation);
        var runningOn = await db.Runs
            .Where(run => run.State == RunState.Running && run.RunnerId != null)
            .GroupBy(run => run.RunnerId!.Value)
            .Select(running => new { RunnerId = running.Key, Count = running.Count() })
            .ToDictionaryAsync(running => running.RunnerId, running => running.Count, cancellation);

        var identity = scope.ServiceProvider.GetRequiredService<IdentityTeams>();
        var teams = new Dictionary<(Guid UserId, Guid WorkspaceId), IReadOnlyCollection<Guid>>();
        foreach (var delegating in waiting.Select(run => (run.DelegatingUserId, run.WorkspaceId)).Distinct())
        {
            teams[delegating] = await identity.TeamsOfAsync(delegating.DelegatingUserId, delegating.WorkspaceId, cancellation);
        }

        var plan = RunQueueRules.Plan(waiting, runners, run => teams[(run.DelegatingUserId, run.WorkspaceId)], runningOn);
        foreach (var run in waiting.Where(run => plan.WaitingBecause.TryGetValue(run.Id, out var why) && run.Why != why))
        {
            await changes.ChangeAsync(run.Id, (r, _) => r.WaitBecause(plan.WaitingBecause[run.Id]), cancellation);
        }

        foreach (var (run, runner) in plan.ToStart)
        {
            var (started, changed) = await changes.ChangeAsync(run.Id, (r, now) => r.StartOn(runner.Id, now), cancellation);
            if (changed)
            {
                logger.LogInformation("Run {RunId} of Agent {AgentId} starts on {Kind} Runner {RunnerId}",
                    run.Id, run.AgentId, runner.Kind, runner.Id);
                runs.Launch(started!, runner);
            }
        }
    }
}
