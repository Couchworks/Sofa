using Sofa.Contracts.Execution;
using Sofa.Execution.Data;

namespace Sofa.Execution.Runners;

public static class RunnerHandler
{
    public static async Task<RunnerAnswer> Handle(
        CreateRunner command, ExecutionDb db, TimeProvider time, ILogger<CreateRunner> logger, CancellationToken cancellation)
    {
        if (await db.Runners.FindAsync([command.RunnerId], cancellation) is { } existing)
        {
            return existing.WorkspaceId == command.WorkspaceId && existing.Owner == command.Owner
                ? RunnerAnswer.Done(existing.Id)
                : RunnerAnswer.Refused(RunnerRefusal.Invalid, "Another Runner has that id.");
        }

        if (RunnerRules.ProblemWith(command) is { } problem)
        {
            return RunnerAnswer.Refused(RunnerRefusal.Invalid, problem);
        }

        var runner = new Runner
        {
            Id = command.RunnerId,
            WorkspaceId = command.WorkspaceId,
            Name = command.Name.Trim(),
            Kind = command.Kind,
            Vendor = command.Vendor,
            OwnerUserId = command.Owner.Kind == RunnerOwnerKind.User ? command.Owner.Id : null,
            OwnerTeamId = command.Owner.Kind == RunnerOwnerKind.Team ? command.Owner.Id : null,
            RunsAtOnce = command.RunsAtOnce,
            ApiKeyId = command.ApiKeyId,
            WorkerVolume = command.Kind == RunnerKind.Subscription ? RunnerRules.WorkerVolumeOf(command.RunnerId) : null,
            CreatedAt = time.GetUtcNow(),
        };
        db.Runners.Add(runner);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Added {Kind} Runner {RunnerId} for {OwnerKind} {OwnerId} in Workspace {WorkspaceId}",
            runner.Kind, runner.Id, runner.Owner.Kind, runner.Owner.Id, runner.WorkspaceId);
        return RunnerAnswer.Done(runner.Id);
    }

    public static async Task<RunnerAnswer> Handle(LimitRunsAtOnce command, ExecutionDb db, CancellationToken cancellation)
    {
        var runner = await db.Runners.FindAsync([command.RunnerId], cancellation);
        if (runner is null || runner.WorkspaceId != command.WorkspaceId)
        {
            return RunnerAnswer.Refused(RunnerRefusal.NotFound, "The Workspace has no such Runner.");
        }

        if (RunnerRules.WhyTheyMayNotChange(runner, command.ByUserId, command.ByAnAdmin) is { } notTheirs)
        {
            return RunnerAnswer.Refused(RunnerRefusal.NotTheirs, notTheirs);
        }

        if (RunnerRules.RunsAtOnceProblem(command.RunsAtOnce) is { } problem)
        {
            return RunnerAnswer.Refused(RunnerRefusal.Invalid, problem);
        }

        runner.RunsAtOnce = command.RunsAtOnce;
        await db.SaveChangesAsync(cancellation);
        return RunnerAnswer.Done(runner.Id);
    }
}
