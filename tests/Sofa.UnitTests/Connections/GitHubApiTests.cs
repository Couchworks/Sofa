using System.Net;
using System.Text.Json;
using Sofa.Connections.GitHub;

namespace Sofa.UnitTests.Connections;

public class GitHubApiTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pages_through_deliveries_until_it_reaches_those_already_checked()
    {
        var pages = new Pages(
            ("app/hook/deliveries?per_page=100", [Noon.AddMinutes(30), Noon.AddMinutes(20)], "app/hook/deliveries?per_page=100&cursor=b"),
            ("app/hook/deliveries?per_page=100&cursor=b", [Noon.AddMinutes(10), Noon.AddMinutes(-10)], "app/hook/deliveries?per_page=100&cursor=c"),
            ("app/hook/deliveries?per_page=100&cursor=c", [Noon.AddMinutes(-20)], null));
        var api = new GitHubApi(new HttpClient(pages) { BaseAddress = new Uri("https://api.github.test/") });

        var deliveries = await api.DeliveriesSince("jwt", Noon, TestContext.Current.CancellationToken);

        Assert.Equal(3, deliveries.Count);
        Assert.Equal(2, pages.Asked);
    }

    [Fact]
    public async Task Says_what_GitHub_refused()
    {
        var api = new GitHubApi(new HttpClient(new Refusing()) { BaseAddress = new Uri("https://api.github.test/") });

        var refused = await Assert.ThrowsAsync<GitHubRefused>(() =>
            api.ConvertManifest("code", TestContext.Current.CancellationToken));

        Assert.Equal(404, refused.Status);
    }

    private sealed class Pages(params (string Path, DateTimeOffset[] DeliveredAt, string? Next)[] pages) : HttpMessageHandler
    {
        public int Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked++;
            var page = pages.Single(p => request.RequestUri!.PathAndQuery == "/" + p.Path);
            var deliveries = page.DeliveredAt.Select((at, i) => new
            {
                id = Asked * 10 + i, guid = Guid.NewGuid(), delivered_at = at, redelivery = false,
                status_code = 500, @event = "issues", action = "labeled",
            });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(deliveries)),
                RequestMessage = request,
            };
            if (page.Next is not null)
            {
                response.Headers.Add("Link", $"<https://api.github.test/{page.Next}>; rel=\"next\"");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"Not Found"}"""),
                RequestMessage = request,
            });
    }
}
