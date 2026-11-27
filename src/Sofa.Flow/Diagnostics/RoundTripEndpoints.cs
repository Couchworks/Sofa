using Sofa.Contracts.Diagnostics;
using Wolverine;

namespace Sofa.Flow.Diagnostics;

/// <summary>
/// Starts a test message to Execution and reports whether its answer came back.
/// Internal to the cluster: Flow has no external endpoint.
/// </summary>
public static class RoundTripEndpoints
{
    public static IEndpointRouteBuilder MapRoundTrips(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/diagnostics/round-trips");

        group.MapPost("/", async (RoundTrips roundTrips, IMessageBus bus) =>
        {
            var trip = roundTrips.Start();
            await bus.SendAsync(new Ping(trip.Id));
            return Results.Accepted($"/diagnostics/round-trips/{trip.Id}", trip);
        });

        group.MapGet("/{id:guid}", (Guid id, RoundTrips roundTrips) =>
            roundTrips.Find(id) is { } trip ? Results.Ok(trip) : Results.NotFound());

        return endpoints;
    }
}
