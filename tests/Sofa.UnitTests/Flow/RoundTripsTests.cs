using Sofa.Flow.Diagnostics;

namespace Sofa.UnitTests.Flow;

public class RoundTripsTests
{
    private readonly FixedTime _time = new(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_started_round_trip_is_unanswered()
    {
        var roundTrips = new RoundTrips(_time);

        var trip = roundTrips.Start();

        Assert.False(roundTrips.Find(trip.Id)!.Answered);
        Assert.Equal(_time.Now, trip.StartedAt);
    }

    [Fact]
    public void An_answer_marks_its_round_trip_answered_when_it_came()
    {
        var roundTrips = new RoundTrips(_time);
        var trip = roundTrips.Start();
        _time.Now = _time.Now.AddSeconds(2);

        Assert.True(roundTrips.Answer(trip.Id));

        Assert.Equal(_time.Now, roundTrips.Find(trip.Id)!.AnsweredAt);
    }

    [Fact]
    public void A_second_answer_keeps_the_first_answers_time()
    {
        var roundTrips = new RoundTrips(_time);
        var trip = roundTrips.Start();
        roundTrips.Answer(trip.Id);
        var firstAnswer = _time.Now;
        _time.Now = _time.Now.AddSeconds(5);

        roundTrips.Answer(trip.Id);

        Assert.Equal(firstAnswer, roundTrips.Find(trip.Id)!.AnsweredAt);
    }

    [Fact]
    public void An_answer_to_an_unknown_round_trip_is_ignored()
    {
        var roundTrips = new RoundTrips(_time);

        Assert.False(roundTrips.Answer(Guid.NewGuid()));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
