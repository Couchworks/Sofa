namespace Sofa.Contracts.Diagnostics;

public sealed record Ping(Guid RoundTripId);

public sealed record Pong(Guid RoundTripId);
