using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sofa.AppGateway.Access;
using Sofa.Contracts.Connections;

namespace Sofa.AppGateway.GitHubReturns;

public sealed class GitHubReturnOptions
{
    public const string Section = "GitHubReturns";
    public const int ShortestSigningKey = 32;

    public string SigningKey { get; set; } = "";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(30);
}

public enum GitHubReturnTo
{
    Created,
    Installed,
}

public sealed class GitHubReturnStates(IOptions<GitHubReturnOptions> options, TimeProvider time)
{
    private const char PayloadAndSignature = '.';

    public TimeSpan Lifetime => options.Value.Lifetime;

    public (string State, GitHubReturn Return) Mint(GitHubReturnTo to, SignedInAdmin admin)
    {
        var started = new GitHubReturn(Guid.NewGuid(), admin.WorkspaceId, admin.UserId, time.GetUtcNow() + Lifetime);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SignedPayload(to, started));
        return ($"{Base64Url(payload)}{PayloadAndSignature}{Base64Url(Sign(payload))}", started);
    }

    public GitHubReturn? Read(string? state, GitHubReturnTo to)
    {
        if (state?.Split(PayloadAndSignature) is not [var encodedPayload, var encodedSignature]
            || FromBase64Url(encodedPayload) is not { } payload
            || FromBase64Url(encodedSignature) is not { } signature
            || !CryptographicOperations.FixedTimeEquals(Sign(payload), signature))
        {
            return null;
        }

        var signed = JsonSerializer.Deserialize<SignedPayload>(payload);
        return signed is not null && signed.To == to && signed.Return.ExpiresAt > time.GetUtcNow()
            ? signed.Return
            : null;
    }

    private byte[] Sign(byte[] payload)
    {
        var key = options.Value.SigningKey;
        if (key.Length < GitHubReturnOptions.ShortestSigningKey)
        {
            throw new InvalidOperationException(
                $"{GitHubReturnOptions.Section}:{nameof(GitHubReturnOptions.SigningKey)} must be a secret " +
                $"of at least {GitHubReturnOptions.ShortestSigningKey} characters.");
        }

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), payload);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        try
        {
            return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed record SignedPayload(GitHubReturnTo To, GitHubReturn Return);
}

public static class GitHubReturnCookie
{
    public const string Name = "sofa_github_return";
    public const string Path = "/github-app";

    public static void Remember(HttpContext http, GitHubReturn started, TimeSpan lifetime) =>
        http.Response.Cookies.Append(Name, started.Nonce.ToString("N"), new CookieOptions
        {
            Path = Path,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = lifetime,
        });

    public static bool CameFromTheSameBrowser(HttpContext http, GitHubReturn started) =>
        http.Request.Cookies[Name] is { } remembered
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(remembered), Encoding.ASCII.GetBytes(started.Nonce.ToString("N")));

    public static void Forget(HttpContext http) =>
        http.Response.Cookies.Delete(Name, new CookieOptions { Path = Path });
}
