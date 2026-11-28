using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sofa.Intake.GitHub;

public sealed class GitHubIssues(HttpClient gitHub, ConnectionsTokens tokens) : IRequestSource, IRequestReporter, IRequestState
{
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

    public async Task<IssueNow?> ReadAsync(IssueRef issue, CancellationToken cancellation)
    {
        var token = await tokens.ToReadIssuesOfAsync(issue.ConnectionId, issue.Repository, cancellation);
        using var request = new HttpRequestMessage(HttpMethod.Get, IssuePath(issue))
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        using var response = await gitHub.SendAsync(request, cancellation);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return null;
        }

        await EnsureSucceeded(response, cancellation);
        var read = await response.Content.ReadFromJsonAsync<ReadIssue>(Json, cancellation);
        return new IssueNow(read!.Title ?? "", read.Body ?? "", read.State ?? "open");
    }

    public async Task<CommentRef> PostAsync(IssueRef issue, string body, CancellationToken cancellation)
    {
        using var response = await SendAsync(issue, HttpMethod.Post, $"{IssuePath(issue)}/comments", new { body }, cancellation);
        await EnsureSucceeded(response, cancellation);
        var posted = await response.Content.ReadFromJsonAsync<PostedComment>(Json, cancellation);
        return new CommentRef(issue, posted!.Id);
    }

    public async Task<bool> UpdateAsync(CommentRef existing, string body, CancellationToken cancellation)
    {
        using var response = await SendAsync(existing.Issue, HttpMethod.Patch,
            $"repos/{existing.Issue.Repository}/issues/comments/{existing.CommentId}", new { body }, cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSucceeded(response, cancellation);
        return true;
    }

    public async Task SetAsync(IssueRef issue, WantedIssueState state, CancellationToken cancellation)
    {
        if (state.PreviousPhaseLabel is { } previous && previous != state.PhaseLabel)
        {
            using var removed = await SendAsync(issue, HttpMethod.Delete,
                $"{IssuePath(issue)}/labels/{Uri.EscapeDataString(previous)}", body: null, cancellation);
            if (removed.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureSucceeded(removed, cancellation);
            }
        }

        if (state.PhaseLabel is { } label)
        {
            using var added = await SendAsync(issue, HttpMethod.Post, $"{IssuePath(issue)}/labels", new { labels = new[] { label } }, cancellation);
            await EnsureSucceeded(added, cancellation);
        }

        if (state.Closed)
        {
            using var closed = await SendAsync(issue, HttpMethod.Patch, IssuePath(issue),
                new { state = "closed", state_reason = "completed" }, cancellation);
            await EnsureSucceeded(closed, cancellation);
        }
    }

    private static string IssuePath(IssueRef issue) => $"repos/{issue.Repository}/issues/{issue.Number}";

    private async Task<HttpResponseMessage> SendAsync(
        IssueRef issue, HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        var token = await tokens.ToWriteIssuesOfAsync(issue.ConnectionId, issue.Repository, cancellation);
        using var request = new HttpRequestMessage(method, path)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = body is null ? null : JsonContent.Create(body, options: Json),
        };
        return await gitHub.SendAsync(request, cancellation);
    }

    private static async Task EnsureSucceeded(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub answered {(int)response.StatusCode} to {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: " +
                await response.Content.ReadAsStringAsync(cancellation),
                inner: null,
                response.StatusCode);
        }
    }

    private sealed record PostedComment(long Id);

    private sealed record ReadIssue(string? Title, string? Body, string? State);
}
