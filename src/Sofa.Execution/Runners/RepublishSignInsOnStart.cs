using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Execution;
using Sofa.Execution.Data;
using Wolverine;

namespace Sofa.Execution.Runners;

public sealed class RepublishSignInsOnStart(IServiceScopeFactory scopes, ILogger<RepublishSignInsOnStart> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDb>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var checkedRunners = await db.Runners.AsNoTracking()
            .Where(r => r.Kind == RunnerKind.Subscription && r.SignInCheckedAt != null)
            .ToListAsync(stopping);
        foreach (var signInSeen in checkedRunners.Select(r => r.SignInSeen).OfType<SubscriptionSignInSeen>())
        {
            await bus.PublishAsync(signInSeen);
        }

        logger.LogInformation("Published the sign-ins of {Count} subscription Runners on starting", checkedRunners.Count);
    }
}
