using Sofa.Connections.GitHub;

namespace Sofa.UnitTests.Connections;

public class RedeliveryPlanTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<Guid> NoneReceived = [];

    [Fact]
    public void Redelivers_a_delivery_that_failed()
    {
        var failed = Delivery(1, Guid.NewGuid(), status: 502);

        Assert.Equal([failed], RedeliveryPlan.FailedOnes([failed], NoneReceived));
    }

    [Fact]
    public void Leaves_a_delivery_that_succeeded() =>
        Assert.Empty(RedeliveryPlan.FailedOnes([Delivery(1, Guid.NewGuid(), status: 202)], NoneReceived));

    [Fact]
    public void Leaves_a_delivery_Sofa_received_although_GitHub_saw_it_fail()
    {
        var guid = Guid.NewGuid();

        Assert.Empty(RedeliveryPlan.FailedOnes([Delivery(1, guid, status: 0)], new HashSet<Guid> { guid }));
    }

    [Fact]
    public void Leaves_a_failed_delivery_that_a_later_attempt_delivered()
    {
        var guid = Guid.NewGuid();

        Assert.Empty(RedeliveryPlan.FailedOnes(
            [Delivery(1, guid, status: 500), Delivery(2, guid, status: 200, redelivery: true, minutesLater: 5)], NoneReceived));
    }

    [Fact]
    public void Redelivers_each_failure_once_and_never_retries_a_failed_redelivery()
    {
        var guid = Guid.NewGuid();

        Assert.Empty(RedeliveryPlan.FailedOnes(
            [Delivery(1, guid, status: 500), Delivery(2, guid, status: 500, redelivery: true, minutesLater: 5)], NoneReceived));
    }

    [Fact]
    public void Redelivers_the_oldest_failures_first()
    {
        var later = Delivery(2, Guid.NewGuid(), status: 500, minutesLater: 10);
        var earlier = Delivery(1, Guid.NewGuid(), status: 500);

        Assert.Equal([earlier, later], RedeliveryPlan.FailedOnes([later, earlier], NoneReceived));
    }

    private static HookDelivery Delivery(
        long id, Guid guid, int status, bool redelivery = false, int minutesLater = 0) =>
        new(id, guid, Noon.AddMinutes(minutesLater), redelivery, status, "issues", "labeled");
}
