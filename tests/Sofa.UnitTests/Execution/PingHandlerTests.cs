using Sofa.Contracts.Diagnostics;
using Sofa.Execution.Diagnostics;

namespace Sofa.UnitTests.Execution;

public class PingHandlerTests
{
    [Fact]
    public void Answers_a_ping_with_a_pong_for_the_same_round_trip_to_its_sender()
    {
        var ping = new Ping(Guid.NewGuid());

        var reply = PingHandler.Handle(ping);

        Assert.Equal(new Pong(ping.RoundTripId), reply.Message);
    }
}
