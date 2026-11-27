namespace Sofa.Connections.GitHub;

public static class RedeliveryPlan
{
    public static IReadOnlyList<HookDelivery> FailedOnes(
        IEnumerable<HookDelivery> deliveries, IReadOnlySet<Guid> alreadyReceived) =>
        deliveries
            .GroupBy(d => d.Guid)
            .Where(attempts => !alreadyReceived.Contains(attempts.Key))
            .Where(attempts => !attempts.Any(Succeeded))
            .Where(attempts => !attempts.Any(d => d.Redelivery))
            .Select(attempts => attempts.OrderByDescending(d => d.DeliveredAt).First())
            .OrderBy(d => d.DeliveredAt)
            .ToList();

    private static bool Succeeded(HookDelivery delivery) => delivery.StatusCode is >= 200 and < 300;
}
