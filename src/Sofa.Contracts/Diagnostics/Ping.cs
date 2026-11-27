namespace Sofa.Contracts.Diagnostics;

/// <summary>
/// A test message Flow sends to Execution to prove the broker and both outboxes work.
/// Execution answers with a <see cref="Pong"/> to whoever sent it.
/// </summary>
public sealed record Ping(Guid RoundTripId);

/// <summary>Execution's answer to a <see cref="Ping"/>.</summary>
public sealed record Pong(Guid RoundTripId);
