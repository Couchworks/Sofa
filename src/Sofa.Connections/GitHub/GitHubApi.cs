using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public sealed class GitHubApi(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int LargestPage = 100;

    public static void Configure(HttpClient http, GitHubOptions options)
    {
        http.BaseAddress = options.ApiAddress;
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Sofa", "1.0"));
    }

    public async Task<ConvertedManifest> ConvertManifest(string code, CancellationToken cancellation)
    {
        using var response = await http.PostAsync($"app-manifests/{Uri.EscapeDataString(code)}/conversions", content: null, cancellation);
        return await Read<ConvertedManifest>(response, cancellation);
    }

    public async Task<string?> UserAccessToken(
        Uri webAddress, string clientId, string clientSecret, string code, Uri redirectUri, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(webAddress, "login/oauth/access_token"))
        {
            Content = JsonContent.Create(
                new { ClientId = clientId, ClientSecret = clientSecret, Code = code, RedirectUri = redirectUri.ToString() }, options: Json),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, cancellation);
        var answer = await Read<UserAccessTokenAnswer>(response, cancellation);
        return string.IsNullOrEmpty(answer.AccessToken) ? null : answer.AccessToken;
    }

    public async Task<GitHubUser> User(string userAccessToken, CancellationToken cancellation)
    {
        using var request = AsUser(HttpMethod.Get, "user", userAccessToken);
        using var response = await http.SendAsync(request, cancellation);
        return await Read<GitHubUser>(response, cancellation);
    }

    public async Task<string?> VerifiedPrimaryEmail(string userAccessToken, CancellationToken cancellation)
    {
        using var request = AsUser(HttpMethod.Get, "user/emails", userAccessToken);
        using var response = await http.SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var emails = await response.Content.ReadFromJsonAsync<List<GitHubEmail>>(Json, cancellation) ?? [];
        return emails.FirstOrDefault(email => email.Primary && email.Verified)?.Email;
    }

    public async Task<GitHubInstallation> Installation(string appJwt, long installationId, CancellationToken cancellation)
    {
        using var request = AsApp(HttpMethod.Get, $"app/installations/{installationId}", appJwt);
        using var response = await http.SendAsync(request, cancellation);
        return await Read<GitHubInstallation>(response, cancellation);
    }

    public async Task<CreatedInstallationToken> CreateInstallationToken(
        string appJwt, long installationId, InstallationTokenRequest scope, CancellationToken cancellation)
    {
        using var request = AsApp(HttpMethod.Post, $"app/installations/{installationId}/access_tokens", appJwt);
        request.Content = JsonContent.Create(scope, options: Json);
        using var response = await http.SendAsync(request, cancellation);
        return await Read<CreatedInstallationToken>(response, cancellation);
    }

    public async Task<IReadOnlyList<HookDelivery>> DeliveriesSince(string appJwt, DateTimeOffset since, CancellationToken cancellation)
    {
        var deliveries = new List<HookDelivery>();
        string? page = $"app/hook/deliveries?per_page={LargestPage}";
        while (page is not null)
        {
            using var request = AsApp(HttpMethod.Get, page, appJwt);
            using var response = await http.SendAsync(request, cancellation);
            var onPage = await Read<List<HookDelivery>>(response, cancellation);
            deliveries.AddRange(onPage.Where(d => d.DeliveredAt >= since));
            var reachedSince = onPage.Count == 0 || onPage.Min(d => d.DeliveredAt) < since;
            page = reachedSince ? null : NextPage(response);
        }

        return deliveries;
    }

    public async Task Redeliver(string appJwt, long deliveryId, CancellationToken cancellation)
    {
        using var request = AsApp(HttpMethod.Post, $"app/hook/deliveries/{deliveryId}/attempts", appJwt);
        using var response = await http.SendAsync(request, cancellation);
        await EnsureSucceeded(response, cancellation);
    }

    private static HttpRequestMessage AsApp(HttpMethod method, string path, string appJwt) =>
        new(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", appJwt) } };

    private static HttpRequestMessage AsUser(HttpMethod method, string path, string userAccessToken) =>
        new(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken) } };

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken cancellation)
    {
        await EnsureSucceeded(response, cancellation);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellation)
            ?? throw new GitHubRefused(response.RequestMessage?.RequestUri, (int)response.StatusCode, "an empty answer");
    }

    private static async Task EnsureSucceeded(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellation);
            throw new GitHubRefused(response.RequestMessage?.RequestUri, (int)response.StatusCode, body);
        }
    }

    private static string? NextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var links))
        {
            return null;
        }

        foreach (var link in links.SelectMany(l => l.Split(',')))
        {
            var parts = link.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts.Skip(1).Any(p => p == "rel=\"next\""))
            {
                return parts[0].Trim('<', '>');
            }
        }

        return null;
    }
}

public sealed class GitHubRefused(Uri? request, int status, string answer)
    : Exception($"GitHub answered {status} to {request?.AbsolutePath}: {answer}")
{
    public int Status { get; } = status;
}

public sealed record GitHubAccount(string Login, string Type);

public sealed record ConvertedManifest(
    long Id,
    string Slug,
    string Name,
    string HtmlUrl,
    string ClientId,
    string ClientSecret,
    string? WebhookSecret,
    string Pem,
    GitHubAccount Owner);

public sealed record GitHubInstallation(long Id, GitHubAccount Account, DateTimeOffset? SuspendedAt);

public sealed record UserAccessTokenAnswer(string? AccessToken, string? Error);

public sealed record GitHubUser(long Id, string Login, string? Name);

public sealed record GitHubEmail(string Email, bool Primary, bool Verified);

public sealed record CreatedInstallationToken(string Token, DateTimeOffset ExpiresAt);

public sealed record HookDelivery(
    long Id,
    Guid Guid,
    DateTimeOffset DeliveredAt,
    bool Redelivery,
    int StatusCode,
    string Event,
    string? Action);
