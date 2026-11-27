using Sofa.Connections;

namespace Sofa.UnitTests.Connections;

public class PublicAddressTests
{
    [Theory]
    [InlineData("https://sofa.example.dev/", true)]
    [InlineData("https://abc.trycloudflare.com/", true)]
    [InlineData("http://localhost:5176/", false)]
    [InlineData("http://app-gateway.dev.localhost:5176/", false)]
    [InlineData("http://127.0.0.1:5176/", false)]
    [InlineData("http://192.168.1.20/", false)]
    [InlineData("http://10.0.0.5/", false)]
    [InlineData("http://172.20.0.5/", false)]
    [InlineData("http://[::1]:5176/", false)]
    public void Knows_whether_GitHub_can_reach_it(string address, bool reachable) =>
        Assert.Equal(reachable, new PublicAddress(new Uri(address)).GitHubCanReachIt);

    [Fact]
    public void Resolves_paths_beneath_a_public_address_with_a_path_of_its_own() =>
        Assert.Equal(
            "https://example.dev/sofa/webhooks/github",
            new PublicAddress(new Uri("https://example.dev/sofa/")).Resolve("/webhooks/github").ToString());
}
