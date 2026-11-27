using Sofa.Contracts;
using Sofa.Contracts.Connections;

namespace Sofa.AppGateway.Connections;

public sealed class ConnectionsClient(HttpClient http)
{
    public static readonly Uri Address = new($"http://{SofaServices.Connections}");

    public async Task<GitHubAppStatus> GitHubApp(CancellationToken cancellation) =>
        (await http.GetFromJsonAsync<GitHubAppStatus>("/github-app", cancellation))!;

    public async Task<List<ConnectionSummary>> ConnectionsOf(Guid workspaceId, CancellationToken cancellation) =>
        (await http.GetFromJsonAsync<List<ConnectionSummary>>($"/workspaces/{workspaceId}/connections", cancellation))!;
}
