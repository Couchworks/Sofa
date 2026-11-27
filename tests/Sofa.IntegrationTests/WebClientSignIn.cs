using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;

namespace Sofa.IntegrationTests;

public sealed class WebClientSignIn : IDisposable
{
    private const string WebClientId = "sofa-web";
    private const string Scopes = "openid profile email offline_access";

    private readonly Uri _gateway;
    private readonly HttpClient _browser;

    public WebClientSignIn(Uri gateway)
    {
        _gateway = gateway;
        Cookies = new CookieContainer();
        _browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = Cookies })
        {
            BaseAddress = gateway,
        };
    }

    public Uri RedirectUri => new(_gateway, "/signed-in");

    public Tokens? Tokens { get; private set; }

    public CookieContainer Cookies { get; }

    public string AuthorizationRequest(string codeChallenge, string state) =>
        "/connect/authorize"
        + $"?client_id={WebClientId}&response_type=code&scope={Uri.EscapeDataString(Scopes)}"
        + $"&redirect_uri={Uri.EscapeDataString(RedirectUri.ToString())}"
        + $"&code_challenge={codeChallenge}&code_challenge_method=S256&state={state}";

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, CancellationToken cancellation) =>
        _browser.GetAsync(pathAndQuery, cancellation);

    public Task<HttpResponseMessage> PostSignInAsync(string email, string password, CancellationToken cancellation) =>
        _browser.PostAsJsonAsync("/account/sign-in", new { email, password }, cancellation);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string pathAndQuery, object? json, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, pathAndQuery)
        {
            Content = json is null ? null : JsonContent.Create(json),
        };
        if (Tokens is { } tokens)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }

        return await _browser.SendAsync(request, cancellation);
    }

    public Task<HttpResponseMessage> PostAsJsonAsync(string pathAndQuery, object? json, CancellationToken cancellation) =>
        SendAsync(HttpMethod.Post, pathAndQuery, json, cancellation);

    public Task<HttpResponseMessage> GetWithTokenAsync(string pathAndQuery, CancellationToken cancellation) =>
        SendAsync(HttpMethod.Get, pathAndQuery, json: null, cancellation);

    public async Task<Tokens> SignInAsync(string email, string password, CancellationToken cancellation)
    {
        var started = await StartAuthorizingAsync(cancellation);

        using var signedIn = await PostSignInAsync(email, password, cancellation);
        signedIn.EnsureSuccessStatusCode();

        return await FinishAuthorizingAsync(started, cancellation);
    }

    public async Task<GitHubSignIn> SignInWithGitHubAsync(string code, CancellationToken cancellation)
    {
        var started = await StartAuthorizingAsync(cancellation);

        using var toGitHub = await _browser.GetAsync(
            $"/account/github/sign-in?returnUrl={Uri.EscapeDataString(started.ReturnUrl)}", cancellation);
        var gitHubState = StateIn(toGitHub.Headers.Location
            ?? throw new InvalidOperationException($"Signing in with GitHub answered {toGitHub.StatusCode}, not a redirect to GitHub."));

        using var backFromGitHub = await ReturnFromGitHubAsync(code, gitHubState, cancellation);
        var landedOn = backFromGitHub.Headers.Location!.OriginalString;
        if (landedOn != started.ReturnUrl)
        {
            return new GitHubSignIn(null, landedOn, gitHubState);
        }

        return new GitHubSignIn(await FinishAuthorizingAsync(started, cancellation), landedOn, gitHubState);
    }

    public Task<HttpResponseMessage> ReturnFromGitHubAsync(string code, string state, CancellationToken cancellation) =>
        _browser.GetAsync(
            $"/account/github/callback?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}", cancellation);

    public static string StateIn(Uri gitHubAddress) => HttpUtility.ParseQueryString(gitHubAddress.Query)["state"]!;

    private async Task<StartedAuthorizing> StartAuthorizingAsync(CancellationToken cancellation)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");
        var authorize = AuthorizationRequest(challenge, state);

        using var toSignInPage = await _browser.GetAsync(authorize, cancellation);
        var signInPage = toSignInPage.Headers.Location
            ?? throw new InvalidOperationException($"Authorizing answered {toSignInPage.StatusCode}, not a redirect to the sign-in page.");
        var returnUrl = HttpUtility.ParseQueryString(signInPage.OriginalString.Split('?', 2)[1])["returnUrl"]!;
        return new StartedAuthorizing(verifier, state, returnUrl);
    }

    private async Task<Tokens> FinishAuthorizingAsync(StartedAuthorizing started, CancellationToken cancellation)
    {
        var (verifier, state, returnUrl) = started;
        using var toWebClient = await _browser.GetAsync(returnUrl, cancellation);
        var callback = toWebClient.Headers.Location
            ?? throw new InvalidOperationException($"Authorizing a signed-in User answered {toWebClient.StatusCode}.");
        var answer = HttpUtility.ParseQueryString(callback.Query);
        if (answer["state"] != state)
        {
            throw new InvalidOperationException("The authorization answer's state is not the request's.");
        }

        return Tokens = await RedeemAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = WebClientId,
            ["code"] = answer["code"]!,
            ["redirect_uri"] = RedirectUri.ToString(),
            ["code_verifier"] = verifier,
        }, cancellation);
    }

    public Task<Tokens> RefreshAsync(string refreshToken, CancellationToken cancellation) =>
        RedeemAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = WebClientId,
            ["refresh_token"] = refreshToken,
        }, cancellation);

    private async Task<Tokens> RedeemAsync(Dictionary<string, string> form, CancellationToken cancellation)
    {
        using var redeemed = await _browser.PostAsync("/connect/token", new FormUrlEncodedContent(form), cancellation);
        if (!redeemed.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"The token endpoint answered {redeemed.StatusCode}: {await redeemed.Content.ReadAsStringAsync(cancellation)}");
        }
        return (await redeemed.Content.ReadFromJsonAsync<Tokens>(cancellation))!;
    }

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _browser.Dispose();

    private sealed record StartedAuthorizing(string Verifier, string State, string ReturnUrl);
}

public sealed record GitHubSignIn(Tokens? Tokens, string LandedOn, string GitHubState);

public sealed record Tokens(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);
