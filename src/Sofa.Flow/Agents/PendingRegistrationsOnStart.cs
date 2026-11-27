using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Sofa.Flow.Projects;
using Wolverine;

namespace Sofa.Flow.Agents;

public sealed class PendingRegistrationsOnStart(IServiceScopeFactory scopes, ILogger<PendingRegistrationsOnStart> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDb>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pending = await db.Agents.AsNoTracking()
            .Where(a => a.Registration == AgentRegistrationState.Pending)
            .ToListAsync(stopping);
        foreach (var registration in ProjectHandler.RegistrationsOf(pending))
        {
            await bus.SendAsync(registration);
        }

        if (pending.Count > 0)
        {
            logger.LogInformation("Asked Identity again to register {Count} Agents it hasn't answered for", pending.Count);
        }
    }
}
