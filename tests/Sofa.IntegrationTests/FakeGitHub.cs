using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sofa.IntegrationTests;

public sealed class FakeGitHub : IAsyncDisposable
{
    public const long AppId = 4242;
    public const string AppSlug = "sofa-acme";
    public const string ClientId = "Iv23.fake-client";
    public const string Organisation = "acme";
    public const long InstallationId = 777;
    public const string ClientSecret = "fake-client-secret";
    public const char CodeNamesItsAccount = ':';

    public static readonly GitHubAccountOnFake Ada = new(5001, "ada-gh", "Ada Lovelace", "ada@example.com");
    public static readonly GitHubAccountOnFake Bea = new(5002, "bea-gh", "Bea Kowalski", "bea@example.com");
    public static readonly GitHubAccountOnFake Mallory = new(5003, "mallory", "Mallory", "mallory@example.com");

    private static readonly GitHubAccountOnFake[] Accounts = [Ada, Bea, Mallory];

    private readonly RSA _appKey = RSA.Create(2048);
    private readonly WebApplication _server;

    public string WebhookSecret { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20));

    public string PrivateKeyPem => _appKey.ExportRSAPrivateKeyPem();

    public ConcurrentBag<string> ConvertedCodes { get; } = [];

    public ConcurrentBag<JsonElement> TokenRequests { get; } = [];

    public ConcurrentBag<long> Redelivered { get; } = [];

    public List<object> Deliveries { get; } = [];

    public Uri ApiAddress { get; private set; } = null!;

    public string WebAddress => ApiAddress.ToString();

    public ConcurrentBag<string> RedirectUris { get; } = [];

    public static string CodeFor(GitHubAccountOnFake account) => $"{account.Login}{CodeNamesItsAccount}{Guid.NewGuid():N}";

    private FakeGitHub()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _server = builder.Build();

        _server.MapPost("/app-manifests/{code}/conversions", (string code) =>
        {
            ConvertedCodes.Add(code);
            return Results.Json(new
            {
                id = AppId,
                slug = AppSlug,
                name = "Sofa acme",
                html_url = $"{WebAddress}apps/{AppSlug}",
                client_id = ClientId,
                client_secret = ClientSecret,
                webhook_secret = WebhookSecret,
                pem = PrivateKeyPem,
                owner = new { login = Organisation, type = "Organization" },
            }, statusCode: StatusCodes.Status201Created);
        });

        _server.MapPost("/login/oauth/access_token", async (HttpRequest request) =>
        {
            var exchange = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            RedirectUris.Add(exchange.GetProperty("redirect_uri").GetString()!);
            var login = exchange.GetProperty("code").GetString()!.Split(CodeNamesItsAccount)[0];
            var fromTheApp = exchange.GetProperty("client_id").GetString() == ClientId
                && exchange.GetProperty("client_secret").GetString() == ClientSecret;
            return fromTheApp && Accounts.Any(account => account.Login == login)
                ? Results.Json(new { access_token = $"ghu_{login}", token_type = "bearer" })
                : Results.Json(new { error = "bad_verification_code" });
        });

        _server.MapGet("/user", (HttpRequest request) =>
            AccountOf(request) is { } account
                ? Results.Json(new { id = account.Id, login = account.Login, name = account.Name })
                : Results.Unauthorized());

        _server.MapGet("/user/emails", (HttpRequest request) =>
            AccountOf(request) is { } account
                ? Results.Json(new[]
                {
                    new { email = $"unverified-{account.VerifiedEmail}", primary = false, verified = false },
                    new { email = account.VerifiedEmail, primary = true, verified = true },
                })
                : Results.Unauthorized());

        _server.MapGet("/app/installations/{id:long}", (long id, HttpRequest request) =>
            !SignedByTheApp(request) ? Results.Unauthorized()
            : id != InstallationId ? Results.NotFound()
            : Results.Json(new { id, account = new { login = Organisation, type = "Organization" }, suspended_at = (string?)null }));

        _server.MapPost("/app/installations/{id:long}/access_tokens", async (long id, HttpRequest request) =>
        {
            if (!SignedByTheApp(request))
            {
                return Results.Unauthorized();
            }

            TokenRequests.Add(await JsonSerializer.DeserializeAsync<JsonElement>(request.Body));
            return Results.Json(new { token = $"ghs_fake_{id}", expires_at = DateTimeOffset.UtcNow.AddHours(1) },
                statusCode: StatusCodes.Status201Created);
        });

        _server.MapGet("/app/hook/deliveries", (HttpRequest request) =>
        {
            if (!SignedByTheApp(request))
            {
                return Results.Unauthorized();
            }

            lock (Deliveries)
            {
                return Results.Json(Deliveries.ToList());
            }
        });

        _server.MapPost("/app/hook/deliveries/{id:long}/attempts", (long id, HttpRequest request) =>
        {
            if (!SignedByTheApp(request))
            {
                return Results.Unauthorized();
            }

            Redelivered.Add(id);
            return Results.Accepted();
        });
    }

    public static async Task<FakeGitHub> Start(CancellationToken cancellation)
    {
        var gitHub = new FakeGitHub();
        await gitHub._server.StartAsync(cancellation);
        var address = gitHub._server.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        gitHub.ApiAddress = new Uri(address.TrimEnd('/') + "/");
        return gitHub;
    }

    public void FailedToDeliver(long deliveryId, Guid guid)
    {
        lock (Deliveries)
        {
            Deliveries.Add(new
            {
                id = deliveryId,
                guid,
                delivered_at = DateTimeOffset.UtcNow,
                redelivery = false,
                status_code = 502,
                @event = "issues",
                action = "labeled",
            });
        }
    }

    public string Sign(byte[] body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), body));

    private static GitHubAccountOnFake? AccountOf(HttpRequest request) =>
        Accounts.SingleOrDefault(account => request.Headers.Authorization.ToString() == $"Bearer ghu_{account.Login}");

    private bool SignedByTheApp(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = authorization["Bearer ".Length..].Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var signatureMatches = _appKey.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), FromBase64Url(parts[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var claims = JsonDocument.Parse(FromBase64Url(parts[1])).RootElement;
        return signatureMatches
            && claims.GetProperty("iss").GetString() == ClientId
            && claims.GetProperty("exp").GetInt64() > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
        _appKey.Dispose();
    }
}

public sealed record GitHubAccountOnFake(long Id, string Login, string Name, string VerifiedEmail);
