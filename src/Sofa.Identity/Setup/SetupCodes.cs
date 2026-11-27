using System.Security.Cryptography;
using System.Text;

namespace Sofa.Identity.Setup;

public static class SetupCodes
{
    public const string Prefix = "sofa_setup_";

    private const int RandomBytes = 24;

    public static string Issue() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(RandomBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    public static bool Matches(string code, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(code)), Encoding.UTF8.GetBytes(hash));
}
