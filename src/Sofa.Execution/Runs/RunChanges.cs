using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Sofa.Execution.Data;
using Wolverine;

namespace Sofa.Execution.Runs;

public sealed class RunChanges(IServiceScopeFactory scopes, TimeProvider time, RunQueueSignal queue, ILogger<RunChanges> logger)
{
    private const int TriesAgainstOtherChanges = 5;

    public async Task<(Run? Run, bool Changed)> ChangeAsync(
        Guid runId, Func<Run, DateTimeOffset, bool> change, CancellationToken cancellation)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDb>();
            var run = await db.Runs.FindAsync([runId], cancellation);
            if (run is null)
            {
                return (null, false);
            }

            var now = time.GetUtcNow();
            var hadEnded = run.HasEnded;
            if (!change(run, now))
            {
                return (run, false);
            }

            var endedNow = !hadEnded && run.HasEnded;
            var retry = endedNow ? run.RetryOnItsOwn(Guid.CreateVersion7(), now) : null;
            if (retry is not null)
            {
                db.Runs.Add(retry);
            }

            try
            {
                await db.SaveChangesAsync(cancellation);
            }
            catch (DbUpdateConcurrencyException) when (attempt < TriesAgainstOtherChanges)
            {
                continue;
            }

            if (endedNow)
            {
                await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(run.Ended);
                logger.LogInformation("Run {RunId} (attempt {Attempt}) ended {State}: {Why}{Retry}",
                    run.Id, run.Attempt, run.State, run.Why ?? (run.Outcome is { } outcome ? $"with Outcome {outcome}" : $"asking Question {run.QuestionId}"),
                    retry is null ? "" : $"; retried as Run {retry.Id}");
            }

            queue.Wake();
            return (run, true);
        }
    }
}

public sealed class RunQueueSignal
{
    private readonly Channel<bool> _woken =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Wake() => _woken.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan atMost, CancellationToken cancellation)
    {
        await Task.WhenAny(_woken.Reader.WaitToReadAsync(cancellation).AsTask(), Task.Delay(atMost, cancellation));
        _woken.Reader.TryRead(out _);
    }
}
