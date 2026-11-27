using System.Net;
using System.Net.Sockets;

namespace Sofa.Connections;

public sealed class PublicAddressOptions
{
    public const string Section = "Sofa";

    public Uri? PublicAddress { get; set; }
}

public sealed record PublicAddress(Uri Address)
{
    public Uri Resolve(string path) => new(Address, path.TrimStart('/'));

    public bool GitHubCanReachIt => !IsLocalHost(Address.Host) && !IsPrivateNetwork(Address.Host);

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivateNetwork(string host)
    {
        if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
        {
            return true;
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254);
    }
}
