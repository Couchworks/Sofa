using System.Security.Cryptography;
using System.Text;

namespace Sofa.Execution.Workers;

public static class EgressRelay
{
    public const int AgentGatewayPort = 8080;
    public const int TlsPort = 443;
    public const string BoxAsTheRelaySeesIt = "host.docker.internal";

    private const string NowhereToRelayTo = "127.0.0.1:1";
    private const string DockersOwnResolver = "127.0.0.11";

    public static string Configuration(IReadOnlyList<string> hosts, Uri agentGateway) =>
        $$"""
        worker_processes 1;
        error_log /dev/stderr warn;
        events { worker_connections 1024; }
        stream {
          resolver {{DockersOwnResolver}} valid=60s ipv6=off;
          log_format relayed '$remote_addr $ssl_preread_server_name $status $bytes_sent $bytes_received';
          access_log /dev/stdout relayed;
          map $ssl_preread_server_name $sofa_allowed {
        {{string.Join('\n', hosts.Select(host => $"    {host} {host}:{TlsPort};"))}}
            default {{NowhereToRelayTo}};
          }
          server { listen {{TlsPort}}; ssl_preread on; proxy_connect_timeout 10s; proxy_pass $sofa_allowed; }
          server { listen {{AgentGatewayPort}}; proxy_pass {{agentGateway.Host}}:{{agentGateway.Port}}; }
        }
        """;

    public static string FingerprintOf(string configuration) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(configuration)))[..16];

    public static WorkerPlacement PlacementBehind(string network, string relayAddress, IReadOnlyList<string> hosts) =>
        new(new WorkerNetwork(network, hosts.ToDictionary(host => host, _ => relayAddress)),
            new Uri($"http://{relayAddress}:{AgentGatewayPort}/"));
}
