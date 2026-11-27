using System.Net;
using System.Net.Http.Json;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class RoundTripTests(SofaApp sofa)
{
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_ping_from_Flow_reaches_Execution_and_its_pong_comes_back()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);

        using var started = await flow.PostAsync("/diagnostics/round-trips", content: null, cancellation);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var trip = await started.Content.ReadFromJsonAsync<RoundTrip>(cancellation);

        var answered = await WaitForAnswer(flow, trip!.Id, cancellation);

        Assert.True(answered.Answered, "Execution's pong never came back to Flow.");
    }

    private static async Task<RoundTrip> WaitForAnswer(HttpClient flow, Guid id, CancellationToken cancellation)
    {
        var deadline = DateTimeOffset.UtcNow + AnswerTimeout;
        while (true)
        {
            var trip = await flow.GetFromJsonAsync<RoundTrip>($"/diagnostics/round-trips/{id}", cancellation);
            if (trip!.Answered || DateTimeOffset.UtcNow > deadline)
            {
                return trip;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellation);
        }
    }

    private sealed record RoundTrip(Guid Id, bool Answered);
}
