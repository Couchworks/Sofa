using System.Security.Cryptography;
using System.Text;

namespace Sofa.Connections.GitHub;

public static class WebhookSignature
{
    public const string Header = "X-Hub-Signature-256";

    private const string Prefix = "sha256=";

    public static string Sign(ReadOnlySpan<byte> body, string secret) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    public static bool Matches(ReadOnlySpan<byte> body, string? signature, string secret)
    {
        if (signature is null || !signature.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] given;
        try
        {
            given = Convert.FromHexString(signature.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}
