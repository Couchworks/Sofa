using Sofa.Contracts.Execution;
using Sofa.Execution.Runners;

namespace Sofa.Execution.Runs;

public abstract record RunnerChoice
{
    public sealed record Spend(Runner Runner) : RunnerChoice;

    public sealed record Wait(string Why) : RunnerChoice;
}

public sealed record RunToStart(Run Run, Runner Runner);

public sealed record QueuePlan(IReadOnlyList<RunToStart> ToStart, IReadOnlyDictionary<Guid, string> WaitingBecause);

public static class RunQueueRules
{
    public static IEnumerable<Run> InTurn(IEnumerable<Run> waiting) =>
        waiting.OrderBy(run => run.Priority).ThenBy(run => run.QueuedAt).ThenBy(run => run.Id);

    public static IReadOnlyList<Runner> RunnersItMaySpend(
        Run run, IEnumerable<Runner> runners, IReadOnlyCollection<Guid> delegatingUsersTeams)
    {
        var ofTheVendor = runners.Where(runner => runner.WorkspaceId == run.WorkspaceId && runner.Vendor == run.Vendor).ToList();
        var personal = ofTheVendor
            .Where(runner => runner.OwnerUserId == run.DelegatingUserId)
            .OrderBy(runner => runner.Kind != RunnerKind.Subscription)
            .ThenBy(runner => runner.CreatedAt)
            .ToList();
        return personal.Count > 0
            ? personal
            : ofTheVendor
                .Where(runner => runner.OwnerTeamId is { } teamId && delegatingUsersTeams.Contains(teamId))
                .OrderBy(runner => runner.CreatedAt)
                .ToList();
    }

    public static RunnerChoice ChooseFor(
        Run run,
        IEnumerable<Runner> runners,
        IReadOnlyCollection<Guid> delegatingUsersTeams,
        IReadOnlyDictionary<Guid, int> runningOn)
    {
        var maySpend = RunnersItMaySpend(run, runners, delegatingUsersTeams);
        if (maySpend.Count == 0)
        {
            return new RunnerChoice.Wait(
                $"Its delegating User has no {run.Vendor} Runner of their own, and no Team of theirs has one either.");
        }

        foreach (var runner in maySpend)
        {
            if (runningOn.GetValueOrDefault(runner.Id) < runner.RunsAtOnce)
            {
                return new RunnerChoice.Spend(runner);
            }
        }

        var atTheirLimit = string.Join(", ", maySpend.Select(runner => $"{runner.Name} runs {runner.RunsAtOnce} at once"));
        return new RunnerChoice.Wait($"Every Runner it may spend is at its limit: {atTheirLimit}.");
    }

    public static QueuePlan Plan(
        IEnumerable<Run> waiting,
        IReadOnlyCollection<Runner> runners,
        Func<Run, IReadOnlyCollection<Guid>> teamsOfTheDelegatingUser,
        IReadOnlyDictionary<Guid, int> runningOn)
    {
        var running = new Dictionary<Guid, int>(runningOn);
        var toStart = new List<RunToStart>();
        var waitingBecause = new Dictionary<Guid, string>();
        foreach (var run in InTurn(waiting))
        {
            switch (ChooseFor(run, runners, teamsOfTheDelegatingUser(run), running))
            {
                case RunnerChoice.Spend spend:
                    toStart.Add(new RunToStart(run, spend.Runner));
                    running[spend.Runner.Id] = running.GetValueOrDefault(spend.Runner.Id) + 1;
                    break;
                case RunnerChoice.Wait wait:
                    waitingBecause[run.Id] = wait.Why;
                    break;
            }
        }

        return new QueuePlan(toStart, waitingBecause);
    }
}
