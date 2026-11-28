using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sofa.Code.GitHub;

public sealed class GitHubRepositories(HttpClient gitHub)
{
    public const string NoCommit = "0000000000000000000000000000000000000000";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Configure(HttpClient http, GitHubApiOptions options)
    {
        http.BaseAddress = options.ApiAddress;
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Sofa", "1.0"));
    }

    public async Task<PullRequestOnGitHub?> PullRequestAsync(string token, string repository, long number, CancellationToken cancellation)
    {
        using var request = As(token, HttpMethod.Get, $"repos/{repository}/pulls/{number}");
        using var response = await gitHub.SendAsync(request, cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSucceeded(response, cancellation);
        return await response.Content.ReadFromJsonAsync<PullRequestOnGitHub>(Json, cancellation);
    }

    public async Task PutRefBackAsync(string token, string repository, string gitRef, string before, CancellationToken cancellation)
    {
        var name = gitRef["refs/".Length..];
        using var request = As(token, HttpMethod.Patch, $"repos/{repository}/git/refs/{name}");
        request.Content = JsonContent.Create(new { sha = before, force = true });
        using var response = await gitHub.SendAsync(request, cancellation);
        await EnsureSucceeded(response, cancellation);
    }

    public async Task DeleteRefAsync(string token, string repository, string gitRef, CancellationToken cancellation)
    {
        using var request = As(token, HttpMethod.Delete, $"repos/{repository}/git/refs/{gitRef["refs/".Length..]}");
        using var response = await gitHub.SendAsync(request, cancellation);
        if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity))
        {
            await EnsureSucceeded(response, cancellation);
        }
    }

    public async Task CreateRefAsync(string token, string repository, string gitRef, string sha, CancellationToken cancellation)
    {
        using var request = As(token, HttpMethod.Post, $"repos/{repository}/git/refs");
        request.Content = JsonContent.Create(new { @ref = gitRef, sha });
        using var response = await gitHub.SendAsync(request, cancellation);
        await EnsureSucceeded(response, cancellation);
    }

    private static HttpRequestMessage As(string token, HttpMethod method, string path) =>
        new(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };

    private static async Task EnsureSucceeded(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub answered {(int)response.StatusCode} to {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: "
                + await response.Content.ReadAsStringAsync(cancellation));
        }
    }
}

public sealed record PullRequestOnGitHub(
    long Number, string? Title, string? HtmlUrl, string? State, bool Merged, BranchOnGitHub? Head, BranchOnGitHub? Base,
    DateTimeOffset? CreatedAt, DateTimeOffset? ClosedAt);

public sealed record BranchOnGitHub(string? Ref);
