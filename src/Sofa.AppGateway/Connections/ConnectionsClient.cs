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

    public async Task<ServiceAnswer<List<ConnectionRepository>>> RepositoriesOf(
        Guid workspaceId, Guid connectionId, CancellationToken cancellation)
    {
        using var response = await http.GetAsync($"/workspaces/{workspaceId}/connections/{connectionId}/repositories", cancellation);
        return await ServiceAnswer<List<ConnectionRepository>>.ReadAsync(response, cancellation);
    }

    public async Task<ServiceAnswer<ApiKeySaved>> SaveApiKey(Guid workspaceId, ApiKeyToSave apiKey, CancellationToken cancellation)
    {
        using var response = await http.PostAsJsonAsync($"/workspaces/{workspaceId}/api-keys", apiKey, cancellation);
        return await ServiceAnswer<ApiKeySaved>.ReadAsync(response, cancellation);
    }
}
