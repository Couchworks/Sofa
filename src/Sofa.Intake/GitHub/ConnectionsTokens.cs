using Sofa.Contracts;
using Sofa.Contracts.Connections;

namespace Sofa.Intake.GitHub;

public sealed class ConnectionsTokens(HttpClient connections)
{
    public static readonly Uri Address = new($"http://{SofaServices.Connections}");

    private static readonly IReadOnlyDictionary<string, string> WritesIssues = new Dictionary<string, string> { ["issues"] = "write" };

    public async Task<string> ToWriteIssuesOfAsync(Guid connectionId, string repository, CancellationToken cancellation)
    {
        var name = repository[(repository.IndexOf('/') + 1)..];
        using var response = await connections.PostAsJsonAsync(
            $"/connections/{connectionId}/installation-tokens", new InstallationTokenRequest([name], WritesIssues), cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstallationToken>(cancellation))!.Token;
    }
}
