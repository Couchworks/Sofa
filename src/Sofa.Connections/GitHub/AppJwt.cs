using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sofa.Connections.GitHub;

public static class AppJwt
{
    public static readonly TimeSpan ClockDriftAllowance = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(9);

    public static string Issue(string clientId, string privateKeyPem, DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = (now - ClockDriftAllowance).ToUnixTimeSeconds(),
            exp = (now + Lifetime).ToUnixTimeSeconds(),
            iss = clientId,
        }));
        var signed = $"{header}.{payload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signed}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
