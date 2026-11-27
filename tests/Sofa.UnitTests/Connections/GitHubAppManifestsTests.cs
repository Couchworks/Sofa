using System.Text.Json;
using Microsoft.Extensions.Options;
using Sofa.Connections;
using Sofa.Connections.GitHub;

namespace Sofa.UnitTests.Connections;

public class GitHubAppManifestsTests
{
    private static readonly PublicAddress Tunnel = new(new Uri("https://sofa.example.dev/"));

    [Fact]
    public void Points_GitHub_back_at_Sofas_public_address()
    {
        var manifest = Parse(ManifestsAt(Tunnel).Manifest("acme", name: null));

        Assert.Equal("https://sofa.example.dev/webhooks/github", manifest.GetProperty("hook_attributes").GetProperty("url").GetString());
        Assert.True(manifest.GetProperty("hook_attributes").GetProperty("active").GetBoolean());
        Assert.Equal("https://sofa.example.dev/github-app/created", manifest.GetProperty("redirect_url").GetString());
        Assert.Equal("https://sofa.example.dev/github-app/installed", manifest.GetProperty("setup_url").GetString());
    }

    [Fact]
    public void Asks_for_a_private_App_that_reads_issues_and_writes_code()
    {
        var manifest = Parse(ManifestsAt(Tunnel).Manifest("acme", name: null));

        Assert.False(manifest.GetProperty("public").GetBoolean());
        var permissions = manifest.GetProperty("default_permissions");
        Assert.Equal("write", permissions.GetProperty("issues").GetString());
        Assert.Equal("write", permissions.GetProperty("contents").GetString());
        Assert.Equal("write", permissions.GetProperty("pull_requests").GetString());
        Assert.Contains("issues", manifest.GetProperty("default_events").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Leaves_webhooks_off_where_GitHub_cannot_reach_Sofa()
    {
        var manifest = Parse(ManifestsAt(new PublicAddress(new Uri("http://localhost:5176/"))).Manifest(null, null));

        Assert.False(manifest.GetProperty("hook_attributes").GetProperty("active").GetBoolean());
    }

    [Fact]
    public void Keeps_the_Apps_name_within_GitHubs_limit()
    {
        var manifest = Parse(ManifestsAt(Tunnel).Manifest("an-organisation-with-a-very-long-name", null));

        Assert.True(manifest.GetProperty("name").GetString()!.Length <= GitHubAppManifests.LongestAppName);
    }

    [Theory]
    [InlineData(null, "https://github.com/settings/apps/new?state=abc")]
    [InlineData("acme", "https://github.com/organizations/acme/settings/apps/new?state=abc")]
    public void Sends_the_manifest_to_the_owners_new_App_page(string? organisation, string expected) =>
        Assert.Equal(expected, ManifestsAt(Tunnel).FormAction("abc", organisation).ToString());

    [Theory]
    [InlineData("acme", true)]
    [InlineData("acme-web-2", true)]
    [InlineData("-acme", false)]
    [InlineData("acme/../evil", false)]
    [InlineData("acme?x=1", false)]
    public void Knows_a_GitHub_account_login(string login, bool expected) =>
        Assert.Equal(expected, GitHubAppManifests.IsAccountLogin(login));

    private static GitHubAppManifests ManifestsAt(PublicAddress address) =>
        new(Options.Create(new GitHubOptions()), address);

    private static JsonElement Parse(string manifest) => JsonDocument.Parse(manifest).RootElement;
}
