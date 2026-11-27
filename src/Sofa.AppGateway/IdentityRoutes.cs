using Sofa.Contracts;
using Yarp.ReverseProxy.Configuration;

namespace Sofa.AppGateway;

public static class IdentityRoutes
{
    private const string Cluster = SofaServices.Identity;
    private const int BeforeTheWebClientsCatchAll = 100;

    public static readonly IReadOnlyList<string> Paths = ["/connect/{**rest}", "/.well-known/{**rest}", "/account/{**rest}"];

    public static IReadOnlyList<RouteConfig> Routes { get; } = Paths
        .Select((path, index) => new RouteConfig
        {
            RouteId = $"identity-{index}",
            ClusterId = Cluster,
            Order = BeforeTheWebClientsCatchAll,
            Match = new RouteMatch { Path = path },
        })
        .ToList();

    public static IReadOnlyList<ClusterConfig> Clusters { get; } =
    [
        new ClusterConfig
        {
            ClusterId = Cluster,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                [SofaServices.Identity] = new() { Address = $"http://{SofaServices.Identity}" },
            },
        },
    ];
}
