using Sofa.Contracts.Code;
using Sofa.Contracts.Execution;

namespace Sofa.Execution.Runs;

public static class PushCaughtHandler
{
    public static async Task Handle(
        PushOutsideItsSandboxCaught caught,
        RunChanges changes,
        RunsInContainers containers,
        ILogger<PushOutsideItsSandboxCaught> logger,
        CancellationToken cancellation)
    {
        foreach (var runId in caught.RunsThatCouldHavePushed)
        {
            var (_, ended) = await changes.ChangeAsync(runId, (r, now) => r.WorkspaceId == caught.WorkspaceId && r.Refuse(RefusedAs.LeftItsSandbox,
                $"It may have left its Sandbox: {caught.Why}"
                + (caught.Undone ? " Code put the branch back." : " Code couldn't put the branch back."), now), cancellation);
            if (ended)
            {
                await containers.StopAsync(runId, cancellation);
                logger.LogWarning("Run {RunId} was ended, as it may have pushed {Ref} in {Repository}", runId, caught.Ref, caught.Repository);
            }
        }
    }
}
