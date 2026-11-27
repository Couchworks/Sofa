using System.Net.Http.Json;
using Sofa.Contracts;
using Sofa.Contracts.Connections;

namespace Sofa.Identity.GitHub;

public sealed class ConnectionsGitHub(HttpClient connections, ILogger<ConnectionsGitHub> logger)
{
    public static readonly Uri Address = new($"http://{SofaServices.Connections}");

    public async Task<GitHubSignInApp?> SignInAppAsync(CancellationToken cancellation)
    {
        try
        {
            using var response = await connections.GetAsync("/github-app/sign-in", cancellation);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<GitHubSignInApp>(cancellation)
                : null;
        }
        catch (HttpRequestException unreachable)
        {
            logger.LogWarning(unreachable, "Couldn't ask Connections about the GitHub App, so no one can sign in with GitHub");
            return null;
        }
    }

    public async Task<(GitHubAccountOfLogin? Account, bool GitHubAnswered)> AccountOfLoginAsync(
        string login, CancellationToken cancellation)
    {
        try
        {
            using var response = await connections.GetAsync($"/github-accounts/{Uri.EscapeDataString(login)}", cancellation);
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<GitHubAccountOfLogin>(cancellation), true),
                System.Net.HttpStatusCode.NotFound => (null, true),
                _ => (null, false),
            };
        }
        catch (HttpRequestException unreachable)
        {
            logger.LogWarning(unreachable, "Couldn't ask Connections whose GitHub login {Login} is", login);
            return (null, false);
        }
    }

    public async Task<GitHubUserAccount?> AccountOfAsync(string code, CancellationToken cancellation)
    {
        using var response = await connections.PostAsJsonAsync("/github-app/sign-in/accounts", new GitHubUserCode(code), cancellation);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<GitHubUserAccount>(cancellation)
            : null;
    }
}
