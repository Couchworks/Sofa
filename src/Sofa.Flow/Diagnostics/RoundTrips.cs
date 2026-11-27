using System.Collections.Concurrent;

namespace Sofa.Flow.Diagnostics;

/// <summary>
/// The test round trips Flow has started and whether Execution has answered them.
/// Held in memory: it only proves the broker works, and holds nothing worth keeping.
/// </summary>
public sealed class RoundTrips(TimeProvider time)
{
    private readonly ConcurrentDictionary<Guid, RoundTrip> _trips = new();

    public RoundTrip Start()
    {
        var trip = new RoundTrip(Guid.NewGuid(), time.GetUtcNow(), AnsweredAt: null);
        _trips[trip.Id] = trip;
        return trip;
    }

    /// <summary>Marks a round trip answered; an answer to one Flow doesn't know is ignored.</summary>
    public bool Answer(Guid id) =>
        _trips.TryGetValue(id, out var trip)
        && _trips.TryUpdate(id, trip with { AnsweredAt = trip.AnsweredAt ?? time.GetUtcNow() }, trip);

    public RoundTrip? Find(Guid id) => _trips.GetValueOrDefault(id);
}

public sealed record RoundTrip(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? AnsweredAt)
{
    public bool Answered => AnsweredAt is not null;
}
