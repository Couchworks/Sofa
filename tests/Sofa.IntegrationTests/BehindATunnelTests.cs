using System.Net.Http.Json;
using System.Text.Json;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class BehindATunnelTests(SofaApp sofa)
{
    private const string TunnelHost = "sofa-tunnel.example.dev";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Identity_names_its_endpoints_with_the_scheme_the_tunnel_was_reached_by()
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        request.Headers.Host = TunnelHost;
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await gateway.SendAsync(request, Cancellation);
        var discovery = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        Assert.Equal($"https://{TunnelHost}/connect/token", discovery.GetProperty("token_endpoint").GetString());
        Assert.Equal($"https://{TunnelHost}/connect/authorize", discovery.GetProperty("authorization_endpoint").GetString());
    }
}
