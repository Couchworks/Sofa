using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sofa.Connections.Persistence;

namespace Sofa.Connections.GitHub;

public sealed class DeliverySweep(
    IServiceScopeFactory scopes,
    IOptions<GitHubOptions> github,
    TimeProvider time,
    ILogger<DeliverySweep> logger) : BackgroundService
{
    public static readonly TimeSpan OverlapForDeliveriesStillInFlight = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await SweepOnce(stopping);
            }
            catch (Exception failure) when (failure is not OperationCanceledException || !stopping.IsCancellationRequested)
            {
                logger.LogWarning(failure, "Couldn't ask GitHub for failed deliveries; trying again in {Interval}",
                    github.Value.DeliverySweepInterval);
            }

            await Task.Delay(github.Value.DeliverySweepInterval, time, stopping);
        }
    }

    public async Task SweepOnce(CancellationToken cancellation)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectionsDb>();
        var access = scope.ServiceProvider.GetRequiredService<GitHubAppAccess>();
        var gitHub = scope.ServiceProvider.GetRequiredService<GitHubApi>();

        var app = await access.FindApp(cancellation);
        if (app is null)
        {
            return;
        }

        var startedAt = time.GetUtcNow();
        var oldestGitHubKeeps = startedAt - github.Value.DeliveriesKeptByGitHub;
        var since = Later(app.DeliveriesCheckedUntil - OverlapForDeliveriesStillInFlight, oldestGitHubKeeps);

        var appJwt = await access.AppJwt(app, cancellation);
        var deliveries = await gitHub.DeliveriesSince(appJwt, since, cancellation);
        var guids = deliveries.Select(d => d.Guid).Distinct().ToList();
        var received = await db.ReceivedDeliveries
            .Where(d => guids.Contains(d.DeliveryId))
            .Select(d => d.DeliveryId)
            .ToListAsync(cancellation);

        foreach (var failed in RedeliveryPlan.FailedOnes(deliveries, received.ToHashSet()))
        {
            await gitHub.Redeliver(appJwt, failed.Id, cancellation);
            logger.LogInformation(
                "Asked GitHub to redeliver {GitHubEvent} delivery {DeliveryId}, which failed with {StatusCode}",
                failed.Action is null ? failed.Event : $"{failed.Event}.{failed.Action}", failed.Guid, failed.StatusCode);
        }

        app.DeliveriesCheckedUntil = startedAt;
        await db.ReceivedDeliveries.Where(d => d.ReceivedAt < oldestGitHubKeeps).ExecuteDeleteAsync(cancellation);
        await db.SaveChangesAsync(cancellation);
    }

    private static DateTimeOffset Later(DateTimeOffset? checkedUntil, DateTimeOffset oldestGitHubKeeps) =>
        checkedUntil is { } until && until > oldestGitHubKeeps ? until : oldestGitHubKeeps;
}
