using Sofa.Contracts.Diagnostics;
using Wolverine;

namespace Sofa.Execution.Diagnostics;

/// <summary>Answers a <see cref="Ping"/> to whichever service sent it.</summary>
public static class PingHandler
{
    public static RespondToSender Handle(Ping ping) => Respond.ToSender(new Pong(ping.RoundTripId));
}
