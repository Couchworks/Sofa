using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Connections;

namespace Sofa.Code.GitHub;

public sealed class ConnectionsTokens(HttpClient connections)
{
    public static readonly Uri Address = new($"http://{SofaServices.Connections}");

    private static readonly IReadOnlyDictionary<string, string> ReadsPullRequests =
        new Dictionary<string, string> { ["metadata"] = "read", ["pull_requests"] = "read" };

    private static readonly IReadOnlyDictionary<string, string> ChangesRefs =
        new Dictionary<string, string> { ["metadata"] = "read", ["contents"] = "write" };

    public async Task<TokenAnswer> IssueAsync(
        Guid connectionId, IReadOnlyList<string> repositoryNames, IReadOnlyDictionary<string, string> permissions, CancellationToken cancellation)
    {
        using var response = await connections.PostAsJsonAsync(
            $"/connections/{connectionId}/installation-tokens", new InstallationTokenRequest(repositoryNames, permissions), cancellation);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>(cancellation);
            return new TokenAnswer(null, problem?.Title ?? $"Connections answered {(int)response.StatusCode}.");
        }

        return new TokenAnswer(await response.Content.ReadFromJsonAsync<InstallationToken>(cancellation), null);
    }

    public async Task<string> ToReadPullRequestsOfAsync(Guid connectionId, string repository, CancellationToken cancellation) =>
        await TokenOrThrowAsync(connectionId, repository, ReadsPullRequests, cancellation);

    public async Task<string> ToChangeRefsOfAsync(Guid connectionId, string repository, CancellationToken cancellation) =>
        await TokenOrThrowAsync(connectionId, repository, ChangesRefs, cancellation);

    public async Task<string?> AppBotAsync(CancellationToken cancellation)
    {
        using var response = await connections.GetAsync("/github-app/bot", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GitHubAppBot>(cancellation))?.Login;
    }

    private async Task<string> TokenOrThrowAsync(
        Guid connectionId, string repository, IReadOnlyDictionary<string, string> permissions, CancellationToken cancellation)
    {
        var answer = await IssueAsync(connectionId, [repository[(repository.IndexOf('/') + 1)..]], permissions, cancellation);
        return answer.Token?.Token ?? throw new InvalidOperationException($"Connections gave no token for {repository}: {answer.Problem}");
    }

    private sealed record Problem(string? Title);
}

public sealed record TokenAnswer(InstallationToken? Token, string? Problem);
