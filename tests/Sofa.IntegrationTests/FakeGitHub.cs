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
    public const string AppBot = $"{AppSlug}[bot]";

    public static readonly string InstallationToken = $"ghs_fake{InstallationId}{new string('0', 32)}";
    public const char CodeNamesItsAccount = ':';

    public static readonly GitHubAccountOnFake Ada = new(5001, "ada-gh", "Ada Lovelace", "ada@example.com");
    public static readonly GitHubAccountOnFake Bea = new(5002, "bea-gh", "Bea Kowalski", "bea@example.com");
    public static readonly GitHubAccountOnFake Mallory = new(5003, "mallory", "Mallory", "mallory@example.com");
    public static readonly GitHubAccountOnFake Carol = new(5004, "carol-gh", "Carol Shaw", "carol@example.com");
    public static readonly GitHubAccountOnFake Dave = new(5005, "dave-gh", "Dave", "dave@example.com");
    public static readonly GitHubAccountOnFake Erin = new(5006, "erin-gh", "Erin Catto", "erin@example.com");
    public static readonly GitHubAccountOnFake Fay = new(5007, "fay-gh", "Fay", "fay@example.com");
    public static readonly GitHubAccountOnFake Gus = new(5008, "gus-gh", "Gus", "gus@example.com");
    public static readonly GitHubAccountOnFake Hal = new(5009, "hal-gh", "Hal", "hal@example.com");
    public static readonly GitHubAccountOnFake Ida = new(5010, "ida-gh", "Ida", "ida@example.com");
    public static readonly GitHubAccountOnFake Jo = new(5011, "jo-gh", "Jo", "jo@example.com");
    public static readonly GitHubAccountOnFake Kim = new(5012, "kim-gh", "Kim", "kim@example.com");
    public static readonly GitHubAccountOnFake Lu = new(5013, "lu-gh", "Lu", "lu@example.com");
    public static readonly GitHubAccountOnFake Max = new(5014, "max-gh", "Max", "max@example.com");
    public static readonly GitHubAccountOnFake Nia = new(5015, "nia-gh", "Nia", "nia@example.com");
    public static readonly GitHubAccountOnFake Oz = new(5016, "oz-gh", "Oz", "oz@example.com");
    public static readonly GitHubAccountOnFake Pia = new(5017, "pia-gh", "Pia", "pia@example.com");
    public static readonly GitHubAccountOnFake Quinn = new(5018, "quinn-gh", "Quinn", "quinn@example.com");
    public static readonly GitHubAccountOnFake Rae = new(5019, "rae-gh", "Rae", "rae@example.com");
    public static readonly GitHubAccountOnFake Sam = new(5020, "sam-gh", "Sam", "sam@example.com");
    public static readonly GitHubAccountOnFake Lee = new(5021, "lee-gh", "Lee", "lee@example.com");
    public static readonly GitHubAccountOnFake Noa = new(5022, "noa-gh", "Noa", "noa@example.com");
    public static readonly GitHubAccountOnFake Uma = new(5023, "uma-gh", "Uma", "uma@example.com");

    public static readonly RepositoryOnFake[] Repositories =
    [
        new("acme/demo", Private: false),
        new("acme/web", Private: true),
        new("acme/api", Private: true),
    ];

    private readonly ConcurrentDictionary<long, GitHubAccountOnFake> _accounts =
        new(new[] { Ada, Bea, Mallory, Carol, Dave, Erin, Fay, Gus, Hal, Ida, Jo, Kim, Lu, Max, Nia, Oz, Pia, Quinn, Rae, Sam, Lee, Noa, Uma }.Select(account => KeyValuePair.Create(account.Id, account)));

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

    public ConcurrentDictionary<long, CommentOnFake> Comments { get; } = new();

    public ConcurrentQueue<IssueChangeOnFake> IssueChanges { get; } = new();

    public ConcurrentQueue<RefChangeOnFake> RefChanges { get; } = new();

    public ConcurrentDictionary<(string Repository, long Number), PullRequestOnFake> PullRequests { get; } = new();

    public ConcurrentDictionary<(string Repository, long Number), IssueOnFake> Issues { get; } = new();

    public void Write(string repository, long number, string title, string body) =>
        Issues[(repository, number)] = new IssueOnFake(title, body);

    private long _lastCommentId = 1_000_000;

    public long NextCommentId() => Interlocked.Increment(ref _lastCommentId);

    public IReadOnlyList<CommentOnFake> CommentsOn(string repository, long number) =>
        Comments.Values.Where(c => c.Repository == repository && c.Number == number).OrderBy(c => c.Id).ToList();

    public IReadOnlyList<string> ChangesOf(string repository, long number) =>
        IssueChanges.Where(c => c.Repository == repository && c.Number == number).Select(c => c.Change).ToList();

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
            return fromTheApp && AccountWithLogin(login) is { } account
                ? Results.Json(new { access_token = $"ghu_{account.Id}", token_type = "bearer" })
                : Results.Json(new { error = "bad_verification_code" });
        });

        _server.MapGet("/users/{login}", (string login) =>
            login.Equals(Organisation, StringComparison.OrdinalIgnoreCase)
                ? Results.Json(new { id = 9100, login = Organisation, type = "Organization" })
                : AccountWithLogin(login) is { } account
                    ? Results.Json(new { id = account.Id, login = account.Login, type = "User" })
                    : Results.NotFound());

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
            return Results.Json(new { token = InstallationToken, expires_at = DateTimeOffset.UtcNow.AddHours(1) },
                statusCode: StatusCodes.Status201Created);
        });

        _server.MapGet("/installation/repositories", (HttpRequest request) =>
            ByTheInstallation(request)
                ? Results.Json(new
                {
                    total_count = Repositories.Length,
                    repositories = Repositories.Select(repository => new { full_name = repository.FullName, @private = repository.Private }),
                })
                : Results.Unauthorized());

        _server.MapGet("/repos/{owner}/{repo}/issues/{number:long}", (string owner, string repo, long number, HttpRequest request) =>
            !ByTheInstallation(request) ? Results.Unauthorized()
            : Issues.TryGetValue(($"{owner}/{repo}", number), out var issue)
                ? Results.Json(new { number, title = issue.Title, body = issue.Body, state = "open", html_url = $"https://github.com/{owner}/{repo}/issues/{number}" })
                : Results.NotFound());

        _server.MapPost("/repos/{owner}/{repo}/issues/{number:long}/comments", async (string owner, string repo, long number, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            var body = (await JsonSerializer.DeserializeAsync<JsonElement>(request.Body)).GetProperty("body").GetString()!;
            var comment = new CommentOnFake(NextCommentId(), $"{owner}/{repo}", number, body, Edits: 0);
            Comments[comment.Id] = comment;
            return Results.Json(new { id = comment.Id, body }, statusCode: StatusCodes.Status201Created);
        });

        _server.MapMethods("/repos/{owner}/{repo}/issues/comments/{id:long}", ["PATCH"], async (long id, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            if (!Comments.TryGetValue(id, out var comment))
            {
                return Results.NotFound();
            }

            var body = (await JsonSerializer.DeserializeAsync<JsonElement>(request.Body)).GetProperty("body").GetString()!;
            Comments[id] = comment with { Body = body, Edits = comment.Edits + 1 };
            return Results.Json(new { id, body });
        });

        _server.MapPost("/repos/{owner}/{repo}/issues/{number:long}/labels", async (string owner, string repo, long number, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            var labels = (await JsonSerializer.DeserializeAsync<JsonElement>(request.Body)).GetProperty("labels")
                .EnumerateArray().Select(label => label.GetString()!).ToList();
            foreach (var label in labels)
            {
                IssueChanges.Enqueue(new IssueChangeOnFake($"{owner}/{repo}", number, $"+{label}"));
            }

            return Results.Json(labels.Select(name => new { name }));
        });

        _server.MapDelete("/repos/{owner}/{repo}/issues/{number:long}/labels/{name}", (string owner, string repo, long number, string name, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            IssueChanges.Enqueue(new IssueChangeOnFake($"{owner}/{repo}", number, $"-{name}"));
            return Results.Json(Array.Empty<object>());
        });

        _server.MapMethods("/repos/{owner}/{repo}/issues/{number:long}", ["PATCH"], async (string owner, string repo, long number, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            var change = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            IssueChanges.Enqueue(new IssueChangeOnFake($"{owner}/{repo}", number,
                $"{change.GetProperty("state").GetString()} as {change.GetProperty("state_reason").GetString()}"));
            return Results.Json(new { number, state = change.GetProperty("state").GetString() });
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

    private void ServeRefsAndPullRequests()
    {
        _server.MapGet("/repos/{owner}/{repo}/pulls/{number:long}", (string owner, string repo, long number, HttpRequest request) =>
            !ByTheInstallation(request) ? Results.Unauthorized()
            : PullRequests.TryGetValue(($"{owner}/{repo}", number), out var pull)
                ? Results.Json(new
                {
                    number,
                    title = pull.Title,
                    html_url = $"https://github.com/{owner}/{repo}/pull/{number}",
                    state = pull.Merged || pull.Closed ? "closed" : "open",
                    merged = pull.Merged,
                    head = new { @ref = pull.HeadBranch },
                    @base = new { @ref = "main" },
                    created_at = DateTimeOffset.UtcNow.AddHours(-1),
                    closed_at = pull.Merged || pull.Closed ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
                })
                : Results.NotFound());

        _server.MapMethods("/repos/{owner}/{repo}/git/refs/{**name}", ["PATCH", "DELETE"], async (string owner, string repo, string name, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            var body = request.Method == "PATCH" ? await new StreamReader(request.Body).ReadToEndAsync() : "";
            RefChanges.Enqueue(new RefChangeOnFake($"{owner}/{repo}", $"refs/{name}", request.Method, body));
            return request.Method == "PATCH" ? Results.Json(new { @ref = $"refs/{name}" }) : Results.NoContent();
        });

        _server.MapPost("/repos/{owner}/{repo}/git/refs", async (string owner, string repo, HttpRequest request) =>
        {
            if (!ByTheInstallation(request))
            {
                return Results.Unauthorized();
            }

            var body = await new StreamReader(request.Body).ReadToEndAsync();
            var created = JsonDocument.Parse(body).RootElement.GetProperty("ref").GetString()!;
            RefChanges.Enqueue(new RefChangeOnFake($"{owner}/{repo}", created, "POST", body));
            return Results.Json(new { @ref = created }, statusCode: StatusCodes.Status201Created);
        });
    }

    public static async Task<FakeGitHub> Start(CancellationToken cancellation)
    {
        var gitHub = new FakeGitHub();
        gitHub.ServeRefsAndPullRequests();
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

    public void Rename(GitHubAccountOnFake account, string newLogin) => _accounts[account.Id] = account with { Login = newLogin };

    private static bool ByTheInstallation(HttpRequest request) =>
        request.Headers.Authorization.ToString() == $"Bearer {InstallationToken}";

    private GitHubAccountOnFake? AccountWithLogin(string login) =>
        _accounts.Values.SingleOrDefault(account => account.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

    private GitHubAccountOnFake? AccountOf(HttpRequest request) =>
        _accounts.Values.SingleOrDefault(account => request.Headers.Authorization.ToString() == $"Bearer ghu_{account.Id}");

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

public sealed record RepositoryOnFake(string FullName, bool Private);

public sealed record CommentOnFake(long Id, string Repository, long Number, string Body, int Edits);

public sealed record IssueOnFake(string Title, string Body);

public sealed record IssueChangeOnFake(string Repository, long Number, string Change);

public sealed record RefChangeOnFake(string Repository, string Ref, string Method, string Body);

public sealed record PullRequestOnFake(string Title, string HeadBranch, bool Merged, bool Closed);
