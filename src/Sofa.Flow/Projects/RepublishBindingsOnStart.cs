using Microsoft.EntityFrameworkCore;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.Projects;

public sealed class RepublishBindingsOnStart(IServiceScopeFactory scopes, ILogger<RepublishBindingsOnStart> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDb>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var projects = await db.Projects.AsNoTracking().ToListAsync(stopping);
        foreach (var project in projects)
        {
            await bus.PublishAsync(project.Bound);
        }

        logger.LogInformation("Published the bindings of {Count} Projects on starting", projects.Count);
    }
}
