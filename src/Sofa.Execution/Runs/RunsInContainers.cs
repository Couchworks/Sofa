using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Sofa.Contracts.Execution;
using Sofa.Execution.Data;
using Sofa.Execution.Runners;
using Sofa.Execution.Workers;
using Wolverine;

namespace Sofa.Execution.Runs;

public abstract record CopiedOut
{
    public sealed record Copied(AttachmentCopied Attachment) : CopiedOut;

    public sealed record Refused(string Why) : CopiedOut;
}

public sealed class RunsInContainers(
    IServiceScopeFactory scopes,
    IWorkerContainers containers,
    IWorkerNetwork network,
    RunChanges changes,
    IOptions<WorkerOptions> options,
    TimeProvider time,
    ILogger<RunsInContainers> logger)
{
    private readonly ConcurrentDictionary<Guid, WatchedRun> _watched = new();
    private readonly CancellationTokenSource _stopping = new();

    public void Launch(Run run, Runner runner)
    {
        var watched = new WatchedRun(CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token));
        if (!_watched.TryAdd(run.Id, watched))
        {
            return;
        }

        watched.Ran = Task.Run(() => RunAsync(run, runner, watched));
    }

    public async Task StopAsync(Guid runId, CancellationToken cancellation)
    {
        if (!_watched.TryGetValue(runId, out var watched))
        {
            return;
        }

        watched.StopAsked = true;
        if (watched.Container is { } container)
        {
            await container.StopAsync(cancellation);
        }
    }

    public void EndedByItsAgent(Guid runId)
    {
        if (_watched.TryGetValue(runId, out var watched))
        {
            watched.EndAfter(options.Value.GraceAfterTheRunEnds);
        }
    }

    public async Task<CopiedOut> CopyOutAsync(Guid runId, string path, CancellationToken cancellation)
    {
        if (!_watched.TryGetValue(runId, out var watched) || watched.Container is not { } container)
        {
            return new CopiedOut.Refused("The Run's container is no longer there to copy its attachments from.");
        }

        var inside = RunRules.InTheWorkingDirectory(path)
            ?? throw new ArgumentException($"{path} is not in the working directory.", nameof(path));
        var output = await container.RunInsideAsync(
            WorkerCommand.CopyingOutAttachment(inside, RunLimits.LargestAttachment), RunLimits.LargestAttachment + 1, cancellation);
        return output switch
        {
            { CutShort: true } or { Output.LongLength: > RunLimits.LargestAttachment } =>
                new CopiedOut.Refused($"{path} is larger than an attachment may be, {RunLimits.LargestAttachment} bytes."),
            { ExitCode: 0 } => new CopiedOut.Copied(new AttachmentCopied(Path.GetFileName(inside), output.Output)),
            { ExitCode: WorkerCommand.AttachmentOutsideTheWorkingDirectory } =>
                new CopiedOut.Refused($"{path} leads out of your working directory, {WorkerCommand.WorkingDirectory}, so Sofa doesn't attach it."),
            _ => new CopiedOut.Refused($"There is no file {path} in your working directory, {WorkerCommand.WorkingDirectory}."),
        };
    }

    public async Task StopEveryRunAsync()
    {
        await _stopping.CancelAsync();
        await Task.WhenAll(_watched.Values.Select(watched => watched.Ran));
        await network.RemoveAsync(CancellationToken.None);
    }

    private async Task RunAsync(Run run, Runner runner, WatchedRun watched)
    {
        var cancellation = watched.Cancellation.Token;
        try
        {
            if (await SecretsForAsync(run, runner, cancellation) is not { } secrets)
            {
                return;
            }

            var placement = await network.PlaceAsync(cancellation);
            var spec = WorkerCommand.For(run, runner, secrets, placement, options.Value);
            await using var container = await containers.StartAsync(spec, cancellation);
            watched.Container = container;
            if (watched.StopAsked)
            {
                await container.StopAsync(cancellation);
            }

            using var tokenKeptFresh = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var refreshing = KeepTheRepositoryTokenFreshAsync(run, secrets.Repositories.ExpiresAt, container, tokenKeptFresh.Token);
            try
            {
                await WatchAsync(run, runner, container, cancellation);
            }
            finally
            {
                await tokenKeptFresh.CancelAsync();
                await refreshing;
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            await containers.RemoveAsync(WorkerCommand.ContainerNameOf(run.Id), CancellationToken.None);
            await changes.ChangeAsync(run.Id, (r, now) => r.Lose("Execution stopped while the Run ran.", now), CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            logger.LogInformation("Run {RunId}'s container went on {Grace} after the Run ended, so it was stopped",
                run.Id, options.Value.GraceAfterTheRunEnds);
        }
        catch (Exception failed)
        {
            logger.LogError(failed, "Run {RunId} failed in Execution", run.Id);
            await changes.ChangeAsync(run.Id, (r, now) => r.Fail($"Execution couldn't run it: {failed.Message}", now), CancellationToken.None);
        }
        finally
        {
            _watched.TryRemove(run.Id, out _);
            watched.Done();
        }
    }

    private async Task<RunSecrets?> SecretsForAsync(Run run, Runner runner, CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<RunTokenClient>().RequestAsync(run.Id, run.AgentId, cancellation);
        if (issued is not RunTokenOutcome.Issued { Token: var runToken })
        {
            var reason = (issued as RunTokenOutcome.Refused)?.Reason ?? "it gave no reason";
            await changes.ChangeAsync(run.Id, (r, now) => r.Refuse(RefusedAs.AgentNotRegistered,
                $"Identity refused a Run token for its Agent {run.AgentId}: {reason}", now), cancellation);
            return null;
        }

        var repositories = await scope.ServiceProvider.GetRequiredService<CodeRepositoryTokens>().ForAsync(run, cancellation);
        if (repositories.Token is not { } repositoryToken)
        {
            await changes.ChangeAsync(run.Id, (r, now) => r.Refuse(RefusedAs.NoRepositoryToken,
                $"Code gave no token for the Project's Repositories: {repositories.Problem}", now), cancellation);
            return null;
        }

        if (runner.Kind == RunnerKind.Subscription)
        {
            return new RunSecrets(runToken, ApiKey: null, repositoryToken);
        }

        var apiKey = await scope.ServiceProvider.GetRequiredService<ConnectionsApiKeys>()
            .OpenForAsync(run.Id, run.WorkspaceId, runner.ApiKeyId!.Value, cancellation);
        if (apiKey is null)
        {
            await changes.ChangeAsync(run.Id, (r, now) => r.Refuse(RefusedAs.ApiKeyUnavailable,
                $"Connections has no API key for {runner.Name}.", now), cancellation);
            return null;
        }

        return new RunSecrets(runToken, apiKey, repositoryToken);
    }

    private async Task WatchAsync(Run run, Runner runner, IWorkerContainer container, CancellationToken cancellation)
    {
        using var longestRun = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        longestRun.CancelAfter(options.Value.LongestRun);
        await using var transcript = new TranscriptToRecords(run.Id, run.WorkspaceId, scopes);
        var signInToCheck = runner.Kind == RunnerKind.Subscription;
        var startedClaudeCode = !signInToCheck;
        try
        {
            await foreach (var line in container.ReadLinesAsync(longestRun.Token))
            {
                if (signInToCheck)
                {
                    signInToCheck = false;
                    startedClaudeCode = await CheckTheSignInAsync(run, runner, container, line, longestRun.Token);
                    continue;
                }

                transcript.Add(line);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            await container.StopAsync(CancellationToken.None);
            await transcript.DisposeAsync();
            await changes.ChangeAsync(run.Id, (r, now) => r.Fail($"It ran longer than {options.Value.LongestRun}, so it was stopped.", now), CancellationToken.None);
            return;
        }

        var exit = await container.WaitForExitAsync(cancellation);
        await transcript.DisposeAsync();
        if (!startedClaudeCode)
        {
            return;
        }

        await changes.ChangeAsync(run.Id, (r, now) => r.Fail(WhyItFailed(exit), now), cancellation);
    }

    private async Task KeepTheRepositoryTokenFreshAsync(
        Run run, DateTimeOffset expiresAt, IWorkerContainer container, CancellationToken cancellation)
    {
        try
        {
            while (true)
            {
                await Task.Delay(RepositoryTokenRefresh.DueIn(expiresAt, time.GetUtcNow(), options.Value.RefreshRepositoryTokenBefore),
                    time, cancellation);
                expiresAt = await RefreshTheRepositoryTokenAsync(run, container, cancellation)
                    ?? RepositoryTokenRefresh.TryAgainAt(time.GetUtcNow(), options.Value.RefreshRepositoryTokenBefore);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private async Task<DateTimeOffset?> RefreshTheRepositoryTokenAsync(Run run, IWorkerContainer container, CancellationToken cancellation)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var fresh = await scope.ServiceProvider.GetRequiredService<CodeRepositoryTokens>().ForAsync(run, cancellation);
            if (fresh.Token is not { } token)
            {
                logger.LogWarning("Code gave no fresh Repository token for Run {RunId}: {Problem}", run.Id, fresh.Problem);
                return null;
            }

            var replaced = await container.RunInsideAsync(WorkerCommand.ReplacingTheRepositoryToken(), RepositoryTokenRefresh.MostOutputRead,
                cancellation, new Dictionary<string, string> { [WorkerCommand.FreshRepositoryToken] = token.Token });
            if (replaced.ExitCode != 0)
            {
                logger.LogWarning("Run {RunId}'s container didn't take its fresh Repository token: exit code {ExitCode}", run.Id, replaced.ExitCode);
                return null;
            }

            logger.LogInformation("Refreshed Run {RunId}'s Repository token, until {ExpiresAt}", run.Id, token.ExpiresAt);
            return token.ExpiresAt;
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogWarning(failed, "Couldn't refresh Run {RunId}'s Repository token", run.Id);
            return null;
        }
    }

    public static string WhyItFailed(WorkerExit exit) =>
        $"Its container ended with exit code {exit.ExitCode} before the Run was ended"
        + (string.IsNullOrWhiteSpace(exit.LastErrors) ? "." : $": {exit.LastErrors.Trim()}");

    private async Task<bool> CheckTheSignInAsync(Run run, Runner runner, IWorkerContainer container, string statusLine, CancellationToken cancellation)
    {
        var seen = ClaudeAuthStatus.Read(statusLine);
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDb>();
            if (seen is not null && await db.Runners.FindAsync([runner.Id], cancellation) is { } recorded)
            {
                recorded.Saw(seen, time.GetUtcNow());
                await db.SaveChangesAsync(cancellation);
            }
        }

        if (seen is { SignedIn: true })
        {
            await container.TellAsync(WorkerCommand.GoAhead, cancellation);
            return true;
        }

        await container.TellAsync(WorkerCommand.DontStart, cancellation);
        var (_, refused) = await changes.ChangeAsync(run.Id, (r, now) => r.Refuse(RefusedAs.SignInAgain,
            $"Sign in again: {runner.Name}'s volume is not signed in to a subscription"
            + (seen is null ? " (Claude Code didn't say)" : "")
            + $". Run `sofa runner login {runner.Id}` on the box.", now), cancellation);
        if (refused)
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .PublishAsync(new SubscriptionSignedOut(run.WorkspaceId, runner.OwnerUserId!.Value, runner.Id, runner.Name, run.Id));
        }

        return false;
    }

    private sealed class WatchedRun(CancellationTokenSource cancellation)
    {
        private readonly Lock _ending = new();
        private bool _done;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public void EndAfter(TimeSpan grace)
        {
            lock (_ending)
            {
                if (!_done)
                {
                    Cancellation.CancelAfter(grace);
                }
            }
        }

        public void Done()
        {
            lock (_ending)
            {
                _done = true;
                Cancellation.Dispose();
            }
        }

        public volatile bool StopAsked;

        public IWorkerContainer? Container { get; set; }

        public Task Ran { get; set; } = Task.CompletedTask;
    }
}
