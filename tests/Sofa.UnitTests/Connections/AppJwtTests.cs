using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sofa.Connections.GitHub;

namespace Sofa.UnitTests.Connections;

public class AppJwtTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Is_signed_with_the_Apps_private_key()
    {
        using var rsa = RSA.Create(2048);

        var jwt = AppJwt.Issue("Iv1.client", rsa.ExportRSAPrivateKeyPem(), Now);

        var parts = jwt.Split('.');
        Assert.True(rsa.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), FromBase64Url(parts[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Names_the_App_and_lives_less_than_the_ten_minutes_GitHub_allows()
    {
        using var rsa = RSA.Create(2048);

        var jwt = AppJwt.Issue("Iv1.client", rsa.ExportRSAPrivateKeyPem(), Now);

        var claims = JsonDocument.Parse(FromBase64Url(jwt.Split('.')[1])).RootElement;
        Assert.Equal("Iv1.client", claims.GetProperty("iss").GetString());
        var issuedAt = claims.GetProperty("iat").GetInt64();
        var expires = claims.GetProperty("exp").GetInt64();
        Assert.True(issuedAt < Now.ToUnixTimeSeconds());
        Assert.True(expires - issuedAt <= TimeSpan.FromMinutes(10).TotalSeconds);
    }

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
