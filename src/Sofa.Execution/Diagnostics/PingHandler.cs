using Sofa.Contracts.Diagnostics;
using Wolverine;

namespace Sofa.Execution.Diagnostics;

public static class PingHandler
{
    public static RespondToSender Handle(Ping ping) => Respond.ToSender(new Pong(ping.RoundTripId));
}
