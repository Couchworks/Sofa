using Sofa.Contracts.Flow;
using Sofa.Execution.Workers;

namespace Sofa.UnitTests.Execution;

public class EgressRelayTests
{
    private static readonly Uri AgentGateway = new("http://host.docker.internal:5159");

    [Fact]
    public void The_relay_passes_on_only_TLS_to_the_names_it_allows_and_sends_everything_else_nowhere()
    {
        var configuration = EgressRelay.Configuration(["api.anthropic.com", "github.com"], AgentGateway);

        Assert.Contains("api.anthropic.com api.anthropic.com:443;", configuration);
        Assert.Contains("github.com github.com:443;", configuration);
        Assert.Contains("default 127.0.0.1:1;", configuration);
        Assert.Contains("ssl_preread on;", configuration);
        Assert.DoesNotContain("listen 80;", configuration);
    }

    [Fact]
    public void The_relay_passes_its_own_port_on_to_the_Agent_Gateway_and_to_no_other_port_of_the_box()
    {
        var configuration = EgressRelay.Configuration(["github.com"], AgentGateway);

        Assert.Contains("listen 8080; proxy_pass host.docker.internal:5159;", configuration);
        Assert.Single(configuration.Split('\n'), line => line.Contains("host.docker.internal", StringComparison.Ordinal));
    }

    [Fact]
    public void Workers_reach_their_vendors_API_and_GitHub_and_nothing_else()
    {
        Assert.Equal(["api.anthropic.com", "platform.claude.com"], WhatWorkersReach.ApiOf(Vendor.Anthropic));
        Assert.Contains("github.com", WhatWorkersReach.Hosts);
        Assert.Contains("api.github.com", WhatWorkersReach.Hosts);
        Assert.DoesNotContain(WhatWorkersReach.Hosts, host => host.Contains("statsig", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Worker_finds_every_name_it_may_reach_at_the_relay_and_the_Agent_Gateway_behind_it()
    {
        var placement = EgressRelay.PlacementBehind("sofa-workers-5159", "172.30.0.2", ["github.com", "api.anthropic.com"]);

        Assert.Equal("sofa-workers-5159", placement.Network.Name);
        Assert.All(placement.Network.HostsAt.Values, address => Assert.Equal("172.30.0.2", address));
        Assert.Equal(new Uri("http://172.30.0.2:8080/"), placement.AgentGateway);
    }

    [Fact]
    public void A_changed_configuration_has_another_fingerprint_so_the_relay_is_started_again()
    {
        Assert.NotEqual(
            EgressRelay.FingerprintOf(EgressRelay.Configuration(["github.com"], AgentGateway)),
            EgressRelay.FingerprintOf(EgressRelay.Configuration(["github.com"], new Uri("http://host.docker.internal:6000"))));
    }
}
