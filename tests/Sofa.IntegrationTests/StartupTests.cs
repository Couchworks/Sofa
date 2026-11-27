using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.ApplicationModel;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class StartupTests(SofaApp sofa)
{
    public static TheoryData<string> Services => new(SofaServices.All);

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Every_service_reports_healthy(string service)
    {
        using var client = sofa.CreateHttpClient(service);

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_App_Gateway_answers_its_status()
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        var status = await gateway.GetFromJsonAsync<Status>("/api/status", TestContext.Current.CancellationToken);

        Assert.Equal("Sofa", status!.Name);
    }

    [Fact]
    public async Task The_web_client_loads_through_the_App_Gateway()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var cancellation = timeout.Token;
        var web = await sofa.App.ResourceNotifications.WaitForResourceAsync(
            "web", e => e.Snapshot.State?.Text is not null && WebHasSettled(e.Snapshot.State.Text), cancellation);
        Assert.Equal(KnownResourceStates.Running, web.Snapshot.State?.Text);
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        var page = await gateway.GetStringAsync("/", cancellation);

        Assert.Contains("<div id=\"root\">", page);
        Assert.Contains("<title>Sofa</title>", page);
    }

    private static bool WebHasSettled(string state) =>
        state == KnownResourceStates.Running || KnownResourceStates.TerminalStates.Contains(state);

    private sealed record Status(string Name);
}
