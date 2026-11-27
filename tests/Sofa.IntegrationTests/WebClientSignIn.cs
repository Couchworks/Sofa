using System.Net;
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
        _browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        {
            BaseAddress = gateway,
        };
    }

    public Uri RedirectUri => new(_gateway, "/signed-in");

    public string AuthorizationRequest(string codeChallenge, string state) =>
        "/connect/authorize"
        + $"?client_id={WebClientId}&response_type=code&scope={Uri.EscapeDataString(Scopes)}"
        + $"&redirect_uri={Uri.EscapeDataString(RedirectUri.ToString())}"
        + $"&code_challenge={codeChallenge}&code_challenge_method=S256&state={state}";

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, CancellationToken cancellation) =>
        _browser.GetAsync(pathAndQuery, cancellation);

    public Task<HttpResponseMessage> PostSignInAsync(string email, string password, CancellationToken cancellation) =>
        _browser.PostAsJsonAsync("/account/sign-in", new { email, password }, cancellation);

    public async Task<Tokens> SignInAsync(string email, string password, CancellationToken cancellation)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");
        var authorize = AuthorizationRequest(challenge, state);

        using var toSignInPage = await _browser.GetAsync(authorize, cancellation);
        var signInPage = toSignInPage.Headers.Location
            ?? throw new InvalidOperationException($"Authorizing answered {toSignInPage.StatusCode}, not a redirect to the sign-in page.");
        var returnUrl = HttpUtility.ParseQueryString(signInPage.OriginalString.Split('?', 2)[1])["returnUrl"]!;

        using var signedIn = await PostSignInAsync(email, password, cancellation);
        signedIn.EnsureSuccessStatusCode();

        using var toWebClient = await _browser.GetAsync(returnUrl, cancellation);
        var callback = toWebClient.Headers.Location
            ?? throw new InvalidOperationException($"Authorizing a signed-in User answered {toWebClient.StatusCode}.");
        var answer = HttpUtility.ParseQueryString(callback.Query);
        if (answer["state"] != state)
        {
            throw new InvalidOperationException("The authorization answer's state is not the request's.");
        }

        return await RedeemAsync(new Dictionary<string, string>
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
}

public sealed record Tokens(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);
