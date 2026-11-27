using Sofa.Code.Data;
using Sofa.Contracts.Execution;

namespace Sofa.Code.Repositories;

public static class RunEndedHandler
{
    public static async Task Handle(RunEnded ended, CodeDb db, TimeProvider time, CancellationToken cancellation)
    {
        if (await db.Grants.FindAsync([ended.RunId], cancellation) is { RunEndedAt: null } grant)
        {
            grant.RunEndedAt = time.GetUtcNow();
            await db.SaveChangesAsync(cancellation);
        }
    }
}
